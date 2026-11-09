using Mediator;

namespace TecnmInventario.Api.Features.Documents.GetAssetDocuments;

public sealed record GetAssetDocumentsQuery(long AssetId) : IQuery<IReadOnlyList<GetAssetDocumentsResponse>>;
