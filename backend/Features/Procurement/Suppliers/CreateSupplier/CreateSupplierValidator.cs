using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;

public class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateSupplierValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es obligatorio.")
            .MaximumLength(180)
            .MustAsync(BeUniqueName).WithMessage("Ya existe un proveedor con ese nombre.");

        RuleFor(x => x.TaxId).MaximumLength(30);
        RuleFor(x => x.ContactName).MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);

        RuleFor(x => x.Email)
            .MaximumLength(254)
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }

    // IgnoreQueryFilters: el índice UNIQUE de la BD incluye proveedores
    // borrados lógicamente; sin esto, un nombre "liberado" solo en apariencia
    // pasaría la validación y el INSERT fallaría con un error 500.
    private async Task<bool> BeUniqueName(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();
        return !await _context.Suppliers
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Name == trimmed, ct);
    }
}
