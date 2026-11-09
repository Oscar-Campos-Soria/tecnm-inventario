using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Programs.GetSoftware;

public sealed class GetSoftwareHandler
    : IQueryHandler<GetSoftwareQuery, PagedResult<GetSoftwareResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetSoftwareHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetSoftwareResponse>> Handle(
        GetSoftwareQuery query, CancellationToken cancellationToken)
    {
        var software = _context.Softwares.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            software = software.Where(s =>
                s.Name.ToLower().Contains(term) || (s.Vendor != null && s.Vendor.ToLower().Contains(term)));
        }

        if (query.SoftwareTypeId is not null)
        {
            software = software.Where(s => s.SoftwareTypeId == query.SoftwareTypeId);
        }

        var totalCount = await software.CountAsync(cancellationToken);

        var items = await software
            .OrderBy(s => s.Name).ThenBy(s => s.Vendor)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new GetSoftwareResponse(
                s.SoftwareId,
                s.Name,
                s.Vendor,
                s.SoftwareTypeId,
                s.SoftwareType != null ? s.SoftwareType.Name : null,
                s.SoftwareLicenses.Count(),
                s.AssetSoftwares.Count(i => i.IsCurrent && !i.IsDeleted),
                s.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetSoftwareResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
