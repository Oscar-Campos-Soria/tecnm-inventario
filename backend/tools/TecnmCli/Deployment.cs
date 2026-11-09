using System.Text;
using System.Text.RegularExpressions;

namespace TecnmCli;

/// <summary>
/// Operaciones sobre el despliegue de producción (database/docker-compose.prod.yml).
/// Todo pasa por docker compose; la BD y la API no tienen puertos publicados.
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

    private string[] Compose(params string[] args) => ["compose", "-f", ComposeFile, "--env-file", EnvFile, .. args];

    private Dictionary<string, string> Env => File.Exists(EnvFile)
        ? ProdEnv.Parse(File.ReadAllText(EnvFile))
        : new Dictionary<string, string>();

    private string Host => Env.GetValueOrDefault("PUBLIC_HOST", "localhost");
    public string WebUrl => $"https://{Host}:6010";
    private string HealthUrl => $"https://{Host}:6011/api/health";

    private string[] Psql(params string[] extra)
    {
        var env = Env;
        return Compose(["exec", "-T", "db", "psql", "-v", "ON_ERROR_STOP=1", "-q",
            "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"], .. extra]);
    }

    // ---------------------------------------------------------------- estado

    public bool IsHealthy() => File.Exists(EnvFile) && Shell.IsHealthy(HealthUrl, CertDir);

    public string StatusLine()
    {
        if (!File.Exists(EnvFile)) return "No instalado (opción 9)";
        var app = IsHealthy() ? "● en línea" : "○ detenido o sin responder";
        var cert = Certificates.ReadServer(CertDir);
        var days = cert is null ? "sin certificado" : $"certificado: {(cert.NotAfter - DateTime.Now).Days} días";
        var last = Directory.Exists(BackupDir) ? BackupFiles.Stamps(Directory.GetFiles(BackupDir).Select(Path.GetFileName)!).FirstOrDefault() : null;
        return $"{app}   {WebUrl}   {days}   último respaldo: {last ?? "ninguno"}";
    }

    public void Status()
    {
        Ui.Title("Contenedores");
        Shell.Stream("docker", Compose("ps", "--format", "table {{.Service}}\t{{.Status}}\t{{.Ports}}"));
        Ui.Title("Salud");
        if (IsHealthy()) Ui.Ok($"API y base de datos responden ({HealthUrl})");
        else Ui.Error($"Sin respuesta en {HealthUrl}");
        Ui.Info(StatusLine());
    }

    public void Logs() => Shell.Stream("docker", Compose("logs", "--tail", "80", "api", "web"));

    // ------------------------------------------------------------- servicios

    public void Start()
    {
        if (!Ready()) return;
        Shell.Stream("docker", Compose("up", "-d"));
        WaitHealthy();
    }

    public void Stop() => Shell.Stream("docker", Compose("stop"));

    public void Restart()
    {
        if (!Ready()) return;
        Shell.Stream("docker", Compose("restart"));
        WaitHealthy();
    }

    /// <summary>Docker disponible y .env.prod válido; si no, explica qué falta.</summary>
    private bool Ready()
    {
        var (code, _) = Shell.Capture("docker", ["info", "--format", "{{.ServerVersion}}"]);
        if (code != 0)
        {
            Ui.Error("Docker no responde. Instálalo o inícialo (en Linux: usuario en el grupo docker o sudo).");
            return false;
        }
        if (!File.Exists(EnvFile))
        {
            Ui.Error("No existe database/.env.prod. Usa la opción 9 (Instalar).");
            return false;
        }
        var problems = ProdEnv.Problems(Env);
        foreach (var p in problems) Ui.Error(p);
        return problems.Count == 0;
    }

    private bool WaitHealthy()
    {
        Console.Write("Esperando a que la API responda");
        for (var i = 0; i < 60; i++)
        {
            if (IsHealthy())
            {
                Console.WriteLine();
                Ui.Ok($"En línea: {WebUrl}");
                return true;
            }
            Console.Write('.');
            Thread.Sleep(2000);
        }
        Console.WriteLine();
        Ui.Error("La API no respondió en 2 minutos. Revisa los logs (opción 4).");
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

    public void Install()
    {
        Ui.Title("Instalar");
        if (!File.Exists(EnvFile))
        {
            var host = Ui.Ask("IP o nombre con el que entrarán los usuarios (sin puerto)", Shell.LocalIPv4() ?? "localhost");
            Directory.CreateDirectory(Path.GetDirectoryName(EnvFile)!);
            Certificates.WriteSecret(EnvFile, ProdEnv.Generate(host));
            Ui.Ok("database/.env.prod creado con contraseñas aleatorias (no se muestran).");
        }
        else
        {
            Ui.Info("database/.env.prod ya existe: se conserva.");
        }
        if (!Ready()) return;

        if (Certificates.ReadServer(CertDir) is null)
        {
            Certificates.Issue(CertDir, Host, DateTimeOffset.Now);
            Ui.Ok($"Certificado HTTPS emitido para {Host} con la CA propia (database/certs/ca.crt).");
        }

        Ui.Title("Construyendo e iniciando contenedores");
        if (Shell.Stream("docker", Compose("up", "-d", "--build")) != 0 || !WaitDatabase()) return;
        if (!ApplyScripts()) return;
        // La API arrancó antes del esquema: se reinicia para que sus servicios en segundo plano lo vean.
        Shell.Stream("docker", Compose("restart", "api"));
        if (!WaitHealthy()) return;

        if (Ui.Confirm("¿Crear ahora el primer administrador?")) CreateAdmin();
        Ui.Title("Listo");
        Ui.Info($"Pantallas: {WebUrl}");
        Ui.Info("Instala el certificado raíz en cada equipo para quitar la advertencia (opción C).");
        Ui.Info("Programa el respaldo diario con la opción P.");
    }

    public void Update()
    {
        Ui.Title("Actualizar");
        if (!Ready()) return;
        var (gitCode, commit) = Shell.Capture("git", ["-C", Root, "log", "-1", "--format=%h %s"]);
        if (gitCode == 0) Ui.Info($"Código a desplegar: {commit.Trim()}  (haz git pull antes si hace falta)");
        if (!Ui.Confirm("Se respalda, se reconstruyen las imágenes y se reinicia. ¿Continuar?")) return;
        if (!Backup()) { Ui.Error("Sin respaldo no se actualiza."); return; }

        if (Shell.Stream("docker", Compose("build")) != 0) { Ui.Error("Falló la construcción; producción sigue con la versión anterior."); return; }
        if (Shell.Stream("docker", Compose("up", "-d")) != 0 || !WaitDatabase()) return;
        if (!ApplyScripts()) return;
        WaitHealthy();
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
                Ui.Error($"{script} falló:");
                Ui.Info(output.Trim());
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
        if (!Ready()) return false;
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

    public void Restore()
    {
        Ui.Title("Restaurar respaldo");
        if (!Ready()) return;
        var stamps = Directory.Exists(BackupDir) ? BackupFiles.Stamps(Directory.GetFiles(BackupDir).Select(Path.GetFileName)!) : [];
        if (stamps.Count == 0) { Ui.Warn("No hay respaldos en database/backups/."); return; }

        for (var i = 0; i < stamps.Count; i++) Ui.Info($"  {i + 1}) {stamps[i]}");
        if (!int.TryParse(Ui.Ask("Número de respaldo"), out var n) || n < 1 || n > stamps.Count) { Ui.Warn("Cancelado."); return; }
        var stamp = stamps[n - 1];

        Ui.Warn($"Se reemplazarán TODOS los datos actuales por los del respaldo {stamp}.");
        if (Ui.Ask("Escribe RESTAURAR para confirmar") != "RESTAURAR") { Ui.Warn("Cancelado."); return; }
        if (!Backup()) { Ui.Error("No se pudo sacar el respaldo de seguridad: no se restaura."); return; }

        var env = Env;
        Shell.Stream("docker", Compose("stop", "api"));
        int code;
        using (var dump = File.OpenRead(Path.Combine(BackupDir, BackupFiles.DbFile(stamp))))
            code = Shell.PipeIn("docker", Compose("exec", "-T", "db", "pg_restore", "--clean", "--if-exists", "--no-owner",
                "--single-transaction", "-U", env["POSTGRES_USER"], "-d", env["POSTGRES_DB"]), dump);
        Shell.Stream("docker", Compose("start", "api"));
        if (code != 0) { Ui.Error("pg_restore falló; la transacción se revirtió y los datos siguen como estaban."); return; }
        Ui.Ok("Base de datos restaurada.");

        var docs = Path.Combine(BackupDir, BackupFiles.DocsFile(stamp));
        if (File.Exists(docs) && WaitHealthy())
        {
            using var tar = File.OpenRead(docs);
            var docsCode = Shell.PipeIn("docker", Compose("exec", "-T", "api", "sh", "-c",
                "find /data/documents -mindepth 1 -delete && tar xzf - -C /data"), tar);
            if (docsCode == 0) Ui.Ok("Documentos restaurados."); else Ui.Error("Falló la restauración de documentos.");
        }
    }

    public void ScheduleBackup()
    {
        Ui.Title("Programar respaldo diario (02:00)");
        var exe = Environment.ProcessPath;
        if (exe is null || Path.GetFileNameWithoutExtension(exe) != "tecnm")
        {
            Ui.Error("Publica el CLI primero (ver specs/deployment.spec.md); con 'dotnet run' no se puede programar.");
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            var code = Shell.Stream("schtasks", ["/Create", "/F", "/SC", "DAILY", "/ST", "02:00", "/TN", "TecNM Inventario respaldo", "/TR", $"\"{exe}\" respaldar"]);
            if (code == 0) Ui.Ok("Tarea 'TecNM Inventario respaldo' creada en el Programador de tareas.");
            return;
        }

        var line = $"0 2 * * * TECNM_ROOT=\"{Root}\" \"{exe}\" respaldar >> \"{Path.Combine(BackupDir, "respaldo.log")}\" 2>&1";
        var (_, current) = Shell.Capture("crontab", ["-l"]);
        if (current.Contains(" respaldar", StringComparison.Ordinal)) { Ui.Ok("Ya estaba programado en crontab."); return; }
        var lines = current.StartsWith("no crontab", StringComparison.OrdinalIgnoreCase) ? "" : current.TrimEnd() + "\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(lines + line + "\n"));
        if (Shell.PipeIn("crontab", ["-"], input) == 0) Ui.Ok("Respaldo diario agregado al crontab de este usuario.");
    }

    // ---------------------------------------------------------- administrador

    [GeneratedRegex("^[A-Za-z0-9._-]{3,60}$")]
    private static partial Regex UsernamePattern();

    public void CreateAdmin()
    {
        Ui.Title("Crear o reactivar administrador");
        if (!Ready()) return;
        var username = Ui.Ask("Usuario", "admin");
        if (!UsernamePattern().IsMatch(username)) { Ui.Error("Usuario: 3 a 60 letras, números, punto, guion o guion bajo."); return; }
        var email = Ui.Ask("Correo");
        if (!email.Contains('@') || email.Length > 254) { Ui.Error("Correo no válido."); return; }
        var first = Ui.Ask("Nombre", "Administrador");
        var last = Ui.Ask("Apellidos", "del sistema");
        if (first.Length is 0 or > 100 || last.Length is 0 or > 100) { Ui.Error("Nombre y apellidos: 1 a 100 caracteres."); return; }

        var password = Ui.Secret("Contraseña (mínimo 12 caracteres, no se muestra)");
        if (password.Length < 12) { Ui.Error("La contraseña debe tener al menos 12 caracteres."); return; }
        if (Ui.Secret("Repite la contraseña") != password) { Ui.Error("Las contraseñas no coinciden."); return; }

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
    }

    // ------------------------------------------------------------ certificado

    public void CertificateMenu()
    {
        Ui.Title("Certificado HTTPS");
        var cert = Certificates.ReadServer(CertDir);
        if (cert is null) Ui.Warn("No hay certificado en database/certs/.");
        else
        {
            Ui.Info($"Emitido para: {cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false)}");
            Ui.Info($"Vence: {cert.NotAfter:yyyy-MM-dd} ({(cert.NotAfter - DateTime.Now).Days} días)");
            if ((cert.NotAfter - DateTime.Now).TotalDays < 30) Ui.Warn("Vence en menos de 30 días: regenéralo.");
        }

        Ui.Info("\n  1) Regenerar certificado del servidor (misma CA; los equipos no reinstalan nada)");
        Ui.Info("  2) Cómo instalar el certificado raíz en los equipos");
        Ui.Info("  3) Usar un certificado entregado por sistemas");
        Ui.Info("  0) Volver");
        switch (Ui.Ask("Opción", "0"))
        {
            case "1":
                if (!File.Exists(EnvFile)) { Ui.Error("Instala primero (opción 9)."); return; }
                Certificates.Issue(CertDir, Host, DateTimeOffset.Now);
                Ui.Ok($"Certificado emitido para {Host}.");
                Shell.Stream("docker", Compose("restart", "web"));
                break;
            case "2":
                Ui.Info($"Archivo: {Path.Combine(CertDir, Certificates.CaCert)}  (cópialo a cada equipo; NO copies ca.key)");
                Ui.Info("  Windows: doble clic > Instalar certificado > Equipo local > \"Entidades de certificación raíz de confianza\".");
                Ui.Info("  Android: Ajustes > Seguridad > Cifrado y credenciales > Instalar certificado > Certificado de CA.");
                Ui.Info("  iPhone: abrir el archivo > Ajustes > Perfil descargado > Instalar; luego General > Información >");
                Ui.Info("           Ajustes de confianza de certificados > activar \"TecNM Inventario CA\".");
                break;
            case "3":
                Ui.Info("Copia en database/certs/ el certificado como server.crt (con la cadena intermedia) y la llave como server.key,");
                Ui.Info("borra ca.crt y ca.key, y reinicia (opción 3). La validación pasará a usar los certificados del sistema.");
                break;
        }
    }
}
