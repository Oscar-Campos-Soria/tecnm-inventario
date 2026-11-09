using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Programs.Shared;

namespace TecnmInventario.Api.Features.Software.Programs.CreateSoftware;

public class CreateSoftwareValidator : AbstractValidator<CreateSoftwareCommand>
{
    public CreateSoftwareValidator(TecnmInventarioDbContext context)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del programa es obligatorio.")
            .MaximumLength(DbFieldLengths.Software.Name);

        RuleFor(x => x.Vendor).MaximumLength(DbFieldLengths.Software.Vendor);

        RuleFor(x => x.SoftwareTypeId)
            .MustAsync((id, ct) => context.SoftwareTypes.AnyAsync(t => t.SoftwareTypeId == id && !t.IsDeleted, ct))
            .When(x => x.SoftwareTypeId is not null)
            .WithMessage("El tipo de software no existe.");

        RuleFor(x => x)
            .MustAsync((c, ct) => SoftwareNames.IsFreeAsync(context, c.Name, c.Vendor, null, ct))
            .When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Ya existe un programa con ese nombre y fabricante.")
            .WithName("Name");
    }
}
