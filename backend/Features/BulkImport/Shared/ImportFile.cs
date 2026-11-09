namespace TecnmInventario.Api.Features.BulkImport.Shared;

/// <summary>Archivo subido (el controller lo lee una vez a memoria; máximo 5 MB).</summary>
public sealed record ImportFile(string FileName, byte[] Content)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 1000;

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
}
