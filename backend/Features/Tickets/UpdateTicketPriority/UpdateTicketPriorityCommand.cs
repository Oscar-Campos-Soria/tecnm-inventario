using Mediator;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketPriority;

public sealed record UpdateTicketPriorityCommand(
    long TicketId,
    int ExpectedVersion,
    long PriorityId) : ICommand<UpdateTicketPriorityResponse>;
