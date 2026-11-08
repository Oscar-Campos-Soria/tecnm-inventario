using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Tickets.Shared;

/// <summary>
/// Transiciones del endpoint de cambio de estado (PATCH /status).
/// ABIERTO → EN_ATENCION solo ocurre al asignar; → RESUELTO solo vía /resolve
/// (exige la resolución); ESPERANDO_USUARIO → EN_ATENCION también ocurre sola
/// cuando el solicitante comenta. CERRADO y CANCELADO son terminales.
/// </summary>
public static class TicketStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [TicketStatusCodes.Abierto] = new HashSet<string> { TicketStatusCodes.Cancelado },
            [TicketStatusCodes.EnAtencion] = new HashSet<string>
            {
                TicketStatusCodes.EsperandoUsuario, TicketStatusCodes.Cancelado
            },
            [TicketStatusCodes.EsperandoUsuario] = new HashSet<string>
            {
                TicketStatusCodes.EnAtencion, TicketStatusCodes.Cancelado
            },
            [TicketStatusCodes.Resuelto] = new HashSet<string>
            {
                TicketStatusCodes.Cerrado, TicketStatusCodes.EnAtencion
            },
            [TicketStatusCodes.Cerrado] = new HashSet<string>(),
            [TicketStatusCodes.Cancelado] = new HashSet<string>(),
        };

    /// <summary>Estados que el endpoint de cambio de estado acepta como destino.</summary>
    public static readonly IReadOnlyCollection<string> ManualTargets = new[]
    {
        TicketStatusCodes.EnAtencion,
        TicketStatusCodes.EsperandoUsuario,
        TicketStatusCodes.Cerrado,
        TicketStatusCodes.Cancelado
    };

    /// <summary>
    /// El técnico (tickets.manage) puede cualquier transición de la matriz.
    /// El solicitante solo cancela, confirma el cierre o reabre un resuelto.
    /// </summary>
    public static bool IsAllowed(string currentStatus, string newStatus, bool isManager)
    {
        if (!Allowed.TryGetValue(currentStatus, out var targets) || !targets.Contains(newStatus))
            return false;

        if (isManager)
            return true;

        return newStatus is TicketStatusCodes.Cancelado or TicketStatusCodes.Cerrado
            || (newStatus == TicketStatusCodes.EnAtencion && currentStatus == TicketStatusCodes.Resuelto);
    }
}
