using Mediator;

namespace TecnmInventario.Api.Features.Tickets.CreateTicket;

public sealed record CreateTicketCommand(
    long CategoryId,
    long? PriorityId,
    string Title,
    string? Description,
    long? AssetId,
    long? LocationId) : ICommand<CreateTicketResponse>;
