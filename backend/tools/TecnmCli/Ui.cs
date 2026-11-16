using System.Text;

namespace TecnmCli;

/// <summary>Una opción de menú: tecla (atajo), texto, sección y comando directo equivalente.</summary>
internal sealed record MenuItem(string Key, string Label, string Section = "", string? Command = null);

/// <summary>
/// Entrada y salida de consola. Con --json no imprime nada: junta los mensajes y Program escribe un solo objeto.
/// Con stdin redirigido lee líneas (scripts y pruebas) y el menú se elige escribiendo la tecla.
/// </summary>
internal static class Ui
{
    public static bool Json { get; set; }
    public static List<CliMessage> Messages { get; } = [];
    /// <summary>Último error mostrado: es el "error" del JSON cuando una operación devuelve false.</summary>
    public static string? LastError { get; private set; }

    public static void Title(string text)
    {
        if (!Json) Write($"\n== {text}", ConsoleColor.Cyan);
    }

    public static void Ok(string text) => Emit("ok", text, "✓ ", ConsoleColor.Green);
    public static void Warn(string text) => Emit("warn", text, "! ", ConsoleColor.Yellow);
    public static void Info(string text) => Emit("info", text, "", null);

    public static void Error(string text)
    {
        LastError = text;
        Emit("error", text, "✗ ", ConsoleColor.Red);
    }

    /// <summary>Salida de docker, git, etc. En modo JSON va a messages en vez de la pantalla.</summary>
    public static void Output(string text)
    {
        if (text.Length > 0) Messages.Add(new CliMessage("output", text.TrimEnd()));
    }

    /// <summary>Puntos y porcentajes de espera: solo para personas.</summary>
    public static void Progress(string text)
    {
        if (!Json) Console.Write(text);
    }

    private static void Emit(string level, string text, string prefix, ConsoleColor? color)
    {
        if (Json) { Messages.Add(new CliMessage(level, text)); return; }
        if (color is null) Console.WriteLine(text);
        else Write(prefix + text, color.Value);
    }

