using Mediator;

namespace TecnmInventario.Api.Features.Tickets.AssignTicket;

public sealed record AssignTicketCommand(
    long TicketId,
    int ExpectedVersion,
    long AssignedToUserId) : ICommand<AssignTicketResponse>;
