using Mediator;
using TecnmInventario.Api.Common.Pdf;

namespace TecnmInventario.Api.Features.Assets.GetAssetLabels;

public sealed record GetAssetLabelsQuery(IReadOnlyList<long> AssetIds) : IQuery<GeneratedFile>;
