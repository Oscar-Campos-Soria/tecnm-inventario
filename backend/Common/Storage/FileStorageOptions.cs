namespace TecnmInventario.Api.Common.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Carpeta raíz en disco donde se guardan los archivos (implementación local).
    /// Debe estar fuera de wwwroot / cualquier carpeta servida públicamente.
    /// </summary>
    public string LocalPath { get; set; } = null!;
}
