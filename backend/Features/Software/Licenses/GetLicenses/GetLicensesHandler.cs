using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenses;

public sealed class GetLicensesHandler
    : IQueryHandler<GetLicensesQuery, PagedResult<GetLicensesResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetLicensesHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetLicensesResponse>> Handle(
        GetLicensesQuery query, CancellationToken cancellationToken)
    {
        var today = LicenseRules.Today;
        var licenses = _context.SoftwareLicenses.AsNoTracking();

        if (query.SoftwareId is not null)
        {
            licenses = licenses.Where(l => l.SoftwareId == query.SoftwareId);
        }

        if (query.ExpiringInDays is not null)
        {
            var limit = today.AddDays(query.ExpiringInDays.Value);
            licenses = licenses.Where(l => l.ValidUntil != null && l.ValidUntil >= today && l.ValidUntil <= limit);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            licenses = licenses.Where(l => l.Software.Name.ToLower().Contains(term));
        }

        var totalCount = await licenses.CountAsync(cancellationToken);

        var rows = await licenses
            .OrderBy(l => l.ValidUntil == null).ThenBy(l => l.ValidUntil).ThenBy(l => l.LicenseId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new
            {
                l.LicenseId,
                l.SoftwareId,
                SoftwareName = l.Software.Name,
                SoftwareVendor = l.Software.Vendor,
                LicenseTypeCode = l.LicenseType.Code,
                LicenseTypeName = l.LicenseType.Name,
                l.QuantityPurchased,
                InUse = l.SoftwareLicenseAssignments.Count(a => a.EndedAt == null),
                l.ValidFrom,
                l.ValidUntil,
                SupplierName = l.Supplier != null ? l.Supplier.Name : null,
                l.Version
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new GetLicensesResponse(
                r.LicenseId, r.SoftwareId, r.SoftwareName, r.SoftwareVendor,
                r.LicenseTypeCode, r.LicenseTypeName, r.QuantityPurchased, r.InUse,
                r.QuantityPurchased - r.InUse,
                r.ValidFrom, r.ValidUntil, r.ValidUntil < today, r.SupplierName, r.Version))
            .ToList();

        return new PagedResult<GetLicensesResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
