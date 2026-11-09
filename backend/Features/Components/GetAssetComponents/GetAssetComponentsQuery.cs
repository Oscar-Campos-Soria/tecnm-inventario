using Mediator;

namespace TecnmInventario.Api.Features.Components.GetAssetComponents;

/// <param name="IncludeRemoved">También los retirados o reemplazados (historial).</param>
public sealed record GetAssetComponentsQuery(long AssetId, bool IncludeRemoved)
    : IQuery<IReadOnlyList<GetAssetComponentsResponse>>;
