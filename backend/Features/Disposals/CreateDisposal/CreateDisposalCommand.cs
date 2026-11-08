using Mediator;

namespace TecnmInventario.Api.Features.Disposals.CreateDisposal;

public sealed record CreateDisposalCommand(
    long AssetId,
    // Versión del activo que el usuario vio (concurrencia optimista sobre assets).
    int ExpectedVersion,
    long DisposalReasonId,
    string Folio,
    string? Dictamen,
    DateOnly? DisposedAt) : ICommand<CreateDisposalResponse>;
