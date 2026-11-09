using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenses;

/// <param name="ExpiringInDays">Solo licencias vigentes que vencen dentro de N días.</param>
public sealed record GetLicensesQuery(
    int PageNumber, int PageSize, long? SoftwareId, int? ExpiringInDays, string? Search)
    : IQuery<PagedResult<GetLicensesResponse>>;
