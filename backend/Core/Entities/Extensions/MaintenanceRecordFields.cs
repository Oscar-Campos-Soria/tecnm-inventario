namespace TecnmInventario.Api.Core.Entities;

// Columnas de scripts/add-maintenance-fields.sql, agregadas como partial para
// no editar el archivo scaffolded (MaintenanceRecord.cs). Mapeo en
// TecnmInventarioDbContext.Custom.cs.
public partial class MaintenanceRecord
{
    public long? IncidentId { get; set; }

    public decimal? Cost { get; set; }

    public virtual Incident? Incident { get; set; }
}
