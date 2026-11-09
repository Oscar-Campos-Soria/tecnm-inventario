using System.Text;
using TecnmCli;

// tecnm: administración del despliegue de producción (database/docker-compose.prod.yml).
// Sin argumentos abre el menú; con argumento ejecuta un comando (para tareas programadas).
Console.OutputEncoding = Encoding.UTF8;

var root = Deployment.FindRoot();
if (root is null)
{
    Ui.Error("No encuentro database/docker-compose.prod.yml. Ejecuta tecnm dentro del repositorio o define TECNM_ROOT.");
    return 1;
}
var deployment = new Deployment(root);

if (args.Length > 0)
{
    switch (args[0])
    {
        case "respaldar": return deployment.Backup() ? 0 : 1;
        case "estado": deployment.Status(); return deployment.IsHealthy() ? 0 : 1;
        case "iniciar": deployment.Start(); return 0;
        case "detener": deployment.Stop(); return 0;
        default:
            Ui.Info("Uso: tecnm [respaldar | estado | iniciar | detener]   (sin argumentos: menú)");
            return 2;
    }
}

while (true)
{
    if (!Console.IsInputRedirected && !Console.IsOutputRedirected) Console.Clear();
    Ui.Write(Ui.Banner, ConsoleColor.Cyan);
    Ui.Info($"\n  {deployment.StatusLine()}\n");
    Ui.Info("  1) Iniciar                 6) Respaldar ahora");
    Ui.Info("  2) Detener                 7) Restaurar respaldo");
    Ui.Info("  3) Reiniciar               8) Crear / reactivar administrador");
    Ui.Info("  4) Estado y logs           9) Instalar por primera vez");
    Ui.Info("  5) Actualizar              C) Certificado HTTPS");
    Ui.Info("  P) Programar respaldo      S) Sandbox (modo desarrollo en este equipo)");
    Ui.Info("  0) Salir\n");

    var option = Ui.Ask("Opción").ToUpperInvariant();
    switch (option)
    {
        case "1": deployment.Start(); break;
        case "2": deployment.Stop(); break;
        case "3": deployment.Restart(); break;
        case "4": deployment.Status(); Ui.Title("Últimos logs"); deployment.Logs(); break;
        case "5": deployment.Update(); break;
        case "6": deployment.Backup(); break;
        case "7": deployment.Restore(); break;
        case "8": deployment.CreateAdmin(); break;
        case "9": deployment.Install(); break;
        case "C": deployment.CertificateMenu(); break;
        case "P": deployment.ScheduleBackup(); break;
        case "S": Sandbox.Run(root); break;
        case "0" or "": return 0;
        default: Ui.Warn("Opción no válida."); break;
    }
    Ui.Pause();
    if (Console.IsInputRedirected && Console.In.Peek() == -1) return 0;
}
