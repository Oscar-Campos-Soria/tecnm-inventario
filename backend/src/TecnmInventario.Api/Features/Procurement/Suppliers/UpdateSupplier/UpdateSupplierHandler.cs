using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

public sealed class UpdateSupplierHandler : ICommandHandler<UpdateSupplierCommand, UpdateSupplierResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateSupplierHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<UpdateSupplierResponse> Handle(
        UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.SupplierId == command.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new KeyNotFoundException("El proveedor solicitado no existe.");
        }

        // Concurrencia optimista: igual criterio que SoftDeleteAuditor, pero
        // aquí es un UPDATE de negocio normal, no una baja, así que se
        // resuelve inline en vez de vía el servicio de borrado.
        _context.Entry(supplier).Property(s => s.Version).OriginalValue = command.ExpectedVersion;

        supplier.Name = command.Name.Trim();
        supplier.TaxId = NullIfBlank(command.TaxId);
        supplier.ContactName = NullIfBlank(command.ContactName);
        supplier.Email = NullIfBlank(command.Email);
        supplier.Phone = NullIfBlank(command.Phone);
        supplier.Address = NullIfBlank(command.Address);
        supplier.Notes = NullIfBlank(command.Notes);
        supplier.UpdatedBy = _currentUser.UserId;
        // UpdatedAt y Version los actualiza el trigger touch_version.

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(supplier).ReloadAsync(cancellationToken);

        return new UpdateSupplierResponse(supplier.SupplierId, supplier.Name, supplier.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
