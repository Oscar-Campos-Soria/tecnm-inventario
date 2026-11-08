using Mediator;

namespace TecnmInventario.Api.Features.Tickets.LinkTicketIncident;

public sealed record LinkTicketIncidentCommand(
    long TicketId,
    int ExpectedVersion,
    long IncidentId) : ICommand<LinkTicketIncidentResponse>;
