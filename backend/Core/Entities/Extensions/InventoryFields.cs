namespace TecnmInventario.Api.Core.Entities;

// Columnas de scripts/add-inventory-counts.sql, como partial para no editar
// los archivos scaffolded. Mapeo en TecnmInventarioDbContext.Custom.cs.

public partial class InventorySession
{
    public long? LocationId { get; set; }

    public virtual Location? Location { get; set; }
}

public partial class InventoryRecord
{
    public long? ExpectedLocationId { get; set; }

    public DateTime? CheckedAt { get; set; }

    public long? CheckedBy { get; set; }

    public string? Adjustment { get; set; }

    public DateTime? AdjustedAt { get; set; }

    public long? AdjustedBy { get; set; }

    public long? IncidentId { get; set; }

    public virtual Location? ExpectedLocation { get; set; }
}
