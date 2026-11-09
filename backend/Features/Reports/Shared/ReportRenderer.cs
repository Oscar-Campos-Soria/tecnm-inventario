using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TecnmInventario.Api.Common.Pdf;
using Unit = QuestPDF.Infrastructure.Unit;

namespace TecnmInventario.Api.Features.Reports.Shared;

public static class ReportRenderer
{
    public static GeneratedFile Render(ReportTable report, string format, DateTime nowUtc)
    {
        var stamp = PdfLayout.MexicoTime(nowUtc).ToString("yyyyMMdd-HHmm");
        return format == ReportCodes.Pdf
            ? new GeneratedFile(Pdf(report, nowUtc), GeneratedFile.Pdf, $"{report.FileSlug}-{stamp}.pdf")
            : new GeneratedFile(Xlsx(report, nowUtc), GeneratedFile.Xlsx, $"{report.FileSlug}-{stamp}.xlsx");
    }

    private static byte[] Xlsx(ReportTable report, DateTime nowUtc)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Reporte");
        sheet.Cell(1, 1).Value = report.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var line = 2;
        foreach (var f in report.Filters.Prepend($"Generado el {PdfLayout.MexicoTime(nowUtc):dd/MM/yyyy HH:mm}"))
        {
            sheet.Cell(line++, 1).Value = f;
        }
        if (report.Truncated)
        {
            sheet.Cell(line++, 1).Value = $"Se muestran las primeras {ReportCodes.MaxRows:N0} filas; usa filtros para acotar.";
        }

        var header = line + 1;
        for (var c = 0; c < report.Columns.Count; c++)
        {
            sheet.Cell(header, c + 1).Value = report.Columns[c];
        }
        for (var r = 0; r < report.Rows.Count; r++)
        {
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var cell = sheet.Cell(header + 1 + r, c + 1);
                switch (report.Rows[r][c])
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case decimal m: cell.Value = m; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd/mm/yyyy"; break;
                    case DateTime t: cell.Value = PdfLayout.MexicoTime(t); cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm"; break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
        }

        var range = sheet.Range(header, 1, header + Math.Max(report.Rows.Count, 1), report.Columns.Count);
        var table = range.CreateTable("Datos");
        table.Theme = XLTableTheme.TableStyleLight9;
        sheet.Columns().AdjustToContents(header, header + Math.Min(report.Rows.Count, 500));
        sheet.SheetView.FreezeRows(header);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] Pdf(ReportTable report, DateTime nowUtc)
        => Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter.Landscape());
            page.Margin(1.3f, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(8.5f));
            page.Header().Element(c => PdfLayout.Header(c, report.Title, report.Filters.Count > 0 ? string.Join(". ", report.Filters) : null));
            page.Content().Column(col =>
            {
                if (report.Truncated)
                {
                    col.Item().PaddingBottom(6).Text($"Se muestran las primeras {ReportCodes.MaxRows:N0} filas; usa filtros para acotar.")
                        .FontColor(Colors.Orange.Darken3);
                }
                if (report.Rows.Count == 0)
                {
                    col.Item().PaddingTop(20).AlignCenter().Text("Sin datos para los filtros elegidos.").FontColor(Colors.Grey.Darken1);
                    return;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in report.Columns) c.RelativeColumn();
                    });
                    table.Header(h =>
                    {
                        foreach (var name in report.Columns)
                        {
                            h.Cell().Background(PdfLayout.Navy).Padding(4).Text(name).FontColor(Colors.White).Bold();
                        }
                    });
                    for (var r = 0; r < report.Rows.Count; r++)
                    {
                        var background = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        foreach (var value in report.Rows[r])
                        {
                            table.Cell().Background(background).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(3).Text(Format(value));
                        }
                    }
                });
                col.Item().PaddingTop(6).AlignRight().Text($"{report.Rows.Count:N0} fila(s)").FontColor(Colors.Grey.Darken1);
            });
            page.Footer().Element(c => PdfLayout.Footer(c, nowUtc));
        })).GeneratePdf();

    private static string Format(object? value) => value switch
    {
        null => "—",
        decimal m => m.ToString("#,##0.00"),
        DateOnly d => d.ToString("dd/MM/yyyy"),
        DateTime t => PdfLayout.MexicoTime(t).ToString("dd/MM/yyyy HH:mm"),
        _ => value.ToString() ?? "—"
    };
}
