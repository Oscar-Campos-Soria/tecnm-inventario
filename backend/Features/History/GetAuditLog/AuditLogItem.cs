namespace TecnmInventario.Api.Features.History.GetAuditLog;

/// <param name="OldData">JSON del estado anterior (proyección explícita; sin secretos).</param>
/// <param name="NewData">JSON del estado nuevo.</param>
public sealed record AuditLogItem(
    long AuditLogId,
    string TableName,
    long RecordId,
    string Action,
    long? PerformedById,
    string? PerformedByUsername,
    DateTime PerformedAt,
    string? OldData,
    string? NewData);
