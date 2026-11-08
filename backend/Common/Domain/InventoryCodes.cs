namespace TecnmInventario.Api.Common.Domain;

/// <summary>Valores del CHECK de inventory_sessions.status (database.sql, en minúsculas).</summary>
public static class InventorySessionStatusCodes
{
    public const string Abierta = "abierta";
    public const string Cerrada = "cerrada";
    public const string Cancelada = "cancelada";
}

/// <summary>Valores del CHECK de inventory_records.adjustment (scripts/add-inventory-counts.sql).</summary>
public static class InventoryAdjustmentCodes
{
    public const string Movimiento = "MOVIMIENTO";
    public const string Incidente = "INCIDENTE";
    public const string SinCambio = "SIN_CAMBIO";

    public static readonly IReadOnlyCollection<string> All = new[] { Movimiento, Incidente, SinCambio };
}

/// <summary>
/// Resultado de un renglón del conteo (calculado, no se guarda):
/// pendiente, encontrado en su lugar, en otro lugar, no encontrado o sobrante.
/// </summary>
public static class InventoryRecordStatusCodes
{
    public const string Pendiente = "PENDIENTE";
    public const string Encontrado = "ENCONTRADO";
    public const string EnOtroLugar = "EN_OTRO_LUGAR";
    public const string NoEncontrado = "NO_ENCONTRADO";
    public const string Sobrante = "SOBRANTE";

    public static string Of(DateTime? checkedAt, bool found, long? expectedLocationId, long? locationFoundId)
    {
        if (checkedAt is null) return Pendiente;
        if (!found) return NoEncontrado;
        if (expectedLocationId is null) return Sobrante;
        return locationFoundId is null || locationFoundId == expectedLocationId ? Encontrado : EnOtroLugar;
    }

    /// <summary>Resultados que requieren un ajuste al conciliar.</summary>
    public static bool NeedsAdjustment(string status) =>
        status is EnOtroLugar or NoEncontrado or Sobrante;
}

/// <summary>Códigos de catálogo usados por la conciliación.</summary>
public static class InventoryCatalogCodes
{
    public const string AjusteMovementReason = "AJUSTE_INVENTARIO";
    public const string NotFoundIncidentType = "ROBO_EXTRAVIO";
}
