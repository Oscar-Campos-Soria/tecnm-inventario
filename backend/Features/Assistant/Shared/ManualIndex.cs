using System.Reflection;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>Una sección del manual: archivo, título de la sección y su texto.</summary>
public sealed record ManualSection(string Source, string Title, string Text);

/// <summary>
/// RAG sin dependencias: el manual (Features/Assistant/Manual/*.md, embebido en la API) se parte por secciones "## "
/// y se busca con BM25 sobre palabras normalizadas. Solo los 2-3 fragmentos relevantes llegan al modelo.
/// shortcut: índice en memoria; con cientos de páginas o edición desde pantalla, pasar a full-text de Postgres.
/// </summary>
public sealed class ManualIndex
{
    private const double K1 = 1.5, B = 0.75;
    private readonly List<(ManualSection Section, Dictionary<string, int> Terms, int Length)> _docs = [];
    private readonly Dictionary<string, int> _docFreq = [];
    private readonly double _avgLength;

    public ManualIndex() : this(LoadEmbedded()) { }

    // internal: con dos constructores públicos, la inyección de dependencias elegía este con una lista vacía.
    internal ManualIndex(IEnumerable<ManualSection> sections)
    {
        foreach (var section in sections)
        {
            var terms = Terms(section.Title + " " + section.Title + " " + section.Text) // el título pesa doble
                .GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            _docs.Add((section, terms, terms.Values.Sum()));
            foreach (var term in terms.Keys) _docFreq[term] = _docFreq.GetValueOrDefault(term) + 1;
        }
        _avgLength = _docs.Count == 0 ? 1 : _docs.Average(d => d.Length);
    }

    public int Count => _docs.Count;

    public IReadOnlyList<ManualSection> Search(string query, int top = 3)
    {
        var terms = QueryTerms(query);
        return _docs
            .Select(d => (d.Section, Score: terms.Sum(t =>
            {
                if (!d.Terms.TryGetValue(t, out var tf)) return 0;
                var idf = Math.Log(1 + (_docs.Count - _docFreq[t] + 0.5) / (_docFreq[t] + 0.5));
                return idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * d.Length / _avgLength));
            })))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(top)
            .Select(r => r.Section)
            .ToList();
    }

    /// <summary>Palabras significativas con un recorte simple de plurales y conjugaciones comunes.</summary>
    private static IEnumerable<string> Terms(string text) => AiText.Tokens(text).Select(Stem).Where(t => t.Length > 2);

    /// <summary>
    /// Términos de la pregunta: sin palabras vacías de pregunta y con sinónimos agregados (no reemplazados).
    /// Los sinónimos van solo en la pregunta: aplicados al manual, "registrar el movimiento" contaba como "alta".
    /// </summary>
    private static List<string> QueryTerms(string query) => AiText.Tokens(query)
        .Where(t => !QueryStop.Contains(t))
        .SelectMany(t => Synonyms.TryGetValue(t, out var syn) ? new[] { t, syn } : [t])
        .Select(Stem).Where(t => t.Length > 2).Distinct().ToList();

    private static readonly HashSet<string> QueryStop =
    [
        "como", "que", "cual", "cuales", "quien", "donde", "cuando", "un", "una", "unos", "otro", "otra", "puedo", "hago",
        "hacer", "se", "me", "mi", "mis", "a", "al", "con", "por", "para", "es", "son", "hay", "sistema", "si",
    ];

    // Cómo pregunta la gente -> cómo lo dice el manual.
    private static readonly Dictionary<string, string> Synonyms = new()
    {
        ["equipo"] = "activo", ["equipos"] = "activos", ["muevo"] = "mover", ["movimiento"] = "mover", ["cambio"] = "mover",
        ["registro"] = "registrar", ["registre"] = "registrar", ["nuevo"] = "alta", ["nuevos"] = "alta", ["agregar"] = "alta",
        ["doy"] = "dar", ["asigno"] = "asignar", ["presto"] = "asignar", ["password"] = "contrasena",
        ["tecnico"] = "roles", ["tecnicos"] = "roles", ["administrador"] = "roles", ["responsable"] = "roles", ["rol"] = "roles",
    };

    // shortcut: raíz aproximada para español (dar/doy/da no se unen); suficiente para un manual chico.
    private static string Stem(string word)
    {
        foreach (var suffix in new[] { "aciones", "acion", "ando", "iendo", "ados", "adas", "ado", "ada", "ar", "er", "ir", "es", "s" })
            if (word.Length > suffix.Length + 3 && word.EndsWith(suffix, StringComparison.Ordinal))
                return word[..^suffix.Length];
        return word;
    }

    public static IEnumerable<ManualSection> Parse(string source, string markdown)
    {
        string? title = null;
        var body = new List<string>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (title is not null) yield return new ManualSection(source, title, string.Join(" ", body).Trim());
                title = line[3..].Trim();
                body.Clear();
            }
            else if (title is not null && !line.StartsWith('#')) body.Add(line.Trim());
        }
        if (title is not null) yield return new ManualSection(source, title, string.Join(" ", body).Trim());
    }

    private static IEnumerable<ManualSection> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Assistant.Manual.") && n.EndsWith(".md")))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var file = name[(name.IndexOf(".Assistant.Manual.", StringComparison.Ordinal) + ".Assistant.Manual.".Length)..];
            foreach (var section in Parse(file, reader.ReadToEnd())) yield return section;
        }
    }
}
