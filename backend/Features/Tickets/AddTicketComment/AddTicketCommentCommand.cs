using Mediator;

namespace TecnmInventario.Api.Features.Tickets.AddTicketComment;

public sealed record AddTicketCommentCommand(
    long TicketId,
    string Body,
    bool IsInternal) : ICommand<AddTicketCommentResponse>;
