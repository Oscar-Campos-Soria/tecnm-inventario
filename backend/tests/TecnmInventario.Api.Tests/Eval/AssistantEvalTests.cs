using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Tests.Eval;

/// <summary>
/// [Theory] que se omite (en descubrimiento, como DockerFact) si faltan las variables de la evaluación:
/// la suite normal no la corre. Para correrla: API y modelo arriba, datos ZZ-IA en la BD y
///   TECNM_EVAL_API=http://localhost:5112/api
///   TECNM_EVAL_ADMIN=usuario:contraseña   (ADMIN)
///   TECNM_EVAL_USUARIO=usuario:contraseña (USUARIO_GENERAL con ZZ-IA-08 asignado)
///   dotnet test backend/TecnmInventario.slnx --filter "Category=Eval"
/// </summary>
public sealed class EvalTheoryAttribute : TheoryAttribute
{
    public EvalTheoryAttribute()
    {
        if (AssistantEval.Missing() is { } missing)
            Skip = $"Evaluación del asistente omitida: falta {missing}.";
    }
}

/// <summary>Evaluación del asistente IA contra la API real (casos en Eval/assistant-eval.json).</summary>
[Trait("Category", "Eval")]
public sealed partial class AssistantEval
{
    // Códigos de los datos de prueba: si la respuesta cita uno, debe venir de lo que consultó (nada inventado).
    [GeneratedRegex(@"\b(ZZ-IA-\d{2}|ZZ-CARGA-\d{2}|TEC-PRUEBA-TK-\d{2})\b")]
    private static partial Regex AssetCode();

    private static readonly Dictionary<string, HttpClient> Sessions = [];
    private static readonly SemaphoreSlim LoginLock = new(1, 1);

    public static string? Missing() =>
        new[] { "TECNM_EVAL_API", "TECNM_EVAL_ADMIN", "TECNM_EVAL_USUARIO" }
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)));

    public static IEnumerable<object[]> Cases()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Eval", "assistant-eval.json");
        var cases = JsonNode.Parse(File.ReadAllText(file))!["cases"]!.AsArray();
        return cases.Select(c => new object[] { (string)c!["id"]! });
    }

    [EvalTheory]
    [MemberData(nameof(Cases))]
    public async Task Caso(string id)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Eval", "assistant-eval.json");
        var spec = JsonNode.Parse(File.ReadAllText(file))!["cases"]!.AsArray().First(c => (string)c!["id"]! == id)!;
        var client = await SessionAsync((string)spec["user"]!);

        Guid? conversation = null;
        string answer = "";
        var steps = new List<JsonNode>();
        var allData = new System.Text.StringBuilder();
        foreach (var turn in spec["turns"]!.AsArray().Select(t => (string)t!))
        {
            var response = await client.PostAsJsonAsync("assistant/ask", new { question = turn, conversationId = conversation });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"[{id}] HTTP {(int)response.StatusCode}: {body}");
            var json = JsonNode.Parse(body)!;
            conversation = Guid.Parse((string)json["conversationId"]!);
            answer = (string)json["answer"]!;
            steps = json["steps"]!.AsArray().Select(s => s!).ToList();
            foreach (var s in steps) allData.Append(s["data"]?.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }

        var context = $"[{id}] respuesta: {answer}\nherramientas: {string.Join(", ", steps.Select(s => $"{s["tool"]}{(s["error"] is null ? "" : "(error)")}"))}";
        var ok = steps.Where(s => s["error"] is null).Select(s => (string)s["tool"]!).ToHashSet();
        var plain = AiText.Plain(answer);
        var data = allData.ToString();

        foreach (var tool in Strings(spec, "tools")) Assert.True(tool.Split('|').Any(ok.Contains),$"{context}\nNo usó {tool}.");
        foreach (var tool in Strings(spec, "deniedTools")) Assert.False(ok.Contains(tool), $"{context}\n{tool} no debía ejecutarse.");
        foreach (var expected in Strings(spec, "mention"))
            Assert.True(expected.Split('|').Any(e => plain.Contains(AiText.Plain(e), StringComparison.Ordinal)), $"{context}\nFalta mencionar: {expected}");
        foreach (var forbidden in Strings(spec, "notMention"))
            Assert.False(plain.Contains(AiText.Plain(forbidden), StringComparison.Ordinal), $"{context}\nNo debía mencionar: {forbidden}");
        foreach (var text in Strings(spec, "dataContains")) Assert.True(data.Contains(text, StringComparison.Ordinal), $"{context}\nLos datos no traen: {text}");
        foreach (var text in Strings(spec, "dataNotContains")) Assert.False(data.Contains(text, StringComparison.Ordinal), $"{context}\nLos datos no debían traer: {text}");
        foreach (Match code in AssetCode().Matches(answer))
            Assert.True(data.Contains(code.Value, StringComparison.OrdinalIgnoreCase) || spec["turns"]!.ToJsonString().Contains(code.Value, StringComparison.Ordinal),
                $"{context}\nCitó {code.Value} sin haberlo consultado (inventado).");
    }

    private static IEnumerable<string> Strings(JsonNode spec, string key) =>
        spec[key]?.AsArray().Select(n => (string)n!) ?? [];

    /// <summary>Una sesión por usuario para toda la corrida (el login está limitado a 10 por minuto).</summary>
    private static async Task<HttpClient> SessionAsync(string user)
    {
        await LoginLock.WaitAsync();
        try
        {
            if (Sessions.TryGetValue(user, out var existing)) return existing;
            var credentials = Environment.GetEnvironmentVariable(user == "admin" ? "TECNM_EVAL_ADMIN" : "TECNM_EVAL_USUARIO")!;
            var colon = credentials.IndexOf(':');
            var cookies = new CookieContainer();
            var api = Environment.GetEnvironmentVariable("TECNM_EVAL_API")!.TrimEnd('/') + "/";
            var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = new Uri(api), Timeout = TimeSpan.FromMinutes(4) };
            var login = await client.PostAsJsonAsync("auth/login", new { usernameOrEmail = credentials[..colon], password = credentials[(colon + 1)..] });
            Assert.True(login.IsSuccessStatusCode, $"Login de {credentials[..colon]}: HTTP {(int)login.StatusCode}");
            var csrf = cookies.GetCookies(client.BaseAddress).FirstOrDefault(c => c.Name == "tecnm_csrf")?.Value;
            if (csrf is not null) client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
            Sessions[user] = client;
            return client;
        }
        finally
        {
            LoginLock.Release();
        }
    }
}
