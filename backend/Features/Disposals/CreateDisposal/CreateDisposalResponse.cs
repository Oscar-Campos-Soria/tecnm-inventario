namespace TecnmInventario.Api.Features.Disposals.CreateDisposal;

public sealed record CreateDisposalResponse(
    long AssetDisposalId,
    long AssetId,
    string Folio,
    DateOnly DisposedAt,
    string AssetStatusCode,
    int AssetVersion);
