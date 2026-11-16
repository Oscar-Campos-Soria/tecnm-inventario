using System.Text.Json;

namespace TecnmCli;

/// <summary>Resultado de un comando directo: si salió bien y los datos para --json.</summary>
internal sealed record CommandResult(bool Ok, object? Data = null, string FailCode = "OPERATION_FAILED");

/// <summary>
/// Modo directo: "tecnm status", "tecnm backup list --json"… Nunca pregunta nada (salvo --password-stdin escrito a mano)
/// y llama a las mismas operaciones de Deployment que el menú. Con --json escribe un solo objeto en stdout.
/// </summary>
internal static class Commands
{
    public static readonly (string Usage, string Description)[] Help =
    [
        ("menu", "Menú interactivo (también: tecnm sin argumentos)"),
        ("status", "Contenedores, salud, URL, certificado, último respaldo e IA (sale con 1 si no está en línea)"),
        ("start | stop | restart", "Iniciar, detener o reiniciar el sistema"),
        ("logs [--tail N]", "Últimas líneas de api y web (80 por omisión)"),
        ("install [--host IP]", "Instalar por primera vez (host: la IP detectada si no se indica)"),
        ("update --yes", "Respaldar, reconstruir y reiniciar con el código actual"),
        ("backup", "Respaldar ahora (base de datos y documentos)"),
        ("backup list", "Respaldos disponibles"),
        ("restore --backup FECHA --yes", "Reemplazar todos los datos por un respaldo (saca uno de seguridad antes)"),
        ("admin --user U --email E [--first N --last A] --password-stdin", "Crear o reactivar un administrador"),
        ("cert [status] | cert renew --yes | cert help", "Certificado HTTPS"),
        ("schedule-backup", "Programar el respaldo diario a las 02:00"),
        ("sandbox", "Modo desarrollo en este equipo (Ctrl+C para detener)"),
        ("ai status | ai install [--from CARPETA] | ai test | ai disable", "Asistente IA local"),
        ("help", "Esta ayuda"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Run(string[] argv)
    {
        Ui.Json = argv.Contains("--json");
        var name = string.Join(' ', argv.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)));
        try
        {
            var args = CliArgs.Parse(argv);
            name = string.Join(' ', args.Words);
            if (args.Command == "help" || args.Has("help"))
            {
                PrintHelp();
                return Finish(name, ExitCodes.Ok, Help.Select(h => new { usage = $"tecnm {h.Usage}", description = h.Description }), null);
            }

            var root = Deployment.FindRoot() ?? throw new CliException(ExitCodes.NotReady, "ROOT_NOT_FOUND",
                "No encuentro database/docker-compose.prod.yml.", "Ejecuta tecnm dentro del repositorio o define TECNM_ROOT.");
            var deployment = new Deployment(root);

            if (args.Command == "menu")
            {
                if (Ui.Json) throw CliException.Usage("El menú es para personas: no admite --json.");
                NoMoreWords(args, 1);
                return Menu.Run(deployment);
            }

            var result = Dispatch(args, deployment);
            return Finish(name, result.Ok ? ExitCodes.Ok : ExitCodes.Failed, result.Data,
                result.Ok ? null : new CliError(result.FailCode, Ui.LastError ?? "La operación falló.", null));
        }
        catch (CliException ex)
        {
            if (!Ui.Json)
            {
                Ui.Error(ex.Message);
                if (ex.Hint is not null) Ui.Info($"  → {ex.Hint}");
            }
            return Finish(name, ex.ExitCode, null, new CliError(ex.Code, ex.Message, ex.Hint));
        }
    }

