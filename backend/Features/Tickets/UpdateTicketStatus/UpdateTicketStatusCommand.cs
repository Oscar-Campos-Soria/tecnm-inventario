using Mediator;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

public sealed record UpdateTicketStatusCommand(
    long TicketId,
    int ExpectedVersion,
    string NewStatus,
    string? Reason) : ICommand<UpdateTicketStatusResponse>;
