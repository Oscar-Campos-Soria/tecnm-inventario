namespace TecnmInventario.Api.Features.Tickets.AddTicketComment;

public sealed record AddTicketCommentResponse(
    long CommentId,
    long TicketId,
    bool IsInternal,
    DateTime CreatedAt,
    string TicketStatus,
    int TicketVersion);