    private static CommandResult Dispatch(CliArgs args, Deployment d)
    {
        switch (args.Command)
        {
            case "status":
                NoMoreWords(args, 1);
                var status = d.GetStatus();
                if (!Ui.Json) d.Status(status);
                return new CommandResult(status.Healthy, status, status.DockerAvailable ? "UNHEALTHY" : "DOCKER_UNAVAILABLE");
            case "start": NoMoreWords(args, 1); return new CommandResult(d.Start());
            case "stop": NoMoreWords(args, 1); return new CommandResult(d.Stop());
            case "restart": NoMoreWords(args, 1); return new CommandResult(d.Restart());
            case "logs":
                NoMoreWords(args, 1);
                var tail = args.Get("tail") is { } t ? (int.TryParse(t, out var n) && n is > 0 and <= 10_000 ? n
                    : throw CliException.Usage("--tail: un número de 1 a 10000.")) : 80;
                return new CommandResult(d.Logs(tail));
            case "install": NoMoreWords(args, 1); return new CommandResult(d.Install(args.Get("host")));
            case "update":
                NoMoreWords(args, 1);
                RequireYes(args, "Actualizar respalda, reconstruye las imágenes y reinicia el sistema.");
                return new CommandResult(d.Update());
            case "backup" when args.Sub == "list":
                NoMoreWords(args, 2);
                var stamps = d.BackupStamps();
                if (stamps.Count == 0) Ui.Warn("No hay respaldos en database/backups/.");
                foreach (var stamp in stamps) Ui.Info(stamp);
                return new CommandResult(true, stamps);
            case "backup": NoMoreWords(args, 1); return new CommandResult(d.Backup());
            case "restore":
                NoMoreWords(args, 1);
                var chosen = args.Require("backup", "fecha del respaldo; consúltalas con tecnm backup list");
                RequireYes(args, $"Restaurar reemplaza TODOS los datos actuales por los del respaldo {chosen}.");
                return new CommandResult(d.Restore(chosen));
            case "admin":
                NoMoreWords(args, 1);
                var user = args.Require("user", "usuario del administrador");
                var email = args.Require("email", "correo del administrador");
                var first = args.Get("first") ?? "Administrador";
                var last = args.Get("last") ?? "del sistema";
                if (!args.Has("password-stdin"))
                    throw CliException.Usage("Falta --password-stdin: la contraseña se lee de la entrada, nunca como argumento.");
                var password = ReadPassword();
                return new CommandResult(d.CreateAdmin(user, email, first, last, password), new { username = user });
            case "cert":
                NoMoreWords(args, 2);
                switch (args.Sub ?? "status")
                {
                    case "status":
                        var cert = d.CertificateStatus();
                        return new CommandResult(cert is not null, cert, "NO_CERTIFICATE");
                    case "renew":
                        RequireYes(args, "Regenerar el certificado reinicia la pantalla web unos segundos.");
                        return new CommandResult(d.RenewCertificate());
                    case "help":
                        d.CertificateHelp();
                        d.CustomCertificateHelp();
                        return new CommandResult(true);
                }
                throw CliException.Usage($"cert {args.Sub}: usa cert status, cert renew --yes o cert help.");
            case "schedule-backup": NoMoreWords(args, 1); return new CommandResult(d.ScheduleBackup());
            case "sandbox": NoMoreWords(args, 1); return new CommandResult(Sandbox.Run(d.Root));
            case "ai":
                NoMoreWords(args, 2);
                switch (args.Sub)
                {
                    case "status": var ai = d.AiStatus(); return new CommandResult(ai.Healthy || !ai.Enabled, ai, "AI_UNHEALTHY");
                    case "install": return new CommandResult(d.AiInstall(args.Get("from")));
                    case "test": return new CommandResult(d.AiTest());
                    case "disable": return new CommandResult(d.AiDisable());
                }
                throw CliException.Usage("ai: usa ai status, ai install [--from CARPETA], ai test o ai disable.");
        }
        throw CliException.Usage($"Comando desconocido: {args.Command}.");
    }

    private static void NoMoreWords(CliArgs args, int allowed)
    {
        if (args.Words.Count > allowed)
            throw CliException.Usage($"Sobra «{args.Words[allowed]}» en «tecnm {string.Join(' ', args.Words)}».");
    }

    private static void RequireYes(CliArgs args, string what)
    {
        if (!args.Has("yes")) throw CliException.NeedsYes(what);
    }

    /// <summary>Por tubería (scripts, agentes): la primera línea. A mano: dos veces, sin mostrarla.</summary>
    private static string ReadPassword()
    {
        if (Console.IsInputRedirected) return (Console.In.ReadLine() ?? "").TrimEnd('\r');
        if (Ui.Json) throw CliException.Usage("Con --json pasa la contraseña por tubería: echo … | tecnm admin … --password-stdin --json.");
        var password = Ui.Secret("Contraseña (mínimo 12 caracteres, no se muestra)");
        if (Ui.Secret("Repite la contraseña") != password) throw CliException.Usage("Las contraseñas no coinciden.", null);
        return password;
    }

    private static void PrintHelp()
    {
        if (Ui.Json) return;
        Ui.Info("tecnm: administración del sistema de inventario (producción).\n");
        foreach (var (usage, description) in Help) Ui.Info($"  tecnm {usage}\n      {description}");
        Ui.Info("\nOpciones generales: --json (un objeto JSON en stdout) · --yes (confirma operaciones que reemplazan o reinician)");
        Ui.Info("Códigos de salida: 0 bien · 1 falló · 2 uso incorrecto · 3 falta --yes · 4 falta un requisito (Docker, instalación)");
        Ui.Info("Los nombres de antes siguen sirviendo: respaldar, estado, iniciar, detener, ia instalar --desde …");
    }

    private static int Finish(string command, int exitCode, object? data, CliError? error)
    {
        if (Ui.Json)
            Console.WriteLine(JsonSerializer.Serialize(new CliOutput(command, exitCode == ExitCodes.Ok, exitCode, data, Ui.Messages, error), JsonOptions));
        return exitCode;
    }
}

internal sealed record CliError(string Code, string Message, string? Hint);

internal sealed record CliOutput(string Command, bool Ok, int ExitCode, object? Data, IReadOnlyList<CliMessage> Messages, CliError? Error);
