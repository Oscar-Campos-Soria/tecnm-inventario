namespace TecnmInventario.Api.Features.Software.Programs.UpdateSoftware;

public sealed record UpdateSoftwareResponse(long SoftwareId, string Name, string? Vendor, int Version);
