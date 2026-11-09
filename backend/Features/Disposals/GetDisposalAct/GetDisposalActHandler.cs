using Mediator;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Unit = QuestPDF.Infrastructure.Unit;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Disposals.GetDisposalAct;

/// <summary>
/// Acta de baja en PDF, generada al vuelo con los datos de la baja ya registrada
/// (solo lectura: no escribe en audit_log). Deja espacio para las firmas.
/// </summary>
public sealed class GetDisposalActHandler : IQueryHandler<GetDisposalActQuery, GeneratedFile>
{
    private readonly TecnmInventarioDbContext _context;

    public GetDisposalActHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GeneratedFile> Handle(GetDisposalActQuery query, CancellationToken cancellationToken)
    {
        var d = await _context.AssetDisposals.AsNoTracking()
            .Where(x => x.AssetDisposalId == query.AssetDisposalId)
            .Select(x => new
            {
                x.Folio,
                x.DisposedAt,
                x.Dictamen,
                Reason = x.DisposalReason.Name,
                x.Asset.InventoryCode,
                AssetType = x.Asset.AssetType.Name,
                Manufacturer = x.Asset.Manufacturer != null ? x.Asset.Manufacturer.Name : null,
                Model = x.Asset.Model != null ? x.Asset.Model.ModelName : null,
                x.Asset.SerialNumber,
                x.Asset.Hostname,
                Location = x.Asset.CurrentLocation != null ? x.Asset.CurrentLocation.Name : null,
                RegisteredBy = x.CreatedByNavigation != null
                    ? x.CreatedByNavigation.FirstName + " " + x.CreatedByNavigation.LastName : null,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("La baja solicitada no existe.");

        var now = DateTime.UtcNow;
        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(10.5f));
            page.Header().Element(c => PdfLayout.Header(c, "Acta de baja de activo", $"Folio {d.Folio}"));
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(14);
                col.Item().Text(t =>
                {
                    t.Span("El ");
                    t.Span($"{d.DisposedAt:dd/MM/yyyy}").Bold();
                    t.Span(" se da de baja definitiva del inventario el activo descrito a continuación, por el motivo ");
                    t.Span(d.Reason).Bold();
                    t.Span(". A partir de esta fecha el equipo no se asigna, mueve ni recibe mantenimiento.");
                });

                col.Item().Text("Datos del activo").Bold().FontColor(PdfLayout.Navy);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(130); c.RelativeColumn(); });
                    void Row(string label, string? value)
                    {
                        table.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Darken2);
                        table.Cell().PaddingVertical(3).Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
                    }
                    Row("Código de inventario", d.InventoryCode);
                    Row("Tipo", d.AssetType);
                    Row("Marca y modelo", string.Join(" ", new[] { d.Manufacturer, d.Model }.Where(v => v != null)));
                    Row("Número de serie", d.SerialNumber);
                    Row("Hostname", d.Hostname);
                    Row("Última ubicación", d.Location);
                });

                col.Item().Text("Dictamen técnico").Bold().FontColor(PdfLayout.Navy);
                col.Item().Text(string.IsNullOrWhiteSpace(d.Dictamen) ? "Sin dictamen registrado." : d.Dictamen);

                col.Item().Text($"Baja registrada en el sistema por {d.RegisteredBy ?? "—"} el {PdfLayout.MexicoTime(d.CreatedAt):dd/MM/yyyy HH:mm}.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                col.Item().PaddingTop(50).Row(row =>
                {
                    foreach (var role in new[] { "Elaboró", "Revisó", "Autorizó" })
                    {
                        row.RelativeItem().PaddingHorizontal(10).Column(sig =>
                        {
                            sig.Item().BorderTop(0.8f).BorderColor(Colors.Grey.Darken2).PaddingTop(4)
                                .AlignCenter().Text(role).FontSize(9);
                            sig.Item().AlignCenter().Text("Nombre y firma").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    }
                });
            });
            page.Footer().Element(c => PdfLayout.Footer(c, now));
        })).GeneratePdf();

        return new GeneratedFile(pdf, GeneratedFile.Pdf, $"acta-baja-{d.Folio}.pdf");
    }
}
