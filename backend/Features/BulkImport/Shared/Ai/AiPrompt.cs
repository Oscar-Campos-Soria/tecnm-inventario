using System.Text.Json.Nodes;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

/// <summary>Instrucciones y esquema JSON para extraer equipos (ajustados con archivos de prueba: ver specs/bulk-import-ai.spec.md).</summary>
public static class AiPrompt
{
    public static string System(AiCatalog catalog) => $"""
        Lees fragmentos de archivos de inventario de equipo de cómputo (Excel, PDF o documentos escaneados convertidos a texto) y extraes cada equipo.
        Cada línea empieza con su origen entre corchetes, por ejemplo [Hoja1 F12] o [p1 L7].
        Para cada equipo devuelve:
        - codigo_inventario: clave o número de inventario, copiado tal cual
        - tipo: código del tipo de equipo, uno de: {string.Join(", ", catalog.Types.Select(t => $"{t.Code} ({t.Name})"))}
        - fabricante: solo la marca (HP, Dell, Lenovo...)
        - modelo: solo el modelo, sin la marca ni el tipo de equipo ni la serie
        - serie: número de serie, copiado tal cual; vacío si no aparece
        - hostname: nombre del equipo en la red, solo si hay una columna o dato explícito de nombre en red; nunca un título; vacío si no aparece
        Reglas: no inventes datos; si un dato no aparece déjalo vacío. Ignora títulos, totales, firmas y texto que no describa un equipo.
        """;

    public static string User(IEnumerable<SourceLine> lines) =>
        "Fragmento:\n" + string.Join("\n", lines.Select(l => $"[{l.Origin}] {l.Text}"));

    public static JsonObject Schema(AiCatalog catalog)
    {
        var text = () => new JsonObject { ["type"] = "string" };
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("activos"),
            ["properties"] = new JsonObject
            {
                ["activos"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["required"] = new JsonArray("codigo_inventario", "tipo", "fabricante", "modelo", "serie", "hostname"),
                        ["properties"] = new JsonObject
                        {
                            ["codigo_inventario"] = text(),
                            ["tipo"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray(catalog.Types.Select(t => (JsonNode)t.Code).ToArray()),
                            },
                            ["fabricante"] = text(),
                            ["modelo"] = text(),
                            ["serie"] = text(),
                            ["hostname"] = text(),
                        },
                    },
                },
            },
        };
    }

    public static IEnumerable<ExtractedAsset> Parse(JsonNode response) =>
        (response["activos"] as JsonArray ?? []).OfType<JsonObject>().Select(o => new ExtractedAsset
        {
            Code = Str(o, "codigo_inventario"),
            Type = Str(o, "tipo"),
            Manufacturer = Str(o, "fabricante"),
            Model = Str(o, "modelo"),
            Serial = Str(o, "serie"),
            Hostname = Str(o, "hostname"),
        });

    private static string Str(JsonObject o, string key) => (o[key]?.GetValue<string>() ?? "").Trim();
}
