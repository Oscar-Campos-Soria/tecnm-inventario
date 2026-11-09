namespace TecnmInventario.Api.Features.Components;

/// <param name="ReplacedAssetComponentId">Solo al reemplazar: el componente que quedó retirado.</param>
public sealed record ComponentWriteResponse(long AssetComponentId, int Version, long? ReplacedAssetComponentId = null);
