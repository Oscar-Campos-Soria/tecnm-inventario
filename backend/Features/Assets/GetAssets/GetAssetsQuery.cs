using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed record GetAssetsQuery(
    int PageNumber,
    int PageSize,
    // Texto libre: código de inventario, serie o hostname.
    string? Search = null,
    string? StatusCode = null,
    long? AssetTypeId = null,
    // Incluye las sub-ubicaciones (edificio -> plantas -> salones).
    long? LocationId = null,
    // Los dados de baja se ocultan salvo que se pidan.
    bool IncludeDisposed = false,
    long? ManufacturerId = null) : IQuery<PagedResult<GetAssetsResponse>>;
