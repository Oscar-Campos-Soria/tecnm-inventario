using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Ai;

/// <summary>
/// Catálogos que el modelo y las reglas usan: tipos (código, nombre) y ubicaciones con su ruta
/// ("Edificio B, Planta 1, Laboratorio de redes"). Lo comparten la carga masiva y el asistente.
/// </summary>
public sealed record AiCatalog(IReadOnlyList<(string Code, string Name)> Types, IReadOnlyList<(string Code, string Path)> Locations)
{
    public static async Task<AiCatalog> LoadAsync(TecnmInventarioDbContext context, CancellationToken ct)
    {
        var types = await context.AssetTypes.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Code)
            .Select(t => new { t.Code, t.Name }).ToListAsync(ct);
        var locations = await context.Locations.AsNoTracking().Where(l => !l.IsDeleted)
            .Select(l => new { l.LocationId, l.ParentLocationId, l.Code, l.Name }).ToListAsync(ct);
        var byId = locations.ToDictionary(l => l.LocationId);
        string PathOf(long id)
        {
            var parts = new List<string>();
            for (long? at = id; at is not null && byId.TryGetValue(at.Value, out var l) && parts.Count < 10; at = l.ParentLocationId)
                parts.Insert(0, l.Name);
            return string.Join(", ", parts);
        }
        return new AiCatalog(
            types.Select(t => (t.Code, t.Name)).ToList(),
            locations.OrderBy(l => l.Code).Select(l => (l.Code, PathOf(l.LocationId))).ToList());
    }

    /// <summary>Texto -> código de ubicación por palabras en común. Fuerte = 2+ palabras, una del nombre propio del lugar.</summary>
    public (string Code, bool Strong) MatchLocation(string text)
    {
        var words = AiText.Tokens(text);
        if (words.Count == 0 || Locations.Count == 0) return ("", false);
        var best = Locations
            .Select(l => (l.Code, l.Path, Words: AiText.Tokens(l.Path)))
            .MaxBy(l => l.Words.Count(words.Contains) - 0.5 * l.Words.Count(t => !words.Contains(t)));
        var shared = best.Words.Count(words.Contains);
        if (shared == 0) return ("", false);
        var leaf = AiText.Tokens(best.Path.Split(", ")[^1]);
        return (best.Code, shared >= 2 && leaf.Any(words.Contains));
    }
}

/// <summary>Normalización de texto en español para comparar sin acentos, mayúsculas ni signos.</summary>
public static partial class AiText
{
    private static readonly HashSet<string> Stop = ["de", "del", "la", "el", "en", "los", "las", "y", "ubicada", "ubicado"];
    private static readonly Dictionary<string, string> Synonyms = new()
    {
        ["ed"] = "edificio", ["p1"] = "planta 1", ["p2"] = "planta 2", ["p3"] = "planta 3",
        ["pb"] = "planta baja", ["lab"] = "laboratorio", ["of"] = "oficina",
    };

    /// <summary>Minúsculas, sin acentos y solo letras/números: "S/N: 5CD-12" -> "sn5cd12".</summary>
    public static string Norm(string? text) => new(Plain(text).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Minúsculas y sin acentos, conservando espacios y signos.</summary>
    public static string Plain(string? text)
    {
        var decomposed = (text ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    /// <summary>Palabras sueltas (sin acentos, minúsculas).</summary>
    public static IEnumerable<string> WordsOf(string? text) => WordRegex().Matches(Plain(text)).Select(m => m.Value);

    /// <summary>Palabras significativas con sinónimos de abreviaturas ("ed" -> "edificio") y sin artículos.</summary>
    public static HashSet<string> Tokens(string text) => WordsOf(text)
        .SelectMany(w => (Synonyms.GetValueOrDefault(w) ?? w).Split(' '))
        .Where(t => !Stop.Contains(t)).ToHashSet();

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordRegex();
}
