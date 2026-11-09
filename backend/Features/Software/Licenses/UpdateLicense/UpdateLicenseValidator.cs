using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Licenses.UpdateLicense;

public class UpdateLicenseValidator : AbstractValidator<UpdateLicenseCommand>
{
    public UpdateLicenseValidator(TecnmInventarioDbContext context)
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión de la licencia que está editando.");

        RuleFor(x => x.LicenseTypeId)
            .MustAsync((id, ct) => context.LicenseTypes.AnyAsync(t => t.LicenseTypeId == id && !t.IsDeleted, ct))
            .WithMessage("El tipo de licencia no existe.");

        RuleFor(x => x.SupplierId)
            .MustAsync((id, ct) => context.Suppliers.AnyAsync(s => s.SupplierId == id, ct))
            .When(x => x.SupplierId is not null)
            .WithMessage("El proveedor no existe.");

        RuleFor(x => x.QuantityPurchased)
            .GreaterThanOrEqualTo(0).When(x => x.QuantityPurchased is not null)
            .WithMessage("La cantidad comprada no puede ser negativa.");

        RuleFor(x => x.ValidUntil)
            .GreaterThanOrEqualTo(x => x.ValidFrom)
            .When(x => x.ValidFrom is not null && x.ValidUntil is not null)
            .WithMessage("La fecha de vencimiento no puede ser anterior a la de inicio.");
    }
}
