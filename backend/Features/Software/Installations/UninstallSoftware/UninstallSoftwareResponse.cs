namespace TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;

/// <param name="HasActiveLicense">El equipo sigue ocupando una licencia de este programa; liberarla es aparte.</param>
public sealed record UninstallSoftwareResponse(long AssetSoftwareId, bool HasActiveLicense, int Version);
