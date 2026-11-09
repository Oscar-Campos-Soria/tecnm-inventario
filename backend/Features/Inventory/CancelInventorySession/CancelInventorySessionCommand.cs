using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Inventory.CancelInventorySession;

public sealed record CancelInventorySessionCommand(long InventorySessionId, int ExpectedVersion, string Reason)
    : ICommand<CancelInventorySessionResponse>, ITransactionalCommand;

public sealed record CancelInventorySessionResponse(long InventorySessionId, string Status, int Version);
