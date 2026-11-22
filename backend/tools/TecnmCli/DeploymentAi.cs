using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TecnmCli;

/// <summary>Asistente IA: modelo local en el contenedor "ia" (perfil de compose), sin puerto publicado.</summary>
internal sealed partial class Deployment
{
    private string ModelDir => Path.Combine(Root, "database", "models");
    private string PathOf(AiModel.ModelFile file) => Path.Combine(ModelDir, file.FileName);

    public string AiStatusLine() => AiModel.Enabled(Env) ? (AiHealthy() ? "IA: ● lista" : "IA: ○ sin responder") : "IA: apagada";

    /// <param name="gpu">auto, cpu, nvidia o vulkan (ya validado).</param>
    public bool AiInstall(string? from, string gpu = "auto")
    {
        Ui.Title("Instalar asistente IA");
        Ready();
        Directory.CreateDirectory(ModelDir);

        foreach (var file in AiModel.Files)
        {
            if (AiModel.Matches(PathOf(file), file)) Ui.Ok($"{file.FileName} ya está y su huella coincide.");
            else if (!(from is null ? Download(file) : CopyFrom(from, file))) return false;
        }
        foreach (var old in AiModel.Stale(Directory.GetFiles(ModelDir).Select(Path.GetFileName)!))
        {
            File.Delete(Path.Combine(ModelDir, old));
            Ui.Info($"Modelo anterior borrado: {old}");
        }

        var renderNode = OperatingSystem.IsLinux() && Directory.Exists("/dev/dri") && Directory.GetFiles("/dev/dri", "renderD*").Length > 0;
        var mode = AiAccel.Choose(gpu, NvidiaVramMiB(), renderNode);
        var env = ProdEnv.Set(File.ReadAllText(EnvFile), "COMPOSE_PROFILES", AiModel.Profile);
        if (!Env.ContainsKey("IA_THREADS")) env = ProdEnv.Set(env, "IA_THREADS", AiAccel.Threads(Environment.ProcessorCount).ToString());
        Certificates.WriteSecret(EnvFile, env);

        if (StartAi(mode)) return AiTest();
        if (mode == AiAccel.Cpu) return false;
        Ui.Warn($"La tarjeta ({mode}) no funcionó; sigo con el procesador. Para reintentar: tecnm ai install --gpu {mode}.");
        return StartAi(AiAccel.Cpu) && AiTest();
    }

    /// <summary>Guarda IA_GPU, levanta el contenedor y espera; con tarjeta, exige que llama.cpp haya cargado capas en ella.</summary>
    private bool StartAi(string mode)
    {
        Certificates.WriteSecret(EnvFile, ProdEnv.Set(File.ReadAllText(EnvFile), "IA_GPU", mode));
        Ui.Info($"Aceleración: {AccelName(mode)}");
        if (Shell.Stream("docker", Compose("up", "-d", "ia")) != 0) return false;

        Ui.Progress("Cargando el modelo");
        var healthy = false;
        for (var i = 0; i < 90 && !(healthy = AiHealthy()); i++)
        {
            Ui.Progress(".");
            Thread.Sleep(2000);
        }
        Ui.Progress("\n");
        if (!healthy)
        {
            Ui.Error("El modelo no respondió en 3 minutos. Revisa: docker compose logs ia");
            return false;
        }
        if (mode != AiAccel.Cpu)
        {
            var layers = AiGpuLayers();
            if (layers == 0) { Ui.Error("llama.cpp arrancó, pero no cargó nada en la tarjeta (corre en procesador)."); return false; }
            Ui.Ok($"{layers} capas del modelo en la tarjeta.");
        }
        Ui.Ok("Asistente IA listo (solo la API lo ve, por la red interna).");
        return true;
    }

    private static long? NvidiaVramMiB()
    {
        var (code, output) = Shell.Capture("nvidia-smi", ["--query-gpu=memory.total", "--format=csv,noheader,nounits"]);
        return code == 0 ? AiAccel.ParseVram(output) : null;
    }

