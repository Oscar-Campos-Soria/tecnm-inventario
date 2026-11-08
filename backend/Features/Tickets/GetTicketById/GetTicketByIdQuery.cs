using Mediator;

namespace TecnmInventario.Api.Features.Tickets.GetTicketById;

public sealed record GetTicketByIdQuery(long TicketId) : IQuery<GetTicketByIdResponse>;
