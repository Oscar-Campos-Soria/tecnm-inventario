using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Tests.Eval.Engine;

/// <summary>
/// Variantes generadas de una frase (no escritas a mano): errores de dedo y registro coloquial.
/// Deterministas (mismo texto → misma variante) para que dos corridas sean comparables.
/// </summary>
public static partial class Variants
{
    public static readonly string[] Kinds = ["error_dedo", "coloquial", "telegrafico"];

    private static readonly (string From, string To)[] Colloquial =
    [
        ("computadoras", "compus"), ("computadora", "compu"), ("laboratorio", "lab"), ("por favor", "porfa"),
        ("tickets", "tickets"), ("cuántos", "cuantos"), ("cuántas", "cuantas"), ("qué", "que"), ("está", "ta"),
        ("están", "tan"), ("para", "pa"), ("equipos", "equipos"), ("necesito", "ocupo"), ("muéstrame", "enseñame"),
    ];

    private static readonly HashSet<string> Stop = ["el", "la", "los", "las", "de", "del", "que", "en", "un", "una", "y", "a", "me", "mi", "hay", "es", "se", "lo", "al", "con", "por", "para"];

    [GeneratedRegex(@"[¿¡?!.,]")]
    private static partial Regex Punct();

    public static string Make(string text, string kind) => kind switch
    {
        "error_dedo" => Typo(Plain(text)),
        "coloquial" => Casual(text),
        "telegrafico" => string.Join(" ", Plain(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Stop.Contains(w))),
        _ => text,
    };

    private static string Plain(string text) => Punct().Replace(AiText.Plain(text).ToLowerInvariant(), "").Trim();

    // Intercambia dos letras de la palabra más larga (sin tocar códigos como ZZ-IA-06).
    private static string Typo(string text)
    {
        var words = text.Split(' ');
        var i = Array.FindIndex(words, w => w == words.Where(x => x.Length >= 5 && !x.Any(char.IsDigit) && !x.Contains('-')).MaxBy(x => x.Length));
        if (i < 0) return text + "?";
        var w = words[i].ToCharArray();
        var at = Math.Abs(StableHash(text)) % (w.Length - 2) + 1;
        (w[at], w[at + 1]) = (w[at + 1], w[at]);
        words[i] = new string(w);
        return string.Join(' ', words);
    }

    private static string Casual(string text)
    {
        var t = text.ToLowerInvariant();
        foreach (var (from, to) in Colloquial) t = Regex.Replace(t, $@"\b{Regex.Escape(from)}\b", to);
        t = Punct().Replace(t, "").Trim();
        return (StableHash(text) % 2 == 0 ? "oye " : "una pregunta, ") + t + (t.Contains("porfa") ? "" : " porfa");
    }

    public static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }
}

/// <summary>Lo que se obtuvo de la API para un fact: total y valores reales en el momento de la corrida.</summary>
public sealed record FactTruth(string Expect, int Count, IReadOnlyList<string> Values, string Match);

/// <summary>Comparación de una respuesta y los datos de las herramientas contra la verdad calculada.</summary>
public static partial class Judge
{
    // Frases con que el asistente dice "no hay" (incluye "no tiene técnico asignado").
    private static readonly string[] None =
    [
        "no hay", "ningun", "ninguna", "no tienes", "no tiene", "no se encontr", "no encontre", "no existen", "no cuentas",
        "sin asignar", "nadie", "aun no", "todavia no", "no ha sido asignad", "no esta asignad", "sin tecnico", "no se ha asignado", "0 ",
    ];

    private static readonly string[] Numbers = ["cero", "uno|un solo|una sola|solo un|solo una|unico|unica", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez"];

    [GeneratedRegex(@"\b(ZZ-IA-\d{2}|ZZ-CARGA-\d{2}|TEC-PRUEBA-TK-\d{2})\b", RegexOptions.IgnoreCase)]
    public static partial Regex AssetCode();

    public static string Plain(string? text) => AiText.Plain(text).ToLowerInvariant();

    public static bool InText(FactTruth fact, string plain) => fact.Expect switch
    {
        "count" => HasNumber(plain, fact.Count),
        _ when fact.Values.Count == 0 => None.Any(plain.Contains),
        _ => fact.Match == "any" ? fact.Values.Any(v => HasValue(plain, v)) : fact.Values.Take(8).All(v => HasValue(plain, v)),
    };

    public static bool InData(FactTruth fact, string plainData) => fact.Expect switch
    {
        // Laxo a propósito: el número aparece como valor JSON en lo que devolvieron las herramientas.
        "count" => Regex.IsMatch(plainData, $@"[:\[,]\s*{fact.Count}\s*[,}}\]]") || (fact.Count == 0 && plainData.Contains("[]")),
        _ when fact.Values.Count == 0 => true,
        _ => fact.Match == "any" ? fact.Values.Any(v => HasValue(plainData, v)) : fact.Values.Take(8).All(v => HasValue(plainData, v)),
    };

    public static bool HasNumber(string plain, int n)
    {
        if (Regex.IsMatch(plain, $@"(?<![\d-]){n}(?![\d-])")) return true;
        if (n == 0 && None.Any(plain.Contains)) return true;
        return n <= 10 && Numbers[n].Split('|').Any(w => Regex.IsMatch(plain, $@"\b{w}\b"))
            // "hay una impresora", "tienes un ticket": artículo + sustantivo cuenta como 1.
            || (n == 1 && Regex.IsMatch(plain, @"\b(hay|tiene|tienes|existe|encontre|es) (un|una)\b"));
    }

    // Códigos y números: exactos. Textos (títulos, nombres): basta con 60 % de sus palabras de 4+ letras.
    public static bool HasValue(string plain, string value)
    {
        var v = Plain(value);
        if (plain.Contains(v)) return true;
        if (v.Any(char.IsDigit)) return false;
        var words = Regex.Matches(v, @"[a-z0-9_]{4,}").Select(m => m.Value).Where(w => w != "zz" ).ToList();
        return words.Count > 0 && words.Count(plain.Contains) >= Math.Ceiling(words.Count * 0.6);
    }

    /// <summary>Items de una respuesta de la API (lista o paginada) filtrados por where.</summary>
    public static FactTruth Truth(JsonNode root, JsonObject fact, DateTime nowUtc)
    {
        var items = (root as JsonArray ?? root["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var where = fact["where"] as JsonObject;
        if (where is not null)
            items = items.Where(i => where.All(w => Matches(i[w.Key], (string)w.Value!, nowUtc))).ToList();
        var count = where is null && root is JsonObject o && o["totalCount"] is { } total ? (int)total : items.Count;
        var field = (string?)fact["field"];
        var values = field is null ? [] : items.Select(i => i[field]?.ToString()).OfType<string>().Where(s => s.Length > 0).Distinct().ToList();
        return new FactTruth((string)fact["expect"]!, count, values, (string?)fact["match"] ?? "all");
    }

    private static bool Matches(JsonNode? node, string rule, DateTime nowUtc)
    {
        var value = node?.ToString();
        if (rule == "!null") return !string.IsNullOrEmpty(value);
        if (rule == "null") return string.IsNullOrEmpty(value);
        if (rule.StartsWith(">=-") && rule.EndsWith('d'))
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var at)
                && at >= nowUtc.AddDays(-int.Parse(rule[3..^1], CultureInfo.InvariantCulture));
        return string.Equals(Plain(value), Plain(rule), StringComparison.Ordinal);
    }

    public static string Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return "-";
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)].ToString("0", CultureInfo.InvariantCulture);
    }

    public static string Sha256(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
