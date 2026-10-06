using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public UpdateSupplierValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es obligatorio.")
            .MaximumLength(180);

        RuleFor(x => x.Email)
            .MaximumLength(254)
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // Único, pero excluyendo al propio proveedor que se está editando.
        RuleFor(x => x)
            .MustAsync(BeUniqueNameForOtherSupplier)
            .WithMessage("Ya existe otro proveedor con ese nombre.")
            .WithName("Name");
    }

    private async Task<bool> BeUniqueNameForOtherSupplier(UpdateSupplierCommand command, CancellationToken ct)
    {
        var trimmed = command.Name.Trim();
        return !await _context.Suppliers
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Name == trimmed && s.SupplierId != command.SupplierId, ct);
    }
}