    public static void Write(string text, ConsoleColor color, bool newLine = true)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newLine) Console.WriteLine(text); else Console.Write(text);
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

    // ------------------------------------------------------------------ menú

    private static bool Interactive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>
    /// Menú con flechas ↑↓ + Enter, atajo por tecla y Esc para volver. Devuelve la tecla elegida ("0" = salir).
    /// header dibuja lo que va arriba (estado). Sin terminal (stdin redirigido) lee la tecla como texto.
    /// </summary>
    public static string Choose(string title, IReadOnlyList<MenuItem> items, Action? header = null)
    {
        if (!Interactive)
        {
            header?.Invoke();
            Console.WriteLine();
            foreach (var item in items) Console.WriteLine($"  {item.Key}) {item.Label}");
            return Ask("Opción", "0").ToUpperInvariant();
        }

        var selected = 0;
        while (true)
        {
            Console.Clear();
            header?.Invoke();
            Render(title, items, selected);
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow: selected = (selected + items.Count - 1) % items.Count; break;
                case ConsoleKey.DownArrow: selected = (selected + 1) % items.Count; break;
                case ConsoleKey.Home: selected = 0; break;
                case ConsoleKey.End: selected = items.Count - 1; break;
                case ConsoleKey.Enter: Console.Clear(); return items[selected].Key;
                case ConsoleKey.Escape: Console.Clear(); return "0";
                default:
                    var typed = char.ToUpperInvariant(key.KeyChar).ToString();
                    if (items.Any(i => i.Key == typed)) { Console.Clear(); return typed; }
                    break;
            }
        }
    }

    private static void Render(string title, IReadOnlyList<MenuItem> items, int selected)
    {
        Write($"\n  {title}", ConsoleColor.White);
        var section = "";
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Section != section)
            {
                section = item.Section;
                Write($"\n  {section.ToUpperInvariant()}", ConsoleColor.DarkCyan);
            }
            if (i == selected)
            {
                Write("  ❯ ", ConsoleColor.Cyan, newLine: false);
                var (fg, bg) = (Console.ForegroundColor, Console.BackgroundColor);
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($" {item.Label,-34} ");
                Console.BackgroundColor = bg;
                Console.ForegroundColor = fg;
                Write($"  {item.Key}", ConsoleColor.Cyan);
            }
            else
            {
                Console.Write($"     {item.Label,-34}  ");
                Write(item.Key, ConsoleColor.DarkGray);
            }
        }
        var command = items[selected].Command;
        Console.WriteLine();
        if (command is not null) Write($"  Sin menú: tecnm {command}", ConsoleColor.DarkGray);
        Write("  ↑↓ moverse · Enter elegir · tecla = atajo · Esc volver", ConsoleColor.DarkGray);
    }

    // Letras de bloque (estilo "ANSI Shadow"), una por arreglo para que las columnas siempre cuadren.
    private static readonly string[][] Letters =
    [
        ["████████╗", "╚══██╔══╝", "   ██║   ", "   ██║   ", "   ██║   ", "   ╚═╝   "],
        ["███████╗", "██╔════╝", "█████╗  ", "██╔══╝  ", "███████╗", "╚══════╝"],
        [" ██████╗", "██╔════╝", "██║     ", "██║     ", "╚██████╗", " ╚═════╝"],
        ["███╗   ██╗", "████╗  ██║", "██╔██╗ ██║", "██║╚██╗██║", "██║ ╚████║", "╚═╝  ╚═══╝"],
        ["███╗   ███╗", "████╗ ████║", "██╔████╔██║", "██║╚██╔╝██║", "██║ ╚═╝ ██║", "╚═╝     ╚═╝"],
    ];

    private static readonly ConsoleColor[] Gradient =
        [ConsoleColor.Cyan, ConsoleColor.Cyan, ConsoleColor.DarkCyan, ConsoleColor.DarkCyan, ConsoleColor.Blue, ConsoleColor.DarkBlue];

    /// <summary>"TECNM" en letras de bloque con degradado. En una terminal baja, una sola línea para que el menú quepa.</summary>
    public static void Banner()
    {
        var tall = !Interactive || SafeHeight() >= 40;
        Console.WriteLine();
        if (tall)
        {
            for (var row = 0; row < Gradient.Length; row++)
            {
                // Relleno (█) con el degradado y bordes (╗║═) en azul oscuro: efecto de sombra.
                Console.Write("  ");
                foreach (var ch in string.Concat(Letters.Select(l => l[row])))
                    Write(ch.ToString(), ch == '█' ? Gradient[row] : ConsoleColor.DarkBlue, newLine: false);
                Console.WriteLine();
            }
            Write("  Inventario de activos tecnológicos", ConsoleColor.DarkGray);
        }
        else
        {
            Write("  ▌TECNM▐  ", ConsoleColor.Cyan, newLine: false);
            Write("Inventario de activos tecnológicos", ConsoleColor.DarkGray);
        }
    }

    private static int SafeHeight()
    {
        try { return Console.WindowHeight; }
        catch (IOException) { return 50; }
    }

    /// <summary>Panel de estado con borde. Cada línea: (texto, color).</summary>
    public static void Panel(IReadOnlyList<(string Text, ConsoleColor Color)> lines)
    {
        var width = Math.Max(lines.Max(l => l.Text.Length), 40) + 2;
        Write($"  ╭{new string('─', width)}╮", ConsoleColor.DarkGray);
        foreach (var (text, color) in lines)
        {
            Write("  │ ", ConsoleColor.DarkGray, newLine: false);
            Write(text.PadRight(width - 1), color, newLine: false);
            Write("│", ConsoleColor.DarkGray);
        }
        Write($"  ╰{new string('─', width)}╯", ConsoleColor.DarkGray);
    }
}

internal sealed record CliMessage(string Level, string Text);
