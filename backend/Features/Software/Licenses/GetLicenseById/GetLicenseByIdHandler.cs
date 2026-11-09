using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseById;

public sealed class GetLicenseByIdHandler : IQueryHandler<GetLicenseByIdQuery, GetLicenseByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetLicenseByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetLicenseByIdResponse> Handle(
        GetLicenseByIdQuery query, CancellationToken cancellationToken)
    {
        var l = await _context.SoftwareLicenses.AsNoTracking()
            .Where(x => x.LicenseId == query.LicenseId)
            .Select(x => new
            {
                x.LicenseId,
                x.SoftwareId,
                SoftwareName = x.Software.Name,
                SoftwareVendor = x.Software.Vendor,
                x.LicenseTypeId,
                LicenseTypeCode = x.LicenseType.Code,
                LicenseTypeName = x.LicenseType.Name,
                x.LicenseKey,
                x.QuantityPurchased,
                InUse = x.SoftwareLicenseAssignments.Count(a => a.EndedAt == null),
                x.ValidFrom,
                x.ValidUntil,
                x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                x.Notes,
                x.CreatedAt,
                x.UpdatedAt,
                x.Version
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("La licencia solicitada no existe.");

        return new GetLicenseByIdResponse(
            l.LicenseId, l.SoftwareId, l.SoftwareName, l.SoftwareVendor,
            l.LicenseTypeId, l.LicenseTypeCode, l.LicenseTypeName,
            LicenseRules.Mask(l.LicenseKey),
            l.QuantityPurchased, l.InUse, l.QuantityPurchased - l.InUse,
            l.ValidFrom, l.ValidUntil, l.ValidUntil < LicenseRules.Today,
            l.SupplierId, l.SupplierName, l.Notes, l.CreatedAt, l.UpdatedAt, l.Version);
    }
}
