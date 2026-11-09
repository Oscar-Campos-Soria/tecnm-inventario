using System.Text;

namespace TecnmCli;

/// <summary>Entrada y salida de consola. Con stdin redirigido lee líneas (scripts y pruebas).</summary>
internal static class Ui
{
    public const string Banner = @"
  _____ _____ ____ _   _ __  __
 |_   _| ____/ ___| \ | |  \/  |
   | | |  _|| |   |  \| | |\/| |
   | | | |__| |___| |\  | |  | |
   |_| |_____\____|_| \_|_|  |_|   Inventario de activos";

    public static void Title(string text) => Write($"\n== {text}", ConsoleColor.Cyan);
    public static void Ok(string text) => Write($"✓ {text}", ConsoleColor.Green);
    public static void Warn(string text) => Write($"! {text}", ConsoleColor.Yellow);
    public static void Error(string text) => Write($"✗ {text}", ConsoleColor.Red);
    public static void Info(string text) => Console.WriteLine(text);

    public static void Write(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    public static string Ask(string question, string? defaultValue = null)
    {
        Console.Write(defaultValue is null ? $"{question}: " : $"{question} [{defaultValue}]: ");
        var answer = Console.ReadLine()?.Trim() ?? string.Empty;
        return answer.Length == 0 && defaultValue is not null ? defaultValue : answer;
    }

    public static bool Confirm(string question) =>
        Ask($"{question} (s/n)", "n").StartsWith("s", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lee una contraseña sin mostrarla.</summary>
    public static string Secret(string question)
    {
        Console.Write($"{question}: ");
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? string.Empty;

        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return buffer.ToString();
    }

    public static void Pause()
    {
        if (Console.IsInputRedirected) return;
        Console.Write("\nPresiona Enter para volver al menú…");
        Console.ReadLine();
    }
}
