using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Disposals.GetDisposals;

public sealed record GetDisposalsQuery(
    int PageNumber,
    int PageSize,
    long? AssetId,
    long? DisposalReasonId,
    string? Search) : IQuery<PagedResult<GetDisposalsResponse>>;
