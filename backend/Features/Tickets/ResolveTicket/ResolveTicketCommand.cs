using Mediator;

namespace TecnmInventario.Api.Features.Tickets.ResolveTicket;

public sealed record ResolveTicketCommand(
    long TicketId,
    int ExpectedVersion,
    string Resolution) : ICommand<ResolveTicketResponse>;
