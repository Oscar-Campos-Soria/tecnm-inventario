using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TecnmInventario.Api.Common.Pdf;

/// <summary>Encabezado y pie comunes de los PDF del sistema (QuestPDF).</summary>
public static class PdfLayout
{
    public const string Institution = "Tecnológico Nacional de México";
    public const string System = "Sistema de inventario de activos tecnológicos";
    public const string Navy = "#1b396a";

    public static void Header(IContainer container, string title, string? subtitle = null)
        => container.PaddingBottom(12).BorderBottom(1.5f).BorderColor(Navy).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(Institution).FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(title).FontSize(16).Bold().FontColor(Navy);
                if (subtitle is not null) col.Item().Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

    public static void Footer(IContainer container, DateTime generatedAtUtc)
        => container.PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"{System}. Generado el {MexicoTime(generatedAtUtc):dd/MM/yyyy HH:mm}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
            row.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });

    // Fechas impresas en hora del centro de México; si el sistema no tiene la zona, UTC.
    public static DateTime MexicoTime(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
    }
}
