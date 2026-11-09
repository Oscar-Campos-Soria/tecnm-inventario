namespace TecnmInventario.Api.Features.Documents.GetAssetDocuments;

public sealed record GetAssetDocumentsResponse(
    long DocumentId,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAt,
    string OwnerType,
    long OwnerId,
    string OwnerLabel);
