using System.Text.RegularExpressions;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

/// <summary>Un equipo encontrado por el modelo, después de las reglas.</summary>
public sealed class ExtractedAsset
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Location { get; set; } = "";
    public string Origin { get; set; } = "";
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Lo que el modelo hace mal y el código hace bien (medido con archivos de prueba): de qué fila salió el
/// equipo, la ubicación (título de sección más cercano contra el catálogo), datos que el modelo inventó
/// y columnas explícitas que mandan sobre lo que dijo el modelo.
/// </summary>
public static partial class ExtractionRules
{
    // shortcut: palabras por código de tipo del catálogo sembrado; un tipo nuevo no se corrige solo, agrégalo aquí.
    private static readonly Dictionary<string, string[]> TypeWords = new()
    {
        ["LAPTOP"] = ["laptop", "portatil"], ["PC"] = ["cpu", "escritorio", "desktop"], ["MONITOR"] = ["monitor"],
        ["PRINTER"] = ["impresora", "multifuncional"], ["PROJECTOR"] = ["proyector"], ["SWITCH"] = ["switch"],
        ["ROUTER"] = ["router", "ruteador"], ["UPS"] = ["no break", "nobreak", "ups"],
        ["ACCESS_POINT"] = ["punto de acceso", "access point"], ["SERVER"] = ["servidor"], ["CAMERA"] = ["camara"],
    };

    private static readonly (string Field, Regex Header)[] ColumnRules =
    [
        ("code", HeaderCode()), ("serial", HeaderSerial()), ("hostname", HeaderHost()),
    ];

    /// <summary>Aplica todas las reglas. Devuelve false si el equipo no se pudo ubicar en el bloque (se descarta).</summary>
    public static bool Apply(ExtractedAsset a, SourceBlock block, AiCatalog catalog)
    {
        var index = FindOrigin(a, block);
        if (index < 0) return false;
        var line = block.Lines[index];
        a.Origin = line.Origin;

        // En PDF/imagen un equipo puede ocupar varias líneas; en Excel la fila está completa.
        var near = block.RowBased
            ? [line]
            : block.Lines.Skip(Math.Max(0, index - 1)).Take(index == 0 ? 2 : 3).ToList();
        var nearText = string.Join(" ", near.Select(l => l.Text));
        var nearNorm = Norm(nearText);

        // Columnas explícitas mandan sobre el modelo (ej. tomó la serie como código).
        if (line.Cells is not null)
        {
            foreach (var (field, header) in ColumnRules)
            {
                var value = line.Cells.FirstOrDefault(c => header.IsMatch(AiText.Plain(c.Key))).Value;
                if (value is null || Norm(value) == Norm(Get(a, field))) continue;
                if (Get(a, field).Length > 0) a.Warnings.Add($"{Label(field)} corregido a «{value}» según su columna.");
                Set(a, field, value);
            }
        }

        // Anti-alucinación: lo copiado tiene que estar en su fila.
        foreach (var field in new[] { "code", "serial", "manufacturer", "model" })
        {
            var value = Get(a, field);
            if (value.Length == 0 || nearNorm.Contains(Norm(value))) continue;
            a.Warnings.Add($"{Label(field)} «{value}» no aparece en el archivo; se dejó vacío.");
            Set(a, field, "");
        }
        // Hostname: el modelo suele poner títulos o repetir el código; se descarta sin ruido.
        if (a.Hostname.Length > 0 && (!nearNorm.Contains(Norm(a.Hostname))
            || Norm(a.Hostname) == Norm(a.Code) || Norm(a.Hostname) == Norm(a.Serial)))
            a.Hostname = "";

        FixType(a, line.Text, catalog);

        var (code, strong) = catalog.MatchLocation(nearText);
        if (strong) a.Location = code;
        else
        {
            for (var i = index - 1; i >= 0 && a.Location.Length == 0; i--)
            {
                (code, strong) = catalog.MatchLocation(block.Lines[i].Text);
                if (!strong) continue;
                a.Location = code;
                a.Warnings.Add($"Ubicación tomada del título «{Shorten(block.Lines[i].Text)}».");
            }
            if (a.Location.Length == 0) a.Warnings.Add("No se encontró la ubicación.");
        }
        return true;
    }

    /// <summary>Línea del bloque que contiene el código o la serie del equipo (el modelo copia mal el origen).</summary>
    public static int FindOrigin(ExtractedAsset a, SourceBlock block)
    {
        var keys = new[] { a.Code, a.Serial }.Select(Norm).Where(k => k.Length > 0).ToList();
        if (keys.Count == 0) return -1;
        for (var i = 0; i < block.Lines.Count; i++)
        {
            var text = Norm(block.Lines[i].Text);
            if (!block.Lines[i].IsHeader && keys.Any(text.Contains)) return i;
        }
        return -1;
    }

    private static void FixType(ExtractedAsset a, string rowText, AiCatalog catalog)
    {
        // Solo palabras: en Excel el texto viene como Descripción="Switch …".
        var plain = " " + string.Join(" ", AiText.WordsOf(rowText)) + " ";
        var hits = TypeWords
            .Where(t => catalog.Types.Any(c => c.Code == t.Key) && t.Value.Any(w => plain.Contains(" " + w)))
            .Select(t => t.Key).ToList();
        if (hits.Count != 1 || hits[0] == a.Type) return;
        if (a.Type.Length > 0) a.Warnings.Add($"Tipo corregido a {hits[0]} por el texto de la fila.");
        a.Type = hits[0];
    }

    public static string Norm(string? text) => AiText.Norm(text);

    private static string Shorten(string text)
    {
        var clean = text.Replace("\"", "").Trim();
        var eq = clean.IndexOf('=');
        if (eq > 0 && eq < 12) clean = clean[(eq + 1)..];
        return clean.Length > 60 ? clean[..60] + "…" : clean;
    }

    private static string Label(string field) => field switch
    {
        "code" => "Código", "serial" => "Serie", "hostname" => "Hostname", "manufacturer" => "Fabricante", _ => "Modelo",
    };

    private static string Get(ExtractedAsset a, string field) => field switch
    {
        "code" => a.Code, "serial" => a.Serial, "hostname" => a.Hostname, "manufacturer" => a.Manufacturer, _ => a.Model,
    };

    private static void Set(ExtractedAsset a, string field, string value)
    {
        switch (field)
        {
            case "code": a.Code = value; break;
            case "serial": a.Serial = value; break;
            case "hostname": a.Hostname = value; break;
            case "manufacturer": a.Manufacturer = value; break;
            default: a.Model = value; break;
        }
    }

    [GeneratedRegex(@"inv|clave|codigo")]
    private static partial Regex HeaderCode();

    [GeneratedRegex(@"serie|s/n|n/s|serial")]
    private static partial Regex HeaderSerial();

    [GeneratedRegex(@"\bred\b|host")]
    private static partial Regex HeaderHost();
}
