using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using TecnmInventario.Api.Common.Errors;

namespace TecnmInventario.Api.Features.BulkImport.Shared;

/// <summary>Una fila del archivo, con los valores por columna conocida.</summary>
public sealed record ImportRow(int RowNumber, IReadOnlyDictionary<string, string?> Values)
{
    public string? this[string column] => Values.TryGetValue(column, out var v) ? v : null;
}

/// <summary>Lee .xlsx (primera hoja) o .csv (coma o punto y coma, UTF-8) con encabezados en la fila 1.</summary>
public static class AssetImportParser
{
    public const string Code = "codigo_inventario";
    public const string Type = "tipo";
    public const string Manufacturer = "fabricante";
    public const string Model = "modelo";
    public const string Serial = "serie";
    public const string Hostname = "hostname";
    public const string Location = "ubicacion";
    public const string Notes = "notas";

    public static readonly string[] Columns = { Code, Type, Manufacturer, Model, Serial, Hostname, Location, Notes };

    public static IReadOnlyList<ImportRow> Parse(ImportFile file)
    {
        var table = file.Extension switch
        {
            ".xlsx" => ReadXlsx(file.Content),
            ".csv" => ReadCsv(file.Content),
            _ => throw new BusinessRuleViolationException("Sube un archivo .xlsx o .csv.")
        };

        if (table.Count == 0)
            throw new BusinessRuleViolationException("El archivo está vacío.");

        var header = table[0].Select(Normalize).ToList();
        var missing = new[] { Code, Type }.Where(c => !header.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleViolationException(
                $"Faltan columnas obligatorias: {string.Join(", ", missing)}. Descarga la plantilla para ver el formato.");

        var rows = new List<ImportRow>();
        for (var i = 1; i < table.Count; i++)
        {
            var cells = table[i];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            var values = new Dictionary<string, string?>();
            for (var c = 0; c < header.Count && c < cells.Count; c++)
            {
                if (Columns.Contains(header[c]))
                    values[header[c]] = string.IsNullOrWhiteSpace(cells[c]) ? null : cells[c].Trim();
            }
            rows.Add(new ImportRow(i + 1, values));
        }

        if (rows.Count == 0)
            throw new BusinessRuleViolationException("El archivo no tiene filas de datos debajo de los encabezados.");
        if (rows.Count > ImportFile.MaxRows)
            throw new BusinessRuleViolationException($"El archivo tiene {rows.Count} filas; el máximo es {ImportFile.MaxRows} por carga.");
        return rows;
    }

    // "Código Inventario" -> "codigo_inventario": sin acentos, minúsculas, espacios a guion bajo.
    private static string Normalize(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new string(decomposed.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        return plain.Replace(' ', '_').Replace('-', '_');
    }

    private static List<List<string>> ReadXlsx(byte[] content)
    {
        try
        {
            using var book = new XLWorkbook(new MemoryStream(content));
            var sheet = book.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used is null) return new();
            var lastColumn = used.LastColumn().ColumnNumber();
            return used.Rows()
                .Select(r => Enumerable.Range(1, lastColumn).Select(c => r.Cell(c).GetFormattedString()).ToList())
                .ToList();
        }
        catch (Exception ex) when (ex is not BusinessRuleViolationException)
        {
            throw new BusinessRuleViolationException("No se pudo leer el Excel. Guárdalo como .xlsx e intenta de nuevo.");
        }
    }

    private static List<List<string>> ReadCsv(byte[] content)
    {
        var text = new UTF8Encoding(false).GetString(content).TrimStart('\uFEFF');
        var firstLine = text.Split('\n', 2)[0];
        var separator = firstLine.Count(ch => ch == ';') > firstLine.Count(ch => ch == ',') ? ';' : ',';

        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == separator) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n')
            {
                row.Add(cell.ToString().TrimEnd('\r'));
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().TrimEnd('\r'));
            rows.Add(row);
        }
        return rows;
    }
}
