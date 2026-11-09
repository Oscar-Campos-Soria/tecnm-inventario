using ClosedXML.Excel;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.GetImportTemplate;

/// <summary>Plantilla .xlsx: hoja Activos con los encabezados y un ejemplo, y hoja Catálogos de referencia.</summary>
public sealed class GetImportTemplateHandler : IQueryHandler<GetImportTemplateQuery, GeneratedFile>
{
    private readonly TecnmInventarioDbContext _context;

    public GetImportTemplateHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GeneratedFile> Handle(GetImportTemplateQuery query, CancellationToken cancellationToken)
    {
        var types = await _context.AssetTypes.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Name)
            .Select(t => new { t.Code, t.Name }).ToListAsync(cancellationToken);
        var locations = await _context.Locations.AsNoTracking().OrderBy(l => l.Code)
            .Select(l => new { l.Code, l.Name }).ToListAsync(cancellationToken);
        var manufacturers = await _context.Manufacturers.AsNoTracking().Where(m => !m.IsDeleted).OrderBy(m => m.Name)
            .Select(m => m.Name).ToListAsync(cancellationToken);

        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Activos");
        for (var c = 0; c < AssetImportParser.Columns.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = AssetImportParser.Columns[c];
            sheet.Cell(1, c + 1).Style.Font.SetBold();
        }
        sheet.Cell(1, 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
        sheet.Cell(1, 2).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
        var example = new[] { "TEC-0001", types.FirstOrDefault()?.Code ?? "PC", manufacturers.FirstOrDefault() ?? "", "", "SN123456", "lab-1-pc01", locations.FirstOrDefault()?.Code ?? "", "Borra esta fila de ejemplo" };
        for (var c = 0; c < example.Length; c++) sheet.Cell(2, c + 1).Value = example[c];
        sheet.Cell(4, 1).Value = "Amarillo = obligatorio. tipo: código o nombre; ubicacion: código; fabricante y modelo: nombre exacto.";
        sheet.Cell(4, 1).Style.Font.SetItalic();
        sheet.Columns().AdjustToContents();

        var catalogs = book.Worksheets.Add("Catálogos");
        catalogs.Cell(1, 1).Value = "Tipo (código)"; catalogs.Cell(1, 2).Value = "Tipo (nombre)";
        catalogs.Cell(1, 4).Value = "Ubicación (código)"; catalogs.Cell(1, 5).Value = "Ubicación (nombre)";
        catalogs.Cell(1, 7).Value = "Fabricante";
        catalogs.Row(1).Style.Font.SetBold();
        for (var i = 0; i < types.Count; i++) { catalogs.Cell(i + 2, 1).Value = types[i].Code; catalogs.Cell(i + 2, 2).Value = types[i].Name; }
        for (var i = 0; i < locations.Count; i++) { catalogs.Cell(i + 2, 4).Value = locations[i].Code; catalogs.Cell(i + 2, 5).Value = locations[i].Name; }
        for (var i = 0; i < manufacturers.Count; i++) catalogs.Cell(i + 2, 7).Value = manufacturers[i];
        catalogs.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return new GeneratedFile(stream.ToArray(), GeneratedFile.Xlsx, "plantilla-carga-activos.xlsx");
    }
}
