using System.Diagnostics;

namespace TecnmCli;

/// <summary>
/// Modo desarrollo en este equipo: BD de desarrollo (database/docker-compose.yml),
/// API con dotnet run (localhost:5112) y pantallas con npm run dev (localhost:5173).
/// No toca producción. Requiere .NET SDK, Node y database/.env.
/// </summary>
internal static class Sandbox
{
    private const string ApiHealth = "http://localhost:5112/api/health";
    private const string WebUrl = "http://localhost:5173";

    public static void Run(string root)
    {
        Ui.Title("Sandbox (modo desarrollo)");
        if (!File.Exists(Path.Combine(root, "database", ".env")))
        {
            Ui.Error("Falta database/.env (credenciales de la BD de desarrollo).");
            return;
        }

        if (Shell.Stream("docker", ["compose", "-f", Path.Combine(root, "database", "docker-compose.yml"), "up", "-d"]) != 0)
            return;

        var logDir = Path.Combine(Path.GetTempPath(), "tecnm-sandbox");
        Directory.CreateDirectory(logDir);
        var started = new List<Process>();
        try
        {
            if (Shell.IsHealthy(ApiHealth, certDir: ""))
                Ui.Info("La API de desarrollo ya estaba corriendo: se reutiliza.");
            else
                started.Add(Launch("dotnet", ["run", "--project", Path.Combine(root, "backend", "TecnmInventario.Api.csproj")],
                    root, Path.Combine(logDir, "api.log")));

            if (Responds(WebUrl))
                Ui.Info("Las pantallas de desarrollo ya estaban corriendo: se reutilizan.");
            else
            {
                var frontend = Path.Combine(root, "frontend");
                if (!Directory.Exists(Path.Combine(frontend, "node_modules")))
                    Shell.Stream(Npm, NpmArgs("install"), frontend);
                // vite directo con node (no npm run dev): un solo proceso que se puede cerrar.
                // Con npm en Windows la cadena cmd -> npm -> cmd -> node deja a vite huérfano.
                started.Add(Launch("node", [Path.Combine("node_modules", "vite", "bin", "vite.js")], frontend, Path.Combine(logDir, "web.log")));
            }

            Console.Write("Esperando a la API");
            for (var i = 0; i < 90 && !Shell.IsHealthy(ApiHealth, ""); i++)
            {
                Console.Write('.');
                Thread.Sleep(2000);
            }
            Console.WriteLine();
            if (!Shell.IsHealthy(ApiHealth, ""))
            {
                Ui.Error($"La API no respondió. Revisa {Path.Combine(logDir, "api.log")}.");
                return;
            }

            Ui.Ok($"Sandbox en {WebUrl} (API en http://localhost:5112). Logs en {logDir}.");
            if (!Console.IsInputRedirected) Shell.OpenBrowser(WebUrl);
            Console.Write("Presiona Enter para detener el sandbox…");
            Console.ReadLine();
        }
        finally
        {
            foreach (var process in started)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                process.Dispose();
            }
            if (started.Count > 0) Ui.Ok("Procesos del sandbox detenidos. La BD de desarrollo sigue en Docker.");
        }
    }

    // npm install: en Windows npm es un .cmd y se lanza a través de cmd.exe.
    private static string Npm => OperatingSystem.IsWindows() ? "cmd.exe" : "npm";
    private static string[] NpmArgs(params string[] args) => OperatingSystem.IsWindows() ? ["/c", "npm", .. args] : args;

    private static Process Launch(string file, IEnumerable<string> args, string cwd, string logFile)
    {
        var psi = new ProcessStartInfo(file)
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        var process = Process.Start(psi)!;
        var log = new StreamWriter(logFile, append: false) { AutoFlush = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine(e.Data); };
        process.Exited += (_, _) => log.Dispose();
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static bool Responds(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try { return client.GetAsync(url).GetAwaiter().GetResult().IsSuccessStatusCode; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
    }
}