    private int AiGpuLayers() => AiAccel.OffloadedLayers(Shell.Capture("docker", Compose("logs", "ia")).Output);

    private static string AccelName(string mode) => mode switch
    {
        AiAccel.Nvidia => "tarjeta NVIDIA (CUDA)",
        AiAccel.Vulkan => "tarjeta AMD/Intel (Vulkan)",
        _ => "procesador",
    };

    /// <summary>Descarga a .part y solo la renombra si tamaño y SHA256 coinciden.</summary>
    private bool Download(AiModel.ModelFile file)
    {
        var part = PathOf(file) + ".part";
        Ui.Info($"Descargando {file.Url}");
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var response = client.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            using var source = response.Content.ReadAsStream();
            using (var target = File.Create(part))
            {
                // shortcut: sin reanudar descargas cortadas; si se corta, se vuelve a empezar.
                var buffer = new byte[1 << 20];
                long done = 0;
                var lastPercent = -1;
                int read;
                while ((read = source.Read(buffer)) > 0)
                {
                    target.Write(buffer, 0, read);
                    done += read;
                    var percent = (int)(done * 100 / file.Size);
                    if (percent != lastPercent) { Ui.Progress($"\r  {percent}% ({done / 1_048_576} MB)"); lastPercent = percent; }
                }
            }
            Ui.Progress("\n");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Ui.Progress("\n");
            File.Delete(part);
            Ui.Error($"Falló la descarga: {ex.Message}. Sin internet: tecnm ai install --from <carpeta con los .gguf>.");
            return false;
        }
        return Accept(part, file);
    }

    private bool CopyFrom(string folder, AiModel.ModelFile file)
    {
        var source = Path.Combine(folder, file.FileName);
        if (!File.Exists(source)) { Ui.Error($"No existe {source}."); return false; }
        var part = PathOf(file) + ".part";
        Ui.Info($"Copiando {file.FileName}…");
        File.Copy(source, part, overwrite: true);
        return Accept(part, file);
    }

    private bool Accept(string part, AiModel.ModelFile file)
    {
        Ui.Info("Comprobando la huella digital (SHA256)…");
        if (!AiModel.Matches(part, file))
        {
            File.Delete(part);
            Ui.Error($"El archivo no es {file.FileName} (tamaño o SHA256 distinto): dañado o alterado. Se borró.");
            return false;
        }
        File.Move(part, PathOf(file), overwrite: true);
        Ui.Ok($"Verificado: database/models/{file.FileName}");
        return true;
    }

    private bool AiHealthy() =>
        Shell.Capture("docker", Compose("exec", "-T", "ia", "curl", "-fs", "http://localhost:8080/health")).Code == 0;

    public AiStatusInfo AiStatus()
    {
        Ui.Title("Asistente IA");
        RequireInstalled();
        var enabled = AiModel.Enabled(Env);
        var healthy = enabled && AiHealthy();
        Ui.Info(enabled ? (healthy ? "IA: ● lista" : "IA: ○ sin responder") : "IA: apagada");
        var gpu = Env.GetValueOrDefault("IA_GPU", AiAccel.Cpu);
        int? layers = healthy && gpu != AiAccel.Cpu ? AiGpuLayers() : null;
        Ui.Info($"Aceleración: {AccelName(gpu)}{(layers is { } l ? $" ({l} capas en la tarjeta)" : "")}");
        var files = AiModel.Files.Select(f => new AiFileInfo(f.FileName, AiModel.Matches(PathOf(f), f))).ToList();
        foreach (var file in files)
            Ui.Info(file.Verified ? $"{file.Name}: huella correcta" : $"{file.Name}: no descargado o dañado");
        string? usage = null;
        var (code, id) = Shell.Capture("docker", Compose("ps", "-q", "ia"));
        if (code == 0 && id.Trim().Length > 0)
        {
            usage = Shell.Capture("docker", ["stats", "--no-stream", "--format", "Memoria: {{.MemUsage}}   CPU: {{.CPUPerc}}", id.Trim()]).Output.Trim();
            Ui.Info(usage);
        }
        return new AiStatusInfo(enabled, healthy, files, usage, gpu, layers);
    }

    /// <summary>Prueba real: empareja encabezados de ejemplo con las columnas de la plantilla y mide la velocidad.</summary>
    public bool AiTest()
    {
        Ui.Title("Prueba del modelo");
        RequireInstalled();
        if (!AiHealthy()) { Ui.Error("El asistente IA no responde. Instálalo o enciéndelo: tecnm ai install."); return false; }

        string[] columns = ["codigo_inventario", "tipo", "fabricante", "modelo", "serie", "hostname", "ubicacion", "notas", "ninguna"];
        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["No. Inventario"] = new { type = "string", @enum = columns },
                ["S/N"] = new { type = "string", @enum = columns },
                ["Equipo"] = new { type = "string", @enum = columns },
                ["Marca"] = new { type = "string", @enum = columns },
                ["Fecha compra"] = new { type = "string", @enum = columns },
            },
            required = new[] { "No. Inventario", "S/N", "Equipo", "Marca", "Fecha compra" },
        };
        var body = JsonSerializer.Serialize(new
        {
            messages = new object[]
            {
                // Sin la descripción de cada columna el modelo responde "notas" a todo (probado).
                new { role = "system", content = """
                    Eres un asistente que empareja encabezados de un Excel de inventario de equipos de cómputo con las columnas de una plantilla.
                    Columnas de la plantilla:
                    - codigo_inventario: número o clave de inventario del activo
                    - tipo: tipo de equipo (laptop, impresora, PC, monitor)
                    - fabricante: marca o fabricante
                    - modelo: modelo del equipo
                    - serie: número de serie (S/N)
                    - hostname: nombre del equipo en la red
                    - ubicacion: lugar, aula, laboratorio o edificio
                    - notas: observaciones o comentarios
                    - ninguna: el encabezado no corresponde a ninguna columna (por ejemplo fechas o precios)
                    Cada columna de la plantilla se usa a lo más una vez.
                    """ },
                new { role = "user", content = "Encabezados: No. Inventario, S/N, Equipo, Marca, Fecha compra" },
            },
            response_format = new { type = "json_schema", json_schema = new { schema } },
            chat_template_kwargs = new { enable_thinking = false },
            temperature = 0,
        });

        var watch = Stopwatch.StartNew();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var (code, output) = Shell.Capture("docker", Compose("exec", "-T", "ia", "curl", "-fs", "-X", "POST",
            "http://localhost:8080/v1/chat/completions", "-H", "Content-Type: application/json", "--data-binary", "@-"), input);
        watch.Stop();
        if (code != 0) { Ui.Error($"El modelo no contestó: {output.Trim()}"); return false; }

        try
        {
            using var json = JsonDocument.Parse(output);
            Ui.Info(json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "");
            var speed = json.RootElement.TryGetProperty("timings", out var t) && t.TryGetProperty("predicted_per_second", out var s)
                ? $", {s.GetDouble():0.0} tokens/s" : "";
            Ui.Ok($"Respondió en {watch.Elapsed.TotalSeconds:0.0} s{speed}.");
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Ui.Error($"Respuesta inesperada: {output.Trim()}");
            return false;
        }
    }

    public bool AiDisable()
    {
        Ui.Title("Apagar asistente IA");
        RequireInstalled();
        if (Shell.Stream("docker", Compose("rm", "-sf", "ia")) != 0) { Ui.Error("docker no pudo quitar el contenedor ia."); return false; }
        Certificates.WriteSecret(EnvFile, ProdEnv.Set(File.ReadAllText(EnvFile), "COMPOSE_PROFILES", ""));
        Ui.Ok("Apagado. El modelo sigue en database/models/; tecnm ai install lo vuelve a encender sin descargar.");
        return true;
    }
}

internal sealed record AiFileInfo(string Name, bool Verified);

internal sealed record AiStatusInfo(bool Enabled, bool Healthy, IReadOnlyList<AiFileInfo> Files, string? Usage, string Gpu, int? GpuLayers);
