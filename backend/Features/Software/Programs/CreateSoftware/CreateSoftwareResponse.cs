namespace TecnmInventario.Api.Features.Software.Programs.CreateSoftware;

public sealed record CreateSoftwareResponse(long SoftwareId, string Name, string? Vendor, int Version);
