using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseAssignments;

public sealed record GetLicenseAssignmentsQuery(long LicenseId, int PageNumber, int PageSize, bool OnlyActive)
    : IQuery<PagedResult<GetLicenseAssignmentsResponse>>;
