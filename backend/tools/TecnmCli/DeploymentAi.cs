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

    public void AiMenu()
    {
        Ui.Title("Asistente IA (carga masiva)");
        Ui.Info(AiStatusLine());
        Ui.Info("\n  1) Instalar / encender (descarga el modelo ~1.9 GB)");
        Ui.Info("  2) Instalar desde una carpeta con los .gguf (servidor sin internet)");
        Ui.Info("  3) Estado y memoria");
        Ui.Info("  4) Probar");
        Ui.Info("  5) Apagar (conserva el modelo)");
        Ui.Info("  0) Volver");
        switch (Ui.Ask("Opción", "0"))
        {
            case "1": AiInstall(null); break;
            case "2": AiInstall(Ui.Ask("Carpeta con " + string.Join(" y ", AiModel.Files.Select(f => f.FileName)))); break;
            case "3": AiStatus(); break;
            case "4": AiTest(); break;
            case "5": AiDisable(); break;
        }
    }

    public bool AiInstall(string? from)
    {
        Ui.Title("Instalar asistente IA");
        if (!Ready()) return false;
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

        Certificates.WriteSecret(EnvFile, ProdEnv.Set(File.ReadAllText(EnvFile), "COMPOSE_PROFILES", AiModel.Profile));
        if (Shell.Stream("docker", Compose("up", "-d", "ia")) != 0) return false;

        Console.Write("Cargando el modelo");
        for (var i = 0; i < 90; i++)
        {
            if (AiHealthy())
            {
                Console.WriteLine();
                Ui.Ok("Asistente IA listo (solo la API lo ve, por la red interna).");
                return AiTest();
            }
            Console.Write('.');
            Thread.Sleep(2000);
        }
        Console.WriteLine();
        Ui.Error("El modelo no respondió en 3 minutos. Revisa: docker compose logs ia");
        return false;
    }

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
                    if (percent != lastPercent) { Console.Write($"\r  {percent}% ({done / 1_048_576} MB)"); lastPercent = percent; }
                }
            }
            Console.WriteLine();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Console.WriteLine();
            File.Delete(part);
            Ui.Error($"Falló la descarga: {ex.Message}. Sin internet usa la opción 2 (carpeta con los .gguf).");
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

    public void AiStatus()
    {
        Ui.Title("Asistente IA");
        Ui.Info(AiStatusLine());
        foreach (var file in AiModel.Files)
            Ui.Info(AiModel.Matches(PathOf(file), file) ? $"{file.FileName}: huella correcta" : $"{file.FileName}: no descargado o dañado");
        var (code, id) = Shell.Capture("docker", Compose("ps", "-q", "ia"));
        if (code == 0 && id.Trim().Length > 0)
            Shell.Stream("docker", ["stats", "--no-stream", "--format", "Memoria: {{.MemUsage}}   CPU: {{.CPUPerc}}", id.Trim()]);
    }

    /// <summary>Prueba real: empareja encabezados de ejemplo con las columnas de la plantilla y mide la velocidad.</summary>
    public bool AiTest()
    {
        Ui.Title("Prueba del modelo");
        if (!AiHealthy()) { Ui.Error("El asistente IA no responde. Instálalo o enciéndelo (opción 1)."); return false; }

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

    public void AiDisable()
    {
        Ui.Title("Apagar asistente IA");
        if (!File.Exists(EnvFile)) { Ui.Error("No existe database/.env.prod."); return; }
        Shell.Stream("docker", Compose("rm", "-sf", "ia"));
        Certificates.WriteSecret(EnvFile, ProdEnv.Set(File.ReadAllText(EnvFile), "COMPOSE_PROFILES", ""));
        Ui.Ok("Apagado. El modelo sigue en database/models/; la opción 1 lo vuelve a encender sin descargar.");
    }
}
