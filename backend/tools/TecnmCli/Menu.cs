namespace TecnmCli;

/// <summary>
/// Modo interactivo: panel de estado + menú con flechas. Solo pide datos y confirma; cada opción llama a la
/// misma operación de Deployment que su comando directo (que se muestra bajo el menú: "Sin menú: tecnm …").
/// </summary>
internal static class Menu
{
    // Mismas teclas que el menú anterior: quien ya lo usaba no tiene que aprender nada.
    private static readonly MenuItem[] Main =
    [
        new("1", "Iniciar", "Servicio", "start"),
        new("2", "Detener", "Servicio", "stop"),
        new("3", "Reiniciar", "Servicio", "restart"),
        new("4", "Estado y logs", "Servicio", "status   ·   tecnm logs"),
        new("6", "Respaldar ahora", "Respaldos", "backup"),
        new("7", "Restaurar un respaldo", "Respaldos", "restore --backup <fecha> --yes"),
        new("P", "Programar respaldo diario", "Respaldos", "schedule-backup"),
        new("9", "Instalar por primera vez", "Instalación", "install --host <ip>"),
        new("5", "Actualizar a la versión actual", "Instalación", "update --yes"),
        new("8", "Crear / reactivar administrador", "Instalación", "admin --user <u> --email <correo> --password-stdin"),
        new("C", "Certificado HTTPS", "Instalación", "cert status | cert renew --yes | cert help"),
        new("I", "Asistente IA", "Instalación", "ai status | ai install | ai test | ai disable"),
        new("S", "Sandbox (desarrollo en este equipo)", "Desarrollo", "sandbox"),
        new("0", "Salir", " ", null),
    ];

    public static int Run(Deployment d)
    {
        while (true)
        {
            // Una consulta por vuelta: el panel se redibuja con cada flecha y no debe volver a llamar a docker.
            var status = d.GetStatus(withContainers: false);
            var key = Ui.Choose("¿Qué quieres hacer?", Main, () => Header(status));
            if (key is "0" or "") return ExitCodes.Ok;
            try
            {
                Execute(key, d);
            }
            catch (CliException ex)
            {
                Ui.Error(ex.Message);
                if (ex.Hint is not null) Ui.Info($"  → {ex.Hint}");
            }
            Ui.Pause();
            if (Console.IsInputRedirected && Console.In.Peek() == -1) return ExitCodes.Ok;
        }
    }

    private static void Header(StatusInfo s)
    {
        var (state, color) = !s.DockerAvailable ? ("○ Docker no responde: inícialo", ConsoleColor.Red)
            : !s.Installed ? ("○ No instalado: elige 9) Instalar por primera vez", ConsoleColor.Yellow)
            : s.Healthy ? ($"● En línea   {s.WebUrl}", ConsoleColor.Green)
            : ($"○ Detenido o sin responder   {s.WebUrl}", ConsoleColor.Red);
        var cert = s.CertificateDaysLeft is { } days ? $"Certificado: {days} días" : "Certificado: no hay";
        var ai = s.AiEnabled ? (s.AiHealthy ? "IA: ● lista" : "IA: ○ sin responder") : "IA: apagada";
        Ui.Banner();
        Console.WriteLine();
        Ui.Panel(
        [
            (state, color),
            ($"{cert}   ·   Último respaldo: {s.LastBackup ?? "ninguno"}   ·   {ai}",
                s.CertificateDaysLeft is < 30 || s.LastBackup is null ? ConsoleColor.Yellow : ConsoleColor.Gray),
        ]);
    }

    private static void Execute(string key, Deployment d)
    {
        switch (key)
        {
            case "1": d.Start(); break;
            case "2": d.Stop(); break;
            case "3": d.Restart(); break;
            case "4":
                var status = d.GetStatus();
                d.Status(status);
                if (status.Installed && status.DockerAvailable) { Ui.Title("Últimos logs"); d.Logs(); }
                break;
            case "5":
                var commit = d.PendingCommit();
                if (Ui.Confirm($"Se respalda, se reconstruyen las imágenes con {commit ?? "el código actual"} y se reinicia. ¿Continuar?"))
                    d.Update();
                break;
            case "6": d.Backup(); break;
            case "7": Restore(d); break;
            case "8": CreateAdmin(d); break;
            case "9":
                var host = d.IsInstalled ? null : Ui.Ask("IP o nombre con el que entrarán los usuarios (sin puerto)", Deployment.DefaultHost);
                if (d.Install(host) && Ui.Confirm("¿Crear ahora el primer administrador?")) CreateAdmin(d);
                break;
            case "C": Certificate(d); break;
            case "P": d.ScheduleBackup(); break;
            case "S": Sandbox.Run(d.Root); break;
            case "I": Ai(d); break;
            default: Ui.Warn("Opción no válida."); break;
        }
    }

