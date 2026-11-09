using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;

namespace TecnmCli;

/// <summary>Procesos externos (docker, schtasks, crontab). Argumentos por lista: sin interpretar por un shell.</summary>
internal static class Shell
{
    /// <summary>Muestra la salida en pantalla.</summary>
    public static int Stream(string file, IEnumerable<string> args, string? cwd = null)
        => Exec(file, args, cwd, input: null, outputFile: null, capture: false).Code;

    /// <summary>Devuelve la salida (stdout + stderr) sin mostrarla.</summary>
    public static (int Code, string Output) Capture(string file, IEnumerable<string> args, Stream? input = null)
        => Exec(file, args, null, input, null, capture: true);

    /// <summary>Manda un archivo o texto por stdin; la salida se muestra.</summary>
    public static int PipeIn(string file, IEnumerable<string> args, Stream input)
        => Exec(file, args, null, input, null, capture: false).Code;

    /// <summary>Guarda stdout (binario) en un archivo; stderr se muestra.</summary>
    public static int PipeOut(string file, IEnumerable<string> args, string outputFile)
        => Exec(file, args, null, null, outputFile, capture: false).Code;

    private static (int Code, string Output) Exec(
        string file, IEnumerable<string> args, string? cwd, Stream? input, string? outputFile, bool capture)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = outputFile is not null || capture,
            RedirectStandardError = capture,
        };
        if (cwd is not null) psi.WorkingDirectory = cwd;
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (Win32Exception)
        {
            return (-1, $"No se encontró el programa '{file}'.");
        }

        using (process)
        {
            var feed = input is null ? Task.CompletedTask : Task.Run(async () =>
            {
                await input.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            });

            var output = string.Empty;
            if (outputFile is not null)
            {
                using var file2 = File.Create(outputFile);
                process.StandardOutput.BaseStream.CopyTo(file2);
            }
            else if (capture)
            {
                var error = process.StandardError.ReadToEndAsync();
                output = process.StandardOutput.ReadToEnd() + error.GetAwaiter().GetResult();
            }

            feed.GetAwaiter().GetResult();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
    }

    /// <summary>GET a /api/health. Acepta el certificado si es válido o si lo firmó la CA propia.</summary>
    public static bool IsHealthy(string url, string certDir)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
                errors == SslPolicyErrors.None
                || (errors == SslPolicyErrors.RemoteCertificateChainErrors && cert is not null && Certificates.IsTrusted(cert, certDir)),
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            return client.GetAsync(url).GetAwaiter().GetResult().IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Primera IPv4 de una interfaz activa (sugerencia para PUBLIC_HOST).</summary>
    public static string? LocalIPv4() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Select(a => a.Address)
        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
        ?.ToString();

    /// <summary>Abre el navegador en el equipo local (en un servidor sin escritorio solo se muestra la dirección).</summary>
    public static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            else
                Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url))?.Dispose();
        }
        catch (Win32Exception)
        {
            // Sin navegador: la dirección ya se mostró en pantalla.
        }
    }
}
