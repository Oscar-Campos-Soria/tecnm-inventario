namespace TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;

public sealed record EndLicenseAssignmentResponse(long LicenseAssignmentId, DateTime EndedAt, int Version);