    private static void Restore(Deployment d)
    {
        var stamps = d.BackupStamps();
        if (stamps.Count == 0) { Ui.Warn("No hay respaldos en database/backups/."); return; }
        var items = stamps.Select((s, i) => new MenuItem((i + 1).ToString(), s, "Respaldos (el más nuevo arriba)", $"restore --backup {s} --yes"))
            .Append(new MenuItem("0", "Volver", " ")).ToList();
        var key = Ui.Choose("¿Cuál respaldo restaurar?", items);
        var chosen = items.FirstOrDefault(i => i.Key == key && key != "0");
        if (chosen is null) { Ui.Warn("Cancelado."); return; }

        Ui.Warn($"Se reemplazarán TODOS los datos actuales por los del respaldo {chosen.Label}.");
        if (Ui.Ask("Escribe RESTAURAR para confirmar") != "RESTAURAR") { Ui.Warn("Cancelado."); return; }
        d.Restore(chosen.Label);
    }

    private static void CreateAdmin(Deployment d)
    {
        Ui.Title("Crear o reactivar administrador");
        var username = Ui.Ask("Usuario", "admin");
        var email = Ui.Ask("Correo");
        var first = Ui.Ask("Nombre", "Administrador");
        var last = Ui.Ask("Apellidos", "del sistema");
        var password = Ui.Secret("Contraseña (mínimo 12 caracteres, no se muestra)");
        Deployment.ValidateAdmin(username, email, first, last, password);
        if (Ui.Secret("Repite la contraseña") != password) { Ui.Error("Las contraseñas no coinciden."); return; }
        d.CreateAdmin(username, email, first, last, password);
    }

    private static void Certificate(Deployment d)
    {
        MenuItem[] items =
        [
            new("1", "Regenerar el del servidor (misma CA)", "Certificado HTTPS", "cert renew --yes"),
            new("2", "Cómo instalar el certificado raíz", "Certificado HTTPS", "cert help"),
            new("3", "Usar uno entregado por sistemas", "Certificado HTTPS", "cert help"),
            new("0", "Volver", " "),
        ];
        switch (Ui.Choose("Certificado HTTPS", items, () => d.CertificateStatus()))
        {
            case "1":
                if (Ui.Confirm("Se reinicia la pantalla web unos segundos. ¿Continuar?")) d.RenewCertificate();
                break;
            case "2": d.CertificateHelp(); break;
            case "3": d.CustomCertificateHelp(); break;
        }
    }

    private static void Ai(Deployment d)
    {
        var line = d.IsInstalled ? d.AiStatusLine() : "IA: instala el sistema primero";
        MenuItem[] items =
        [
            new("1", "Instalar / encender (descarga ~1.9 GB)", "Asistente IA", "ai install"),
            new("2", "Instalar desde una carpeta (sin internet)", "Asistente IA", "ai install --from <carpeta>"),
            new("3", "Estado y memoria", "Asistente IA", "ai status"),
            new("4", "Probar", "Asistente IA", "ai test"),
            new("5", "Apagar (conserva el modelo)", "Asistente IA", "ai disable"),
            new("0", "Volver", " "),
        ];
        switch (Ui.Choose("Asistente IA (carga masiva y Pregúntale)", items, () => Ui.Info($"\n  {line}")))
        {
            case "1": d.AiInstall(null); break;
            case "2": d.AiInstall(Ui.Ask("Carpeta con " + string.Join(" y ", AiModel.Files.Select(f => f.FileName)))); break;
            case "3": d.AiStatus(); break;
            case "4": d.AiTest(); break;
            case "5": d.AiDisable(); break;
        }
    }
}
