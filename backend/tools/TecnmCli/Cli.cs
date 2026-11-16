namespace TecnmCli;

/// <summary>Códigos de salida del modo directo (documentados en specs/deployment.spec.md).</summary>
internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;
    public const int NeedsConfirmation = 3;
    public const int NotReady = 4;
}

/// <summary>Error que corta la operación: código estable (para agentes), mensaje y qué hacer.</summary>
internal sealed class CliException(int exitCode, string code, string message, string? hint = null) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
    public string Code { get; } = code;
    public string? Hint { get; } = hint;

    public static CliException Usage(string message, string? hint = "tecnm help") => new(ExitCodes.Usage, "USAGE", message, hint);

    public static CliException NeedsYes(string what) =>
        new(ExitCodes.NeedsConfirmation, "CONFIRMATION_REQUIRED", $"{what} Agrega --yes para confirmarlo.", "Vuelve a ejecutar con --yes.");
}

/// <summary>
/// Argumentos del modo directo: palabras (comando y subcomando) y opciones --nombre valor | --nombre=valor | --bandera.
/// Acepta los nombres en español de antes (respaldar, estado, ia instalar --desde…) como alias.
/// </summary>
internal sealed class CliArgs
{
    private static readonly HashSet<string> ValueOptions = ["host", "tail", "backup", "user", "email", "first", "last", "from"];
    private static readonly HashSet<string> Flags = ["json", "yes", "password-stdin", "help"];

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["respaldar"] = "backup", ["estado"] = "status", ["iniciar"] = "start", ["detener"] = "stop", ["reiniciar"] = "restart",
        ["ia"] = "ai", ["instalar"] = "install", ["probar"] = "test", ["desactivar"] = "disable", ["actualizar"] = "update",
        ["restaurar"] = "restore", ["ayuda"] = "help", ["--desde"] = "--from", ["-h"] = "--help", ["--ayuda"] = "--help",
    };

    public IReadOnlyList<string> Words { get; }
    private readonly Dictionary<string, string?> _options;

    private CliArgs(List<string> words, Dictionary<string, string?> options)
    {
        Words = words;
        _options = options;
    }

    public string Command => Words.Count > 0 ? Words[0] : "menu";
    public string? Sub => Words.Count > 1 ? Words[1] : null;
    public bool Has(string flag) => _options.ContainsKey(flag);
    public string? Get(string name) => _options.GetValueOrDefault(name);

    public string Require(string name, string what) =>
        Get(name) is { Length: > 0 } value ? value : throw CliException.Usage($"Falta --{name} ({what}).");

    public static CliArgs Parse(IReadOnlyList<string> args)
    {
        var words = new List<string>();
        var options = new Dictionary<string, string?>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = Aliases.GetValueOrDefault(args[i], args[i]);
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                words.Add(Aliases.GetValueOrDefault(arg.ToLowerInvariant(), arg.ToLowerInvariant()));
                continue;
            }

            var name = arg[2..];
            string? value = null;
            var eq = name.IndexOf('=');
            if (eq >= 0) (name, value) = (name[..eq], name[(eq + 1)..]);

            if (Flags.Contains(name))
            {
                if (value is not null) throw CliException.Usage($"--{name} no lleva valor.");
            }
            else if (ValueOptions.Contains(name))
            {
                if (value is null)
                {
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        throw CliException.Usage($"--{name} necesita un valor.");
                    value = args[++i];
                }
            }
            else throw CliException.Usage($"Opción desconocida: --{name}.");

            options[name] = value;
        }
        return new CliArgs(words, options);
    }
}
