using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>
/// Prefiltro sin modelo: ordena las capacidades del rol por parecido entre el mensaje y su texto (nombre, resumen,
/// descripción) con trigramas de caracteres ponderados por rareza, que toleran errores de dedo y palabras sin acento.
/// Solo acota la lista: quien entiende y elige sigue siendo el 2B entre las primeras. Si nada se parece, no recorta.
/// </summary>
public static class ToolPrefilter
{
    public const int Keep = 4;

    public static IReadOnlyList<IAssistantTool> Rank(IReadOnlyList<IAssistantTool> tools, string text)
    {
        if (tools.Count <= Keep) return tools;
        var docs = tools.Select(t => Grams($"{t.Name.Replace('_', ' ')} {t.Summary} {t.Description}")).ToList();
        // Rareza: un trigrama que aparece en todas las capacidades no distingue nada.
        var df = docs.SelectMany(d => d).GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
        var query = Grams(text);
        var scores = docs.Select(d => query.Where(d.Contains).Sum(g => Math.Log((double)tools.Count / df[g]))).ToList();
        if (scores.Max() <= 0) return tools;
        return tools.Select((t, i) => (Tool: t, Score: scores[i], Index: i))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Index).Take(Keep)
            .OrderBy(x => x.Index) // orden del registro: mismo prefijo de prompt, mejor caché
            .Select(x => x.Tool).ToList();
    }

    internal static HashSet<string> Grams(string text)
    {
        var grams = new HashSet<string>();
        foreach (var word in AiText.WordsOf(text).Where(w => w.Length >= 3))
        {
            var padded = $" {word} ";
            for (var i = 0; i + 3 <= padded.Length; i++) grams.Add(padded.Substring(i, 3));
        }
        return grams;
    }
}
