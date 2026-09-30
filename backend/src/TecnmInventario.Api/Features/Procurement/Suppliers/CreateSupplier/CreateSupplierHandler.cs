using Mediator;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;
using TecnmInventario.Infrastructure.Entities;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;

public sealed class CreateSupplierHandler : ICommandHandler<CreateSupplierCommand, CreateSupplierResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateSupplierHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateSupplierResponse> Handle(
        CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var supplier = new Supplier
        {
            Name = command.Name.Trim(),
            TaxId = NullIfBlank(command.TaxId),
            ContactName = NullIfBlank(command.ContactName),
            Email = NullIfBlank(command.Email),
            Phone = NullIfBlank(command.Phone),
            Address = NullIfBlank(command.Address),
            Notes = NullIfBlank(command.Notes),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateSupplierResponse(supplier.SupplierId, supplier.Name, supplier.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
