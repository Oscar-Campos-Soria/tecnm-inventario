using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseAssignments;

public sealed class GetLicenseAssignmentsHandler
    : IQueryHandler<GetLicenseAssignmentsQuery, PagedResult<GetLicenseAssignmentsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetLicenseAssignmentsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetLicenseAssignmentsResponse>> Handle(
        GetLicenseAssignmentsQuery query, CancellationToken cancellationToken)
    {
        if (!await _context.SoftwareLicenses.AnyAsync(l => l.LicenseId == query.LicenseId, cancellationToken))
        {
            throw new KeyNotFoundException("La licencia solicitada no existe.");
        }

        var assignments = _context.SoftwareLicenseAssignments.AsNoTracking()
            .Where(a => a.LicenseId == query.LicenseId);

        if (query.OnlyActive)
        {
            assignments = assignments.Where(a => a.EndedAt == null);
        }

        var totalCount = await assignments.CountAsync(cancellationToken);

        var items = await assignments
            .OrderBy(a => a.EndedAt != null).ThenByDescending(a => a.AssignedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new GetLicenseAssignmentsResponse(
                a.LicenseAssignmentId,
                a.AssetId,
                a.Asset.InventoryCode,
                a.Asset.Hostname,
                a.Asset.CurrentLocation != null ? a.Asset.CurrentLocation.Name : null,
                a.AssignedAt,
                a.EndedAt,
                a.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetLicenseAssignmentsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
