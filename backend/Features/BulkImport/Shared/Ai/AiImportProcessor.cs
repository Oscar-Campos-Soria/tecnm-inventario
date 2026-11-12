using ClosedXML.Excel;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.BulkImport.GetImportTemplate;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

/// <summary>
/// Archivo "como venga" -> plantilla oficial llena + columnas origen/revisar. No escribe en la BD:
/// el usuario revisa la plantilla y la sube por la carga masiva normal (preview -> commit, auditada).
/// </summary>
public sealed class AiImportProcessor
{
    public const int LinesPerChunk = 20;
    public const int MaxChunks = 30;
    public const int MaxAssets = 300;

    private readonly ILocalAiClient _ai;
    private readonly TecnmInventarioDbContext _context;
    private readonly AssetImportChecker _checker;

    public AiImportProcessor(ILocalAiClient ai, TecnmInventarioDbContext context, AssetImportChecker checker)
    {
        _ai = ai;
        _context = context;
        _checker = checker;
    }

    public async Task ProcessAsync(AiImportJob job, CancellationToken ct)
    {
        var file = job.File ?? throw new InvalidOperationException("El trabajo ya no tiene archivo.");
        var catalog = await AiCatalog.LoadAsync(_context, ct);
        var document = SourceReader.Read(file);

        var blocks = document.Blocks.ToList();
        for (var i = 0; i < document.Images.Count; i++)
        {
            var image = document.Images[i];
            job.Progress($"Leyendo imagen {i + 1} de {document.Images.Count} ({image.Name})", 0, 0, 0);
            blocks.Add(SourceReader.FromText(image.Page, await _ai.TranscribeAsync(image.Content, image.MimeType, ct)));
        }

        var chunks = Chunk(blocks).ToList();
        if (chunks.Count == 0) throw new BusinessRuleViolationException("No se encontró texto en el archivo.");
        if (chunks.Count > MaxChunks)
            throw new BusinessRuleViolationException(
                $"El archivo es demasiado grande para la IA (máximo unas {MaxChunks * LinesPerChunk} líneas). Divídelo en partes.");

        var system = AiPrompt.System(catalog);
        var schema = AiPrompt.Schema(catalog);
        var found = new List<ExtractedAsset>();
        for (var c = 0; c < chunks.Count && found.Count < MaxAssets; c++)
        {
            job.Progress($"Leyendo bloque {c + 1} de {chunks.Count}", c, chunks.Count, found.Count);
            var (block, lines) = chunks[c];
            var response = await _ai.CompleteJsonAsync(system, AiPrompt.User(lines), schema, ct);
            foreach (var asset in AiPrompt.Parse(response))
            {
                if (!ExtractionRules.Apply(asset, block, catalog)) continue;
                if (found.Any(f => f.Origin == asset.Origin && ExtractionRules.Norm(f.Code) == ExtractionRules.Norm(asset.Code))) continue;
                found.Add(asset);
            }
        }
        if (found.Count > MaxAssets)
        {
            found = found.Take(MaxAssets).ToList();
            found[^1].Warnings.Add($"Se llegó al máximo de {MaxAssets} equipos por archivo; el resto no se leyó.");
        }

        // Mismas validaciones que la carga masiva: el usuario ve antes de subir qué va a fallar.
        var rows = found.Select((a, i) => new ImportRow(i + 2, new Dictionary<string, string?>
        {
            [AssetImportParser.Code] = Blank(a.Code), [AssetImportParser.Type] = Blank(a.Type),
            [AssetImportParser.Manufacturer] = Blank(a.Manufacturer), [AssetImportParser.Model] = Blank(a.Model),
            [AssetImportParser.Serial] = Blank(a.Serial), [AssetImportParser.Hostname] = Blank(a.Hostname),
            [AssetImportParser.Location] = Blank(a.Location),
        })).ToList();
        var checkedRows = await _checker.CheckAsync(rows, ct);
        for (var i = 0; i < found.Count; i++) found[i].Warnings.AddRange(checkedRows[i].Errors);

        var result = await BuildWorkbookAsync(found, file.FileName, ct);
        job.Complete(result, found.Count, found.Count(a => a.Warnings.Count > 0));
    }

    /// <summary>Bloques de 20 líneas; los que continúan una hoja repiten su título y encabezados.</summary>
    private static IEnumerable<(SourceBlock Block, List<SourceLine> Lines)> Chunk(IEnumerable<SourceBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var head = block.Lines.Take(LinesPerChunk).Where(l => l.IsHeader || l.Text.StartsWith("=== Hoja", StringComparison.Ordinal)).ToList();
            for (var i = 0; i < block.Lines.Count; i += LinesPerChunk)
                yield return (block, (i == 0 ? [] : head).Concat(block.Lines.Skip(i).Take(LinesPerChunk)).ToList());
        }
    }

    private async Task<GeneratedFile> BuildWorkbookAsync(List<ExtractedAsset> assets, string sourceName, CancellationToken ct)
    {
        // Parte de la plantilla oficial (encabezados + hoja Catálogos) para que se suba tal cual.
        var template = await new GetImportTemplateHandler(_context).Handle(new GetImportTemplateQuery(), ct);
        using var book = new XLWorkbook(new MemoryStream(template.Content));
        var sheet = book.Worksheet("Activos");
        sheet.Rows(2, 4).Clear();

        var origin = AssetImportParser.Columns.Length + 1;
        sheet.Cell(1, origin).Value = "origen";
        sheet.Cell(1, origin + 1).Value = "revisar";
        sheet.Range(1, origin, 1, origin + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        for (var i = 0; i < assets.Count; i++)
        {
            var a = assets[i];
            var row = i + 2;
            string[] values = [a.Code, a.Type, a.Manufacturer, a.Model, a.Serial, a.Hostname, a.Location, ""];
            for (var c = 0; c < values.Length; c++) sheet.Cell(row, c + 1).SetValue(values[c]);
            sheet.Cell(row, origin).SetValue($"{sourceName} · {a.Origin}");
            sheet.Cell(row, origin + 1).SetValue(string.Join(" ", a.Warnings));
            if (a.Warnings.Count > 0) sheet.Row(row).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFF4CE"));
        }
        // Como comentario y no como fila: el parser leería una fila de nota como un equipo más.
        sheet.Cell(1, origin + 1).CreateComment().AddText(
            "Generado con IA: revisa cada fila (las amarillas tienen avisos aquí), corrige y sube este archivo en Carga masiva. Las columnas origen y revisar se ignoran al subir.");
        sheet.Columns().AdjustToContents(1, assets.Count + 1, 10, 60);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        var name = Path.GetFileNameWithoutExtension(sourceName);
        return new GeneratedFile(stream.ToArray(), GeneratedFile.Xlsx, $"plantilla-ia-{name}.xlsx");
    }

    private static string? Blank(string value) => value.Length == 0 ? null : value;
}
