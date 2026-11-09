namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseAssignments;

public sealed record GetLicenseAssignmentsResponse(
    long LicenseAssignmentId,
    long AssetId,
    string InventoryCode,
    string? Hostname,
    string? LocationName,
    DateTime AssignedAt,
    DateTime? EndedAt,
    int Version);
