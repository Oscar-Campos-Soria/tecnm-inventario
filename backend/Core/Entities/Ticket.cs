namespace TecnmInventario.Api.Core.Entities;

// Tabla de scripts/add-tickets.sql, mapeada a mano en TecnmInventarioDbContext.Custom.cs.
public partial class Ticket
{
    public long TicketId { get; set; }

    public long RequesterId { get; set; }

    public long? AssignedTo { get; set; }

    public long TicketCategoryId { get; set; }

    public long IncidentPriorityId { get; set; }

    public string Status { get; set; } = null!;

    public long? AssetId { get; set; }

    public long? LocationId { get; set; }

    public long? IncidentId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Resolution { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual AppUser Requester { get; set; } = null!;

    public virtual AppUser? AssignedToNavigation { get; set; }

    public virtual TicketCategory TicketCategory { get; set; } = null!;

    public virtual IncidentPriority IncidentPriority { get; set; } = null!;

    public virtual Asset? Asset { get; set; }

    public virtual Location? Location { get; set; }

    public virtual Incident? Incident { get; set; }

    public virtual ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
}
