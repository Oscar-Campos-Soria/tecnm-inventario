namespace TecnmInventario.Api.Core.Entities;

// Inmutable (sin Version ni soft-delete). Tabla de scripts/add-tickets.sql.
public partial class TicketComment
{
    public long TicketCommentId { get; set; }

    public long TicketId { get; set; }

    public long AuthorId { get; set; }

    public string Body { get; set; } = null!;

    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual AppUser Author { get; set; } = null!;
}
