using System.Globalization;
using ClosedXML.Excel;
using TecnmInventario.Api.Common.Errors;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

/// <summary>Una línea del archivo con su origen ("Hoja1 F12", "p2 L7"). En filas de tabla, celdas por encabezado.</summary>
public sealed record SourceLine(string Origin, string Text, IReadOnlyDictionary<string, string>? Cells = null, bool IsHeader = false);

/// <summary>Una hoja, el CSV o el texto de un PDF/imagen. RowBased: cada fila trae completo su equipo (Excel/CSV).</summary>
public sealed record SourceBlock(string Name, IReadOnlyList<SourceLine> Lines, bool RowBased);

/// <summary>Página escaneada o foto: el modelo la transcribe antes de extraer.</summary>
public sealed record SourceImage(string Name, int Page, byte[] Content, string MimeType);

public sealed record SourceDocument(IReadOnlyList<SourceBlock> Blocks, IReadOnlyList<SourceImage> Images);

/// <summary>Archivo -> líneas con origen. Sin IA: el modelo solo ve este texto.</summary>
public static class SourceReader
{
    public const int MaxPdfPages = 10;
    // Menos letras que esto en una página = escaneada (solo trae la imagen).
    private const int MinTextChars = 20;

    public static readonly string[] Extensions = [".xlsx", ".csv", ".pdf", ".jpg", ".jpeg", ".png"];

    public static SourceDocument Read(ImportFile file) => file.Extension switch
    {
        ".xlsx" => new(ReadXlsx(file.Content), []),
        ".csv" => new([TableBlock("CSV", AssetImportParser.ReadCsv(file.Content)
            .Select((cells, i) => (Row: i + 1, Cells: cells.Select(c => (Text: c, IsText: !IsNumber(c))).ToList())))], []),
        ".pdf" => ReadPdf(file.Content),
        ".jpg" or ".jpeg" => new([], [new SourceImage(file.FileName, 1, file.Content, "image/jpeg")]),
        ".png" => new([], [new SourceImage(file.FileName, 1, file.Content, "image/png")]),
        _ => throw new BusinessRuleViolationException("Sube un archivo .xlsx, .csv, .pdf, .jpg o .png."),
    };

    /// <summary>Texto transcrito de una imagen -> bloque como PDF (un equipo puede ocupar varias líneas).</summary>
    public static SourceBlock FromText(int page, string text) => new($"p{page}",
        text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0)
            .Select((l, i) => new SourceLine($"p{page} L{i + 1}", l.Trim())).ToList(), RowBased: false);

    private static List<SourceBlock> ReadXlsx(byte[] content)
    {
        try
        {
            using var book = new XLWorkbook(new MemoryStream(content));
            return book.Worksheets
                .Select(sheet => TableBlock(sheet.Name, (sheet.RangeUsed()?.Rows() ?? Enumerable.Empty<IXLRangeRow>())
                    .Select(r => (Row: r.RowNumber(), Cells: r.Cells()
                        .Select(c => (Text: c.GetFormattedString(), IsText: c.DataType == XLDataType.Text)).ToList()))))
                .Where(b => b.Lines.Count > 1)
                .ToList();
        }
        catch (Exception ex) when (ex is not BusinessRuleViolationException)
        {
            throw new BusinessRuleViolationException("No se pudo leer el Excel. Guárdalo como .xlsx e intenta de nuevo.");
        }
    }

    /// <summary>
    /// Filas de una hoja. Encabezado = primera fila con 3+ celdas de texto; las filas siguientes se
    /// etiquetan con él (Clave="TEC-1") para que el modelo no confunda columnas en otro orden.
    /// </summary>
    private static SourceBlock TableBlock(string name, IEnumerable<(int Row, List<(string Text, bool IsText)> Cells)> rows)
    {
        var lines = new List<SourceLine> { new(name, $"=== Hoja: {name} ===") };
        List<string>? header = null;
        foreach (var (row, cells) in rows)
        {
            var filled = cells.Select((c, i) => (c.Text.Trim(), c.IsText, i)).Where(c => c.Item1.Length > 0).ToList();
            if (filled.Count == 0) continue;
            var origin = $"{name} F{row}";
            if (header is null && filled.Count >= 3 && filled.All(c => c.IsText))
            {
                header = cells.Select(c => c.Text.Trim()).ToList();
                lines.Add(new SourceLine(origin, "(encabezados) " + string.Join(" | ", filled.Select(c => c.Item1)), IsHeader: true));
                continue;
            }
            var labeled = filled.Select(c => (Key: header is not null && c.i < header.Count && header[c.i].Length > 0
                ? header[c.i] : ColumnLetter(c.i), Value: c.Item1)).ToList();
            var dict = new Dictionary<string, string>();
            foreach (var (key, value) in labeled) dict.TryAdd(key, value);
            lines.Add(new SourceLine(origin, string.Join(" ", labeled.Select(c => $"{c.Key}=\"{c.Value}\"")), dict));
        }
        return new SourceBlock(name, lines, RowBased: true);
    }

    private static SourceDocument ReadPdf(byte[] content)
    {
        try
        {
            using var pdf = PdfDocument.Open(content);
            if (pdf.NumberOfPages > MaxPdfPages)
                throw new BusinessRuleViolationException($"El PDF tiene {pdf.NumberOfPages} páginas; el máximo es {MaxPdfPages}.");

            var blocks = new List<SourceBlock>();
            var images = new List<SourceImage>();
            foreach (var page in pdf.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page);
                if (text.Count(char.IsLetterOrDigit) >= MinTextChars)
                {
                    blocks.Add(FromText(page.Number, text));
                    continue;
                }
                // Página escaneada: la imagen más grande. JPEG se manda tal cual; lo demás, como PNG.
                var image = page.GetImages().OrderByDescending(i => i.BoundingBox.Width * i.BoundingBox.Height).FirstOrDefault();
                if (image is null) continue;
                var raw = image.RawBytes.ToArray();
                if (raw.Length > 2 && raw[0] == 0xFF && raw[1] == 0xD8)
                    images.Add(new SourceImage($"página {page.Number}", page.Number, raw, "image/jpeg"));
                else if (image.TryGetPng(out var png))
                    images.Add(new SourceImage($"página {page.Number}", page.Number, png, "image/png"));
            }
            return new SourceDocument(blocks, images);
        }
        catch (Exception ex) when (ex is not BusinessRuleViolationException)
        {
            throw new BusinessRuleViolationException("No se pudo leer el PDF. Puede estar protegido o dañado.");
        }
    }

    private static bool IsNumber(string text) =>
        double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    private static string ColumnLetter(int index)
    {
        var letters = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) letters = (char)('A' + (n - 1) % 26) + letters;
        return letters;
    }
}
