using Mediator;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Unit = QuestPDF.Infrastructure.Unit;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.GetAssetLabels;

/// <summary>
/// Hoja carta de etiquetas: 3 columnas x 10 filas (formato tipo 5160, 6.7 x 2.5 cm).
/// El QR lleva el código de inventario, lo mismo que lee el levantamiento físico.
/// </summary>
public sealed class GetAssetLabelsHandler : IQueryHandler<GetAssetLabelsQuery, GeneratedFile>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetLabelsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GeneratedFile> Handle(GetAssetLabelsQuery query, CancellationToken cancellationToken)
    {
        var ids = query.AssetIds.Distinct().ToList();
        var assets = await _context.Assets.AsNoTracking()
            .Where(a => ids.Contains(a.AssetId) && !a.IsDeleted)
            .OrderBy(a => a.InventoryCode)
            .Select(a => new { a.InventoryCode, Type = a.AssetType.Name, a.SerialNumber })
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            throw new KeyNotFoundException("Ninguno de los activos elegidos existe.");
        }

        using var generator = new QRCodeGenerator();
        var labels = assets.Select(a =>
        {
            using var data = generator.CreateQrCode(a.InventoryCode, QRCodeGenerator.ECCLevel.M);
            return (a.InventoryCode, a.Type, a.SerialNumber, Qr: new PngByteQRCode(data).GetGraphic(10));
        }).ToList();

        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginVertical(1.27f, Unit.Centimetre);
            page.MarginHorizontal(0.48f, Unit.Centimetre);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(0.32f, Unit.Centimetre);
                    c.RelativeColumn();
                    c.ConstantColumn(0.32f, Unit.Centimetre);
                    c.RelativeColumn();
                });

                for (var i = 0; i < labels.Count; i++)
                {
                    var l = labels[i];
                    table.Cell().Height(2.54f, Unit.Centimetre).Padding(4).Row(row =>
                    {
                        row.ConstantItem(1.9f, Unit.Centimetre).AlignMiddle().Image(l.Qr);
                        row.RelativeItem().PaddingLeft(5).AlignMiddle().Column(col =>
                        {
                            col.Item().Text(PdfLayout.Institution).FontSize(5.5f).FontColor(Colors.Grey.Darken2);
                            col.Item().Text(l.InventoryCode).FontSize(10).Bold().FontColor(PdfLayout.Navy);
                            col.Item().Text(l.Type).FontSize(7);
                            if (!string.IsNullOrWhiteSpace(l.SerialNumber))
                                col.Item().Text($"S/N {l.SerialNumber}").FontSize(6).FontColor(Colors.Grey.Darken2);
                        });
                    });
                    // Separadores entre columnas (formato de hoja de etiquetas).
                    if (i % 3 != 2) table.Cell();
                }
            });
        })).GeneratePdf();

        return new GeneratedFile(pdf, GeneratedFile.Pdf, $"etiquetas-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }
}
