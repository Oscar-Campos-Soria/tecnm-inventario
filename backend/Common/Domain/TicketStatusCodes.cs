namespace TecnmInventario.Api.Common.Domain;

/// <summary>Valores del CHECK de tickets.status (scripts/add-tickets.sql).</summary>
public static class TicketStatusCodes
{
    public const string Abierto = "ABIERTO";
    public const string EnAtencion = "EN_ATENCION";
    public const string EsperandoUsuario = "ESPERANDO_USUARIO";
    public const string Resuelto = "RESUELTO";
    public const string Cerrado = "CERRADO";
    public const string Cancelado = "CANCELADO";

    /// <summary>Estados en los que el ticket sigue pendiente de atención.</summary>
    public static readonly IReadOnlyCollection<string> Open =
        new[] { Abierto, EnAtencion, EsperandoUsuario };

    /// <summary>Estados finales: ya no admiten comentarios, adjuntos ni cambios.</summary>
    public static readonly IReadOnlyCollection<string> Terminal =
        new[] { Cerrado, Cancelado };

    public static readonly IReadOnlyCollection<string> All =
        new[] { Abierto, EnAtencion, EsperandoUsuario, Resuelto, Cerrado, Cancelado };
}
