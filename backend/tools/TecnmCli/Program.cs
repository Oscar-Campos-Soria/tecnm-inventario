using System.Text;
using TecnmCli;

// tecnm: administración del despliegue de producción (database/docker-compose.prod.yml).
// Sin argumentos (o "menu") abre el menú; con argumentos ejecuta un comando directo (scripts, tareas, agentes).
Console.OutputEncoding = Encoding.UTF8;
return Commands.Run(args);
