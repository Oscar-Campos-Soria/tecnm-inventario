using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TecnmCli;

/// <summary>
/// Operaciones sobre el despliegue de producción (database/docker-compose.prod.yml).
/// Todo pasa por docker compose; la BD y la API no tienen puertos publicados.
/// Ninguna operación pregunta nada: el menú (Menu.cs) pide los datos y el modo directo (Commands.cs) los toma
/// de las opciones. Un requisito que falta corta con CliException; una operación que falla devuelve false.
/// </summary>
internal sealed partial class Deployment(string root)
{
    public string Root { get; } = root;
    private string ComposeFile => Path.Combine(Root, "database", "docker-compose.prod.yml");
    private string EnvFile => Path.Combine(Root, "database", ".env.prod");
    public string CertDir => Path.Combine(Root, "database", "certs");
    public string BackupDir => Path.Combine(Root, "database", "backups");

    /// <summary>Raíz del repo: TECNM_ROOT o la primera carpeta (subiendo) que tenga el compose de producción.</summary>
    public static string? FindRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TECNM_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return Path.GetFullPath(fromEnv);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "database", "docker-compose.prod.yml")))
                    return dir.FullName;
        }
        return null;
    }

    private string[] Compose(params string[] args) =>
        AiAccel.OverrideFile(Env.GetValueOrDefault("IA_GPU", AiAccel.Cpu)) is { } gpu
            ? ["compose", "-f", ComposeFile, "-f", Path.Combine(Root, "database", gpu), "--env-file", EnvFile, .. args]
            : ["compose", "-f", ComposeFile, "--env-file", EnvFile, .. args];

    private Dictionary<string, string> Env => File.Exists(EnvFile)
        ? ProdEnv.Parse(File.ReadAllText(EnvFile))
        : new Dictionary<string, string>();

    private string Host => Env.GetValueOrDefault("PUBLIC_HOST", "localhost");
    public string WebUrl => $"https://{Host}:6010";
    private string HealthUrl => $"https://{Host}:6011/api/health";
    public bool IsInstalled => File.Exists(EnvFile);

    private string[] Psql(params string[] extra)
    {
        var env = Env;
        return Compose(["exec", "-T", "db", "psql", "-v", "ON_ERROR_STOP=1", "-q",
            "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"], .. extra]);
    }

    // ---------------------------------------------------------------- estado

    public bool IsHealthy() => File.Exists(EnvFile) && Shell.IsHealthy(HealthUrl, CertDir);

    public IReadOnlyList<string> BackupStamps() =>
        Directory.Exists(BackupDir) ? BackupFiles.Stamps(Directory.GetFiles(BackupDir).Select(Path.GetFileName)!) : [];

    /// <summary>Todo el estado como datos: lo usan el panel del menú, "tecnm status" y "tecnm status --json".</summary>
    public StatusInfo GetStatus(bool withContainers = true)
    {
        var docker = DockerAvailable();
        var installed = IsInstalled;
        var cert = Certificates.ReadServer(CertDir);
        var aiEnabled = installed && AiModel.Enabled(Env);
        return new StatusInfo(
            Installed: installed,
            DockerAvailable: docker,
            Healthy: installed && IsHealthy(),
            WebUrl: WebUrl,
            HealthUrl: HealthUrl,
            Containers: withContainers && installed && docker ? Containers() : [],
            CertificateDaysLeft: cert is null ? null : (cert.NotAfter - DateTime.Now).Days,
            LastBackup: BackupStamps().FirstOrDefault(),
            AiEnabled: aiEnabled,
            AiHealthy: aiEnabled && docker && AiHealthy());
    }

    /// <summary>docker compose ps en JSON: una línea por contenedor o un arreglo, según la versión de compose.</summary>
    private List<ContainerInfo> Containers()
    {
        var (code, output) = Shell.Capture("docker", Compose("ps", "--all", "--format", "json"));
        return code == 0 ? ParseContainers(output) : [];
    }

    public static List<ContainerInfo> ParseContainers(string output)
    {
        var text = output.Trim();
        var items = new List<JsonElement>();
        try
        {
            if (text.StartsWith('[')) items.AddRange(JsonDocument.Parse(text).RootElement.EnumerateArray());
            else items.AddRange(text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.TrimStart().StartsWith('{')).Select(l => JsonDocument.Parse(l).RootElement));
        }
        catch (JsonException)
        {
            return [];
        }
        static string Field(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "";
        return items.Select(e => new ContainerInfo(Field(e, "Service"), Field(e, "State"), Field(e, "Health"), Field(e, "Status"))).ToList();
    }

    public void Status(StatusInfo status)
    {
        Ui.Title("Contenedores");
        if (status.Containers.Count == 0) Ui.Warn(status.Installed ? "Sin contenedores (o Docker no responde)." : "No instalado: tecnm install.");
        foreach (var c in status.Containers)
            Ui.Info($"  {(c.State == "running" ? "●" : "○")} {c.Service,-6} {c.Status}");
        Ui.Title("Salud");
        if (!status.DockerAvailable) Ui.Error("Docker no responde.");
        if (status.Healthy) Ui.Ok($"API y base de datos responden ({HealthUrl})");
        else Ui.Error($"Sin respuesta en {HealthUrl}");
        Ui.Info($"Pantallas: {WebUrl}");
        Ui.Info($"Certificado: {(status.CertificateDaysLeft is { } d ? $"{d} días" : "no hay")}   Último respaldo: {status.LastBackup ?? "ninguno"}");
        Ui.Info($"Asistente IA: {(status.AiEnabled ? (status.AiHealthy ? "listo" : "sin responder") : "apagado")}");
    }

    public bool Logs(int tail = 80)
    {
        RequireInstalled();
        return Shell.Stream("docker", Compose("logs", "--tail", tail.ToString(CultureInfo.InvariantCulture), "api", "web")) == 0;
    }

    // ------------------------------------------------------------- servicios

    public bool Start()
    {
        Ready();
        return Shell.Stream("docker", Compose("up", "-d")) == 0 && WaitHealthy();
    }

    public bool Stop()
    {
        RequireInstalled();
        return Shell.Stream("docker", Compose("stop")) == 0;
    }

    public bool Restart()
    {
        Ready();
        return Shell.Stream("docker", Compose("restart")) == 0 && WaitHealthy();
    }

    private static bool DockerAvailable() => Shell.Capture("docker", ["info", "--format", "{{.ServerVersion}}"]).Code == 0;

    private static void RequireDocker()
    {
        if (!DockerAvailable())
            throw new CliException(ExitCodes.NotReady, "DOCKER_UNAVAILABLE", "Docker no responde.",
                "Instálalo o inícialo (Windows: Docker Desktop; Linux: usuario en el grupo docker o sudo).");
    }

    private void RequireInstalled()
    {
        RequireDocker();
        if (!File.Exists(EnvFile))
            throw new CliException(ExitCodes.NotReady, "NOT_INSTALLED", "No existe database/.env.prod: el sistema no está instalado.", "tecnm install");
    }

    /// <summary>Docker disponible y .env.prod válido; si no, corta con un error que dice qué falta.</summary>
    private void Ready()
    {
        RequireInstalled();
        var problems = ProdEnv.Problems(Env);
        if (problems.Count > 0)
            throw new CliException(ExitCodes.NotReady, "ENV_INVALID", string.Join(" ", problems), "Corrige database/.env.prod.");
    }

    private bool WaitHealthy()
    {
        Ui.Progress("Esperando a que la API responda");
        for (var i = 0; i < 60; i++)
        {
            if (IsHealthy())
            {
                Ui.Progress("\n");
                Ui.Ok($"En línea: {WebUrl}");
                return true;
            }
            Ui.Progress(".");
            Thread.Sleep(2000);
        }
        Ui.Progress("\n");
        Ui.Error("La API no respondió en 2 minutos. Revisa los logs: tecnm logs.");
        return false;
    }

    private bool WaitDatabase()
    {
        var env = Env;
        for (var i = 0; i < 60; i++)
        {
            if (Shell.Capture("docker", Compose("exec", "-T", "db", "pg_isready", "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"])).Code == 0)
                return true;
            Thread.Sleep(1000);
        }
        Ui.Error("La base de datos no arrancó.");
        return false;
    }

    // ------------------------------------------------------ instalar/actualizar

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$")]
    private static partial Regex HostPattern();

    public static string DefaultHost => Shell.LocalIPv4() ?? "localhost";

    /// <summary>Primera instalación o completar una a medias. host solo se usa si aún no existe .env.prod.</summary>
    public bool Install(string? host)
    {
        Ui.Title("Instalar");
        RequireDocker();
        if (!File.Exists(EnvFile))
        {
            host = string.IsNullOrWhiteSpace(host) ? DefaultHost : host.Trim();
            if (!HostPattern().IsMatch(host)) throw CliException.Usage($"«{host}» no es una IP o nombre válido (sin https:// ni puerto).", null);
            Directory.CreateDirectory(Path.GetDirectoryName(EnvFile)!);
            Certificates.WriteSecret(EnvFile, ProdEnv.Generate(host));
            Ui.Ok($"database/.env.prod creado para {host} con contraseñas aleatorias (no se muestran).");
        }
        else
        {
            Ui.Info("database/.env.prod ya existe: se conserva.");
        }
        Ready();

        if (Certificates.ReadServer(CertDir) is null)
        {
            Certificates.Issue(CertDir, Host, DateTimeOffset.Now);
            Ui.Ok($"Certificado HTTPS emitido para {Host} con la CA propia (database/certs/ca.crt).");
        }

        Ui.Title("Construyendo e iniciando contenedores");
        if (Shell.Stream("docker", Compose("up", "-d", "--build")) != 0 || !WaitDatabase()) return false;
        if (!ApplyScripts()) return false;
        // La API arrancó antes del esquema: se reinicia para que sus servicios en segundo plano lo vean.
        Shell.Stream("docker", Compose("restart", "api"));
        if (!WaitHealthy()) return false;

        Ui.Title("Listo");
        Ui.Info($"Pantallas: {WebUrl}");
        Ui.Info("Instala el certificado raíz en cada equipo para quitar la advertencia (opción C o tecnm cert help).");
        Ui.Info("Programa el respaldo diario con la opción P o tecnm schedule-backup.");
        return true;
    }

    /// <summary>Versión del código que se desplegaría (se muestra antes de confirmar).</summary>
    public string? PendingCommit()
    {
        var (gitCode, commit) = Shell.Capture("git", ["-C", Root, "log", "-1", "--format=%h %s"]);
        return gitCode == 0 ? commit.Trim() : null;
    }

    /// <summary>Respalda, reconstruye y reinicia. Quien llama ya confirmó (menú: pregunta; directo: --yes).</summary>
    public bool Update()
    {
        Ui.Title("Actualizar");
        Ready();
        if (PendingCommit() is { } commit) Ui.Info($"Código a desplegar: {commit}  (haz git pull antes si hace falta)");
        if (!Backup()) { Ui.Error("Sin respaldo no se actualiza."); return false; }

        if (Shell.Stream("docker", Compose("build")) != 0) { Ui.Error("Falló la construcción; producción sigue con la versión anterior."); return false; }
        if (Shell.Stream("docker", Compose("up", "-d")) != 0 || !WaitDatabase()) return false;
        if (!ApplyScripts()) return false;
        return WaitHealthy();
    }

    /// <summary>database.sql y scripts/ en el orden del README (son idempotentes).</summary>
    private bool ApplyScripts()
    {
        Ui.Title("Aplicando esquema y scripts");
        var order = SqlScripts.ProductionOrder(File.ReadAllText(Path.Combine(Root, "scripts", "README.md")));
        foreach (var script in order)
        {
            using var file = File.OpenRead(Path.Combine(Root, script));
            var (code, output) = Shell.Capture("docker", Psql(), file);
            if (code != 0)
            {
                Ui.Error($"{script} falló: {output.Trim()}");
                return false;
            }
            Ui.Ok(script);
        }
        return true;
    }

    // ------------------------------------------------------------- respaldos

    public bool Backup()
    {
        Ui.Title("Respaldo");
        Ready();
        Directory.CreateDirectory(BackupDir);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(BackupDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var env = Env;
        var stamp = BackupFiles.Stamp(DateTime.Now);
        var dbFile = Path.Combine(BackupDir, BackupFiles.DbFile(stamp));
        var code = Shell.PipeOut("docker", Compose("exec", "-T", "db", "pg_dump", "-Fc", "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"]), dbFile);
        if (code != 0 || new FileInfo(dbFile).Length == 0)
        {
            File.Delete(dbFile);
            Ui.Error("pg_dump falló: no se guardó respaldo.");
            return false;
        }
        Ui.Ok($"Base de datos: {Path.GetFileName(dbFile)} ({new FileInfo(dbFile).Length / 1024} KB)");

        var docsFile = Path.Combine(BackupDir, BackupFiles.DocsFile(stamp));
        if (Shell.PipeOut("docker", Compose("exec", "-T", "api", "tar", "czf", "-", "-C", "/data", "documents"), docsFile) == 0)
            Ui.Ok($"Documentos: {Path.GetFileName(docsFile)} ({new FileInfo(docsFile).Length / 1024} KB)");
        else
        {
            File.Delete(docsFile);
            Ui.Warn("No se pudieron respaldar los documentos (¿la API está detenida?). La base de datos sí quedó.");
        }

        foreach (var old in BackupFiles.ToDelete(Directory.GetFiles(BackupDir).Select(Path.GetFileName)!))
            File.Delete(Path.Combine(BackupDir, old));
        Ui.Info($"Se conservan los últimos {BackupFiles.Keep} respaldos en database/backups/. Cópialos también fuera del servidor.");
        return true;
    }

    /// <summary>Reemplaza todos los datos por los del respaldo. Quien llama ya confirmó (menú: RESTAURAR; directo: --yes).</summary>
    public bool Restore(string stamp)
    {
        Ui.Title("Restaurar respaldo");
        Ready();
        if (!BackupStamps().Contains(stamp))
            throw new CliException(ExitCodes.Usage, "BACKUP_NOT_FOUND", $"No existe el respaldo «{stamp}» en database/backups/.", "tecnm backup list");
        if (!Backup()) { Ui.Error("No se pudo sacar el respaldo de seguridad: no se restaura."); return false; }

        var env = Env;
        Shell.Stream("docker", Compose("stop", "api"));
        int code;
        using (var dump = File.OpenRead(Path.Combine(BackupDir, BackupFiles.DbFile(stamp))))
            code = Shell.PipeIn("docker", Compose("exec", "-T", "db", "pg_restore", "--clean", "--if-exists", "--no-owner",
                "--single-transaction", "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"]), dump);
        Shell.Stream("docker", Compose("start", "api"));
        if (code != 0) { Ui.Error("pg_restore falló; la transacción se revirtió y los datos siguen como estaban."); return false; }
        Ui.Ok("Base de datos restaurada.");

        var docs = Path.Combine(BackupDir, BackupFiles.DocsFile(stamp));
        if (File.Exists(docs) && WaitHealthy())
        {
            using var tar = File.OpenRead(docs);
            var docsCode = Shell.PipeIn("docker", Compose("exec", "-T", "api", "sh", "-c",
                "find /data/documents -mindepth 1 -delete && tar xzf - -C /data"), tar);
            if (docsCode != 0) { Ui.Error("Falló la restauración de documentos."); return false; }
            Ui.Ok("Documentos restaurados.");
        }
        return true;
    }

    public bool ScheduleBackup()
    {
        Ui.Title("Programar respaldo diario (02:00)");
        var exe = Environment.ProcessPath;
        if (exe is null || Path.GetFileNameWithoutExtension(exe) != "tecnm")
            throw new CliException(ExitCodes.NotReady, "CLI_NOT_PUBLISHED",
                "Con 'dotnet run' no se puede programar: usa el tecnm publicado.", "database/instalar.ps1 o database/instalar.sh");
        if (OperatingSystem.IsWindows())
        {
            var code = Shell.Stream("schtasks", ["/Create", "/F", "/SC", "DAILY", "/ST", "02:00", "/TN", "TecNM Inventario respaldo", "/TR", $"\"{exe}\" respaldar"]);
            if (code != 0) { Ui.Error("schtasks no pudo crear la tarea."); return false; }
            Ui.Ok("Tarea 'TecNM Inventario respaldo' creada en el Programador de tareas.");
            return true;
        }

        var line = $"0 2 * * * TECNM_ROOT=\"{Root}\" \"{exe}\" respaldar >> \"{Path.Combine(BackupDir, "respaldo.log")}\" 2>&1";
        var (_, current) = Shell.Capture("crontab", ["-l"]);
        if (current.Contains(" respaldar", StringComparison.Ordinal)) { Ui.Ok("Ya estaba programado en crontab."); return true; }
        var lines = current.StartsWith("no crontab", StringComparison.OrdinalIgnoreCase) ? "" : current.TrimEnd() + "\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(lines + line + "\n"));
        if (Shell.PipeIn("crontab", ["-"], input) != 0) { Ui.Error("No se pudo escribir el crontab."); return false; }
        Ui.Ok("Respaldo diario agregado al crontab de este usuario.");
        return true;
    }

    // ---------------------------------------------------------- administrador

    [GeneratedRegex("^[A-Za-z0-9._-]{3,60}$")]
    private static partial Regex UsernamePattern();

    /// <summary>Mismas reglas para el menú y para tecnm admin: un dato inválido corta con código 2 y dice cuál.</summary>
    public static void ValidateAdmin(string username, string email, string first, string last, string password)
    {
        if (!UsernamePattern().IsMatch(username)) throw CliException.Usage("Usuario: 3 a 60 letras, números, punto, guion o guion bajo.", null);
        if (!email.Contains('@') || email.Length > 254) throw CliException.Usage("Correo no válido.", null);
        if (first.Length is 0 or > 100 || last.Length is 0 or > 100) throw CliException.Usage("Nombre y apellidos: 1 a 100 caracteres.", null);
        if (password.Length < 12) throw CliException.Usage("La contraseña debe tener al menos 12 caracteres.", null);
    }

    public bool CreateAdmin(string username, string email, string first, string last, string password)
    {
        Ui.Title("Crear o reactivar administrador");
        ValidateAdmin(username, email, first, last, password);
        Ready();

        // Valores por variables de psql (:'x'): sin concatenar SQL con lo que escribió el usuario.
        const string sql = """
            SET search_path TO inventario, public;
            INSERT INTO app_users (username, email, password_hash, first_name, last_name, role_id, status, version)
            SELECT :'u', :'e', :'h', :'fn', :'ln', role_id, 'activo', 1 FROM roles WHERE code = 'ADMIN'
            ON CONFLICT (username) DO UPDATE
              SET password_hash = EXCLUDED.password_hash, role_id = EXCLUDED.role_id,
                  status = 'activo', is_deleted = false, updated_at = now();
            """;
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(sql));
        var (code, output) = Shell.Capture("docker",
            Psql("-v", $"u={username}", "-v", $"e={email}", "-v", $"h={hash}", "-v", $"fn={first}", "-v", $"ln={last}"), input);
        if (code == 0) Ui.Ok($"Administrador '{username}' listo. Entra en {WebUrl}.");
        else Ui.Error(output.Contains("app_users_email_key") ? "Ese correo ya lo usa otro usuario." : output.Trim());
        return code == 0;
    }

    // ------------------------------------------------------------ certificado

    public CertificateInfo? CertificateStatus()
    {
        Ui.Title("Certificado HTTPS");
        var cert = Certificates.ReadServer(CertDir);
        if (cert is null) { Ui.Warn("No hay certificado en database/certs/."); return null; }
        var info = new CertificateInfo(
            cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false),
            cert.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (cert.NotAfter - DateTime.Now).Days);
        Ui.Info($"Emitido para: {info.Subject}");
        Ui.Info($"Vence: {info.NotAfter} ({info.DaysLeft} días)");
        if (info.DaysLeft < 30) Ui.Warn("Vence en menos de 30 días: regenéralo (tecnm cert renew --yes).");
        return info;
    }

    /// <summary>Regenera el certificado del servidor con la misma CA (los equipos no reinstalan nada) y reinicia web.</summary>
    public bool RenewCertificate()
    {
        RequireInstalled();
        Certificates.Issue(CertDir, Host, DateTimeOffset.Now);
        Ui.Ok($"Certificado emitido para {Host}.");
        return Shell.Stream("docker", Compose("restart", "web")) == 0;
    }

    public void CertificateHelp()
    {
        Ui.Title("Instalar el certificado raíz en los equipos");
        Ui.Info($"Archivo: {Path.Combine(CertDir, Certificates.CaCert)}  (cópialo a cada equipo; NO copies ca.key)");
        Ui.Info("  Windows: doble clic > Instalar certificado > Equipo local > \"Entidades de certificación raíz de confianza\".");
        Ui.Info("  Android: Ajustes > Seguridad > Cifrado y credenciales > Instalar certificado > Certificado de CA.");
        Ui.Info("  iPhone: abrir el archivo > Ajustes > Perfil descargado > Instalar; luego General > Información >");
        Ui.Info("           Ajustes de confianza de certificados > activar \"TecNM Inventario CA\".");
    }

    public void CustomCertificateHelp()
    {
        Ui.Title("Usar un certificado entregado por sistemas");
        Ui.Info("Copia en database/certs/ el certificado como server.crt (con la cadena intermedia) y la llave como server.key,");
        Ui.Info("borra ca.crt y ca.key, y reinicia (tecnm restart). La validación pasará a usar los certificados del sistema.");
    }
}

internal sealed record ContainerInfo(string Service, string State, string Health, string Status);

internal sealed record CertificateInfo(string Subject, string NotAfter, int DaysLeft);

internal sealed record StatusInfo(bool Installed, bool DockerAvailable, bool Healthy, string WebUrl, string HealthUrl,
    IReadOnlyList<ContainerInfo> Containers, int? CertificateDaysLeft, string? LastBackup, bool AiEnabled, bool AiHealthy);
