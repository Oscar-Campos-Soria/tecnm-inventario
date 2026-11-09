using Mediator;

namespace TecnmInventario.Api.Features.Software.Installations.GetAssetSoftware;

/// <param name="IncludeHistory">También las instalaciones anteriores (versiones reemplazadas o desinstaladas).</param>
public sealed record GetAssetSoftwareQuery(long AssetId, bool IncludeHistory)
    : IQuery<IReadOnlyList<GetAssetSoftwareResponse>>;
