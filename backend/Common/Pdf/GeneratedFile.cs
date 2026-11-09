namespace TecnmInventario.Api.Common.Pdf;

/// <summary>Archivo generado al vuelo (PDF, Excel) que el controller devuelve tal cual.</summary>
public sealed record GeneratedFile(byte[] Content, string ContentType, string FileName)
{
    public const string Pdf = "application/pdf";
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
