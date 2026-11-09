using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Software.Programs.GetSoftware;

public sealed record GetSoftwareQuery(int PageNumber, int PageSize, string? Search, long? SoftwareTypeId)
    : IQuery<PagedResult<GetSoftwareResponse>>;
