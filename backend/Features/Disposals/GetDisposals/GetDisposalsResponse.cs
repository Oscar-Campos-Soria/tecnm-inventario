namespace TecnmInventario.Api.Features.Disposals.GetDisposals;

public sealed record GetDisposalsResponse(
    long AssetDisposalId,
    long AssetId,
    string AssetInventoryCode,
    string AssetTypeName,
    string ReasonCode,
    string ReasonName,
    string Folio,
    string? Dictamen,
    DateOnly DisposedAt,
    string? CreatedByUsername,
    DateTime CreatedAt);
