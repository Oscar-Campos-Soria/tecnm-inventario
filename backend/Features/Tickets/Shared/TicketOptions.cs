namespace TecnmInventario.Api.Features.Tickets.Shared;

public sealed class TicketOptions
{
    public const string SectionName = "Tickets";

    /// <summary>Días que un ticket RESUELTO espera la confirmación del usuario antes de cerrarse solo.</summary>
    public int AutoCloseDays { get; set; } = 3;
}
