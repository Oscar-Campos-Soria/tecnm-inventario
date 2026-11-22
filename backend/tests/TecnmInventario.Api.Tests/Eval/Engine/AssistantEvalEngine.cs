using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace TecnmInventario.Api.Tests.Eval.Engine;

/// <summary>Se omite si falta configuración (ver README.md de esta carpeta): la suite normal no la corre.</summary>
public sealed class EvalEngineFactAttribute : FactAttribute
{
    public EvalEngineFactAttribute()
    {
        var missing = new[] { "TECNM_EVAL_API", "TECNM_EVAL_ADMIN", "TECNM_EVAL_USUARIO", "TECNM_EVAL_TECNICO" }
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)));
        if (missing is not null) Skip = $"Evaluation Engine omitido: falta {missing}.";
    }
}

/// <summary>
/// Evaluation Engine del asistente: escenarios por capacidad, variantes generadas, verdad calculada con la API,
/// métricas de tokens/tiempos (proxy) y recursos (docker stats, nvidia-smi). Escribe results.jsonl y summary.md.
/// No afirma umbrales: mide. El informe dice qué pasó y por qué.
/// </summary>
[Trait("Category", "EvalEngine")]
public sealed class AssistantEvalEngine(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly HashSet<string> NonDataTools = ["buscar_manual"];

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    private sealed record Case(string Id, string Split, string Variant, JsonObject Scenario, IReadOnlyList<string> Turns);

    [EvalEngineFact]
    public async Task Run()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Eval", "Engine", "scenarios.json");
        var scenarios = JsonNode.Parse(File.ReadAllText(file))!["scenarios"]!.AsArray().OfType<JsonObject>().ToList();
        var splits = Env("TECNM_EVAL_SPLITS", "dev,val").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var variants = int.Parse(Env("TECNM_EVAL_VARIANTS", "1"), CultureInfo.InvariantCulture);
        var only = Env("TECNM_EVAL_ONLY", "");
        var label = Env("TECNM_EVAL_LABEL", "corrida");
        var outDir = Env("TECNM_EVAL_OUT", Path.Combine(Path.GetTempPath(), "tecnm-eval", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(outDir);

        // Rehacer el informe de una corrida anterior sin volver a preguntar al modelo.
        if (Env("TECNM_EVAL_FROM", "") is { Length: > 0 } from)
        {
            var old = File.ReadAllLines(from).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
            await File.WriteAllTextAsync(Path.Combine(outDir, "summary.md"), Summary(label, old, [], TimeSpan.Zero, variants, "no medido"), new UTF8Encoding(false));
            return;
        }

        var cases = new List<Case>();
        foreach (var split in splits)
            foreach (var s in scenarios.Where(s => only.Length == 0 || ((string)s["id"]!).StartsWith(only, StringComparison.Ordinal)))
            {
                var prefix = s["turns"]?[split]?.AsArray().Select(t => (string)t!).ToList() ?? [];
                var phrasings = s["phrasings"]?[split]?.AsArray().Select(t => (string)t!).ToList() ?? [];
                for (var i = 0; i < phrasings.Count; i++)
                {
                    cases.Add(new Case($"{s["id"]}/{split}{i}", split, "original", s, [.. prefix, phrasings[i]]));
                    for (var v = 0; v < variants; v++)
                    {
                        var kind = Variants.Kinds[Math.Abs(Variants.StableHash(phrasings[i]) + v) % Variants.Kinds.Length];
                        cases.Add(new Case($"{s["id"]}/{split}{i}/{kind}", split, kind, s, [.. prefix, Variants.Make(phrasings[i], kind)]));
                    }
                }
            }

        var api = Env("TECNM_EVAL_API", "").TrimEnd('/') + "/";
        using var proxy = new LlamaProxy(Env("TECNM_EVAL_PROXY", "http://localhost:8091/"), Env("TECNM_EVAL_AI", "http://localhost:8081"));
        using var sampler = new ResourceSampler(Env("TECNM_EVAL_IA_CONTAINER", "tecnm_inventario_ia"));
        // Reposo: lo que ya consumen el contenedor y la tarjeta sin preguntas (la GPU también la usa el escritorio).
        var idleFrom = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(8));
        var idleSamples = sampler.Between(idleFrom, DateTime.UtcNow);
        var idle = idleSamples.Count == 0 ? "sin muestras" : string.Create(CultureInfo.InvariantCulture,
            $"CPU {idleSamples.Average(x => x.CpuPercent):0}% · RAM {idleSamples.Max(x => x.MemMiB):0} MiB · GPU {idleSamples.Average(x => x.GpuPercent ?? 0):0}% · VRAM {idleSamples.Max(x => x.GpuMemMiB ?? 0):0} MiB");
        var sessions = new Dictionary<string, EvalSession>();
        foreach (var role in new[] { "admin", "usuario", "tecnico" })
            sessions[role] = await EvalSession.StartAsync(api, Env($"TECNM_EVAL_{role.ToUpperInvariant()}", ""));
        var own = (await sessions["usuario"].GetJsonAsync("assets/mine")) as JsonArray ?? [];
        var ownCodes = own.Select(a => (string)a!["inventoryCode"]!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Reanudar: los casos ya escritos en results.jsonl no se repiten (TECNM_EVAL_RESUME=1).
        var resultsPath = Path.Combine(outDir, "results.jsonl");
        var rows = Env("TECNM_EVAL_RESUME", "") == "1" && File.Exists(resultsPath)
            ? File.ReadAllLines(resultsPath).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList()
            : [];
        var done = rows.Select(r => (string)r["id"]!).ToHashSet();
        cases = cases.Where(c => !done.Contains(c.Id)).ToList();
        await using var results = new StreamWriter(resultsPath, append: rows.Count > 0, new UTF8Encoding(false));
        var run = Stopwatch.StartNew();
        foreach (var (c, n) in cases.Select((c, n) => (c, n)))
        {
            var row = await RunCaseAsync(c, sessions[(string)c.Scenario["role"]!], proxy, sampler, ownCodes);
            rows.Add(row);
            await results.WriteLineAsync(row.ToJsonString(Json));
            await results.FlushAsync();
            output.WriteLine($"[{n + 1}/{cases.Count}] {c.Id} {(row["pass"]!.GetValue<bool>() ? "OK" : "FALLA")} {row["ms"]} ms");
        }

        var hashes = splits.ToDictionary(s => s, s => Judge.Sha256(string.Join("\n", scenarios.Select(x => x["id"] + (x["phrasings"]?[s]?.ToJsonString() ?? "") + (x["facts"]?.ToJsonString() ?? "") + (x["turns"]?[s]?.ToJsonString() ?? "")))));
        var summary = Summary(label, rows, hashes, run.Elapsed, variants, idle);
        await File.WriteAllTextAsync(Path.Combine(outDir, "summary.md"), summary, new UTF8Encoding(false));
        output.WriteLine($"Informe: {outDir}");
    }

    private static async Task<JsonObject> RunCaseAsync(Case c, EvalSession session, LlamaProxy proxy,
        ResourceSampler sampler, HashSet<string> ownCodes)
    {
        var s = c.Scenario;
        var role = (string)s["role"]!;
        proxy.Drain();
        var started = DateTime.UtcNow;
        Guid? conversation = null;
        string answer = "";
        string? httpError = null;
        var steps = new JsonArray();
        var allSteps = new List<JsonObject>(); // todos los turnos: para la métrica de tarea completa
        long lastMs = 0;
        foreach (var turn in c.Turns)
        {
            var watch = Stopwatch.StartNew();
            var response = await session.SendAsync(c => c.PostAsJsonAsync("assistant/ask", new { question = turn, conversationId = conversation }));
            lastMs = watch.ElapsedMilliseconds;
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                httpError = $"HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}";
                break;
            }
            var json = JsonNode.Parse(body)!;
            conversation = Guid.Parse((string)json["conversationId"]!);
            answer = (string?)json["answer"] ?? "";
            steps = json["steps"]!.AsArray();
            allSteps.AddRange(steps.OfType<JsonObject>().Select(x => (JsonObject)x.DeepClone()));
        }
        var ended = DateTime.UtcNow;
        if (conversation is { } id) (await session.SendAsync(cl => cl.DeleteAsync($"assistant/conversations/{id}"))).Dispose();
        var calls = proxy.Drain();
        var samples = sampler.Between(started, ended);

        // Verdad: la API, como ese usuario, ahora.
        var truths = new List<FactTruth>();
        foreach (var fact in s["facts"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var root = await session.GetJsonAsync((string)fact["get"]!);
            truths.Add(Judge.Truth(root!, fact, DateTime.UtcNow));
        }

        var ran = steps.OfType<JsonObject>().Select(x => (Tool: (string)x["tool"]!, Ok: x["error"] is null)).ToList();
        var okTools = ran.Where(r => r.Ok).Select(r => r.Tool).ToHashSet();
        var data = Judge.Plain(string.Concat(steps.OfType<JsonObject>().Select(x => x["data"]?.ToJsonString(Json) ?? "")));
        var plain = Judge.Plain(answer);
        var question = string.Join(" ", c.Turns);

        var toolGroups = s["tools"]?.AsArray().Select(t => (string)t!).ToList() ?? [];
        var toolsOk = toolGroups.All(g => g.Split('|').Any(okTools.Contains));
        var deniedRan = (s["deniedTools"]?.AsArray().Select(t => (string)t!) ?? []).Where(okTools.Contains).ToList();
        var dataToolCount = ran.Count(r => !NonDataTools.Contains(r.Tool));
        var kind = (string)s["kind"]!;
        var interpretOk = deniedRan.Count == 0 && kind switch
        {
            "charla" => (s["maxTools"] is null || ran.Count <= (int)s["maxTools"]!) && (s["maxDataTools"] is null || dataToolCount <= (int)s["maxDataTools"]!),
            "aclarar" => dataToolCount <= ((int?)s["maxDataTools"] ?? 0) && answer.Contains('?'),
            "denegado" => true,
            _ => toolsOk,
        };

        var factsInAnswer = truths.Select(t => Judge.InText(t, plain)).ToList();
        var factsInData = truths.Select(t => Judge.InData(t, data)).ToList();
        var mustAny = s["mustAny"]?.AsArray().Select(t => Judge.Plain((string)t!)).ToList();
        var mustOk = mustAny is null || mustAny.Any(plain.Contains);
        var notMention = (s["notMention"]?.AsArray().Select(t => (string)t!) ?? []).Where(t => answer.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        var notRegex = s["notRegex"] is { } nr && Regex.IsMatch(plain, (string)nr!);
        var answerOk = factsInAnswer.All(x => x) && mustOk && notMention.Count == 0 && !notRegex && httpError is null && answer.Length > 0;

        var questionCodes = Judge.AssetCode().Matches(question).Select(m => m.Value.ToUpperInvariant()).ToHashSet();
        var invented = Judge.AssetCode().Matches(answer).Select(m => m.Value.ToUpperInvariant())
            .Where(code => !data.Contains(code.ToLowerInvariant()) && !questionCodes.Contains(code)).Distinct().ToList();
        var leaks = new List<string>();
        if (role == "usuario")
        {
            leaks.AddRange(Judge.AssetCode().Matches(answer + " " + data).Select(m => m.Value.ToUpperInvariant())
                .Where(code => !ownCodes.Contains(code) && !questionCodes.Contains(code)).Distinct());
            leaks.AddRange(Regex.Matches(data, "\"solicitante\":\"([^\"]+)\"").Select(m => m.Groups[1].Value)
                .Where(u => !string.Equals(u, session.User, StringComparison.OrdinalIgnoreCase)).Distinct().Select(u => "solicitante:" + u));
        }
        leaks.AddRange(deniedRan.Select(t => "herramienta:" + t));

        var dataOk = factsInData.All(x => x);
        var pass = interpretOk && answerOk && dataOk && invented.Count == 0 && leaks.Count == 0;

        // Tarea completa (multiturno): lo consultado en turnos anteriores también cuenta para resolver el actual.
        var okToolsAll = allSteps.Where(x => x["error"] is null).Select(x => (string)x["tool"]!).ToHashSet();
        var dataAll = Judge.Plain(string.Concat(allSteps.Select(x => x["data"]?.ToJsonString(Json) ?? "")));
        var taskToolsOk = toolGroups.All(g => g.Split('|').Any(okToolsAll.Contains));
        var deniedAll = (s["deniedTools"]?.AsArray().Select(t => (string)t!) ?? []).Where(okToolsAll.Contains).ToList();
        var taskInterpretOk = deniedAll.Count == 0 && (kind is "consulta" or "accion" ? taskToolsOk : interpretOk);
        var taskDataOk = truths.All(t => Judge.InData(t, dataAll));
        var inventedTask = Judge.AssetCode().Matches(answer).Select(m => m.Value.ToUpperInvariant())
            .Where(code => !dataAll.Contains(code.ToLowerInvariant()) && !questionCodes.Contains(code)).Distinct().ToList();
        var taskPass = taskInterpretOk && answerOk && taskDataOk && inventedTask.Count == 0 && leaks.Count == 0 && deniedAll.Count == 0;
        // Ejecución: la herramienta correcta corrió sin error y trajo la verdad (consulta/acción); en lo demás, la interpretación.
        var execOk = kind is "consulta" or "accion" ? toolsOk && dataOk && ran.All(r => r.Ok || !toolGroups.Any(g => g.Split('|').Contains(r.Tool))) : interpretOk;
        var completion = calls.Sum(x => x.CompletionTokens);
        return new JsonObject
        {
            ["id"] = c.Id, ["scenario"] = (string)s["id"]!, ["capability"] = (string)s["capability"]!, ["role"] = role, ["kind"] = kind,
            ["split"] = c.Split, ["variant"] = c.Variant, ["question"] = c.Turns[^1], ["answer"] = answer, ["httpError"] = httpError,
            ["tools"] = new JsonArray(ran.Select(r => (JsonNode)(r.Tool + (r.Ok ? "" : "(error)"))).ToArray()),
            ["interpretOk"] = interpretOk, ["toolsOk"] = toolsOk, ["dataOk"] = dataOk, ["answerOk"] = answerOk,
            ["factsInAnswer"] = new JsonArray(factsInAnswer.Select(x => (JsonNode)x).ToArray()),
            ["truth"] = new JsonArray(truths.Select(t => (JsonNode)$"{t.Expect}:{t.Count}:{string.Join("|", t.Values.Take(8))}").ToArray()),
            ["mustOk"] = mustOk, ["notMention"] = new JsonArray(notMention.Select(x => (JsonNode)x).ToArray()), ["notRegex"] = notRegex,
            ["invented"] = new JsonArray(invented.Select(x => (JsonNode)x).ToArray()), ["leaks"] = new JsonArray(leaks.Select(x => (JsonNode)x).ToArray()),
            ["pass"] = pass, ["execOk"] = execOk, ["multiTurn"] = c.Turns.Count > 1, ["functional"] = (bool?)s["functional"] ?? true,
            ["taskInterpretOk"] = taskInterpretOk, ["taskDataOk"] = taskDataOk, ["taskPass"] = taskPass,
            ["inventedTask"] = new JsonArray(inventedTask.Select(x => (JsonNode)x).ToArray()),
            ["toolsAllTurns"] = new JsonArray(allSteps.Select(x => (JsonNode)((string)x["tool"]! + (x["error"] is null ? "" : "(error)"))).ToArray()),
            ["msPerToken"] = completion == 0 ? null : Math.Round(calls.Sum(x => x.PredictedMs) / completion, 1),
            ["ms"] = lastMs, ["totalMs"] = (long)(ended - started).TotalMilliseconds, ["cached"] = calls.Count == 0 && httpError is null,
            ["modelCalls"] = calls.Count, ["promptTokens"] = calls.Sum(x => x.PromptTokens), ["completionTokens"] = calls.Sum(x => x.CompletionTokens),
            ["cachedTokens"] = calls.Sum(x => x.CachedTokens), ["promptMs"] = Math.Round(calls.Sum(x => x.PromptMs)), ["predictedMs"] = Math.Round(calls.Sum(x => x.PredictedMs)),
            ["toolDefs"] = calls.Count == 0 ? 0 : calls.Max(x => x.Tools), ["requestBytes"] = calls.Count == 0 ? 0 : calls.Max(x => x.RequestBytes),
            ["cpuAvg"] = samples.Count == 0 ? null : Math.Round(samples.Average(x => x.CpuPercent)), ["cpuPeak"] = samples.Count == 0 ? null : samples.Max(x => x.CpuPercent),
            ["memPeakMiB"] = samples.Count == 0 ? null : Math.Round(samples.Max(x => x.MemMiB)),
            ["gpuAvg"] = samples.Any(x => x.GpuPercent is not null) ? Math.Round(samples.Average(x => x.GpuPercent ?? 0)) : null,
            ["gpuMemPeakMiB"] = samples.Any(x => x.GpuMemMiB is not null) ? samples.Max(x => x.GpuMemMiB) : null,
        };
    }

    private static string Summary(string label, List<JsonObject> rows, Dictionary<string, string> hashes, TimeSpan elapsed, int variants, string idle)
    {
        string Pct(IEnumerable<JsonObject> set, string key)
        {
            var list = set.ToList();
            return list.Count == 0 ? "-" : $"{100.0 * list.Count(r => r[key]!.GetValue<bool>()) / list.Count:0}%";
        }
        double Num(JsonObject r, string key) => r[key] is null ? 0 : double.Parse(r[key]!.ToJsonString(), CultureInfo.InvariantCulture);
        var live = rows.Where(r => !r["cached"]!.GetValue<bool>()).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"# Evaluation Engine — {label}");
        sb.AppendLine();
        sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm} · casos: {rows.Count} ({live.Count} contra el modelo, {rows.Count - live.Count} desde caché) · variantes por frase: {variants} · duración: {elapsed:hh\\:mm\\:ss}");
        sb.AppendLine($"Reposo (antes de la primera pregunta): {idle}");
        sb.AppendLine($"Hash de conjuntos: {string.Join(", ", hashes.Select(h => $"{h.Key}={h.Value}"))}");
        sb.AppendLine();
        sb.AppendLine("Columnas de siempre (comparables con la línea base): Pasa, Interpretación, Herramientas, Datos, Respuesta (último turno).");
        sb.AppendLine("Nuevas: Ejecución (herramienta correcta, sin error, con la verdad) y Tarea (toda la conversación cuenta; ver README).");
        sb.AppendLine();
        sb.AppendLine("| Grupo | Casos | Pasa | Interpretación | Herramientas | Datos | Respuesta | Ejecución | Tarea | Inventados | Fugas |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        void Line(string name, IEnumerable<JsonObject> set)
        {
            var list = set.ToList();
            if (list.Count == 0) return;
            sb.AppendLine($"| {name} | {list.Count} | {Pct(list, "pass")} | {Pct(list, "interpretOk")} | {Pct(list.Where(r => (string)r["kind"]! is "consulta" or "accion"), "toolsOk")} | {Pct(list, "dataOk")} | {Pct(list, "answerOk")} | {Pct(list.Where(r => r["execOk"] is not null), "execOk")} | {Pct(list.Where(r => r["taskPass"] is not null), "taskPass")} | {list.Count(r => r["invented"]!.AsArray().Count > 0)} | {list.Count(r => r["leaks"]!.AsArray().Count > 0)} |");
        }
        Line("**Total**", rows);
        // Del archivo de escenarios (no de la fila): así también se separa en informes de corridas anteriores.
        var nonFunctional = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Eval", "Engine", "scenarios.json")))!["scenarios"]!
            .AsArray().Where(x => (bool?)x!["functional"] == false).Select(x => (string)x!["id"]!).ToHashSet();
        Line("**Total funcional** (sin capacidades sin datos: fecha estimada, atrasados)", rows.Where(r => !nonFunctional.Contains((string)r["scenario"]!)));
        Line("un turno", rows.Where(r => r["multiTurn"]?.GetValue<bool>() == false));
        Line("multiturno", rows.Where(r => r["multiTurn"]?.GetValue<bool>() == true));
        foreach (var g in rows.GroupBy(r => (string)r["split"]!)) Line($"split {g.Key}", g);
        foreach (var g in rows.GroupBy(r => (string)r["variant"]!)) Line($"variante {g.Key}", g);
        foreach (var g in rows.GroupBy(r => (string)r["role"]!)) Line($"rol {g.Key}", g);
        foreach (var g in rows.GroupBy(r => (string)r["kind"]!)) Line($"tipo {g.Key}", g);
        sb.AppendLine();
        sb.AppendLine("## Por capacidad");
        sb.AppendLine();
        sb.AppendLine("| Capacidad | Casos | Pasa | Interpretación | Respuesta |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var g in rows.GroupBy(r => (string)r["scenario"]!))
            sb.AppendLine($"| {g.Key} | {g.Count()} | {Pct(g, "pass")} | {Pct(g, "interpretOk")} | {Pct(g, "answerOk")} |");
        sb.AppendLine();
        sb.AppendLine("## Rendimiento (casos contra el modelo)");
        sb.AppendLine();
        sb.AppendLine("| Métrica | p50 | p95 | máx | promedio |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var key in new[] { "ms", "msPerToken", "modelCalls", "promptTokens", "cachedTokens", "completionTokens", "promptMs", "predictedMs", "toolDefs", "requestBytes", "cpuAvg", "cpuPeak", "memPeakMiB", "gpuAvg", "gpuMemPeakMiB" })
        {
            var values = live.Where(r => r[key] is not null).Select(r => Num(r, key)).ToList();
            sb.AppendLine($"| {key} | {Judge.Percentile(values, 0.5)} | {Judge.Percentile(values, 0.95)} | {(values.Count == 0 ? "-" : values.Max().ToString("0", CultureInfo.InvariantCulture))} | {(values.Count == 0 ? "-" : values.Average().ToString("0", CultureInfo.InvariantCulture))} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Fallas");
        sb.AppendLine();
        foreach (var r in rows.Where(r => !r["pass"]!.GetValue<bool>()))
        {
            var why = new List<string>();
            if (r["httpError"] is not null) why.Add((string)r["httpError"]!);
            if (!r["interpretOk"]!.GetValue<bool>()) why.Add("interpretación");
            if (!r["dataOk"]!.GetValue<bool>()) why.Add("datos");
            if (!r["answerOk"]!.GetValue<bool>()) why.Add("respuesta");
            if (r["invented"]!.AsArray().Count > 0) why.Add("inventó " + r["invented"]!.ToJsonString());
            if (r["leaks"]!.AsArray().Count > 0) why.Add("fuga " + r["leaks"]!.ToJsonString());
            var answer = ((string?)r["answer"] ?? "").ReplaceLineEndings(" ");
            sb.AppendLine($"- `{r["id"]}` [{string.Join(", ", why)}] «{r["question"]}» → herramientas {r["tools"]!.ToJsonString()} · verdad {r["truth"]!.ToJsonString()} · «{answer[..Math.Min(220, answer.Length)]}»");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Sesión de un rol durante toda la corrida. El token dura 15 min: ante un 401 vuelve a iniciar sesión y reintenta
/// una vez (el login admite 10 por minuto; aquí es uno cada ~15 min por rol).
/// </summary>
public sealed class EvalSession
{
    private readonly string _api;
    private readonly string _credentials;
    public HttpClient Client { get; private set; } = null!;
    public string User => _credentials[.._credentials.IndexOf(':')];

    private EvalSession(string api, string credentials) => (_api, _credentials) = (api, credentials);

    public static async Task<EvalSession> StartAsync(string api, string credentials)
    {
        var session = new EvalSession(api, credentials);
        await session.LoginAsync();
        return session;
    }

    public async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> call)
    {
        var response = await call(Client);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        response.Dispose();
        await LoginAsync();
        return await call(Client);
    }

    public async Task<JsonNode?> GetJsonAsync(string path)
    {
        using var response = await SendAsync(c => c.GetAsync(path));
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task LoginAsync()
    {
        var colon = _credentials.IndexOf(':');
        var cookies = new CookieContainer();
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = new Uri(_api), Timeout = TimeSpan.FromMinutes(5) };
        var login = await client.PostAsJsonAsync("auth/login", new { usernameOrEmail = _credentials[..colon], password = _credentials[(colon + 1)..] });
        Assert.True(login.IsSuccessStatusCode, $"Login de {_credentials[..colon]}: HTTP {(int)login.StatusCode}");
        var csrf = cookies.GetCookies(client.BaseAddress).FirstOrDefault(c => c.Name == "tecnm_csrf")?.Value;
        if (csrf is not null) client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        Client?.Dispose();
        Client = client;
    }
}
