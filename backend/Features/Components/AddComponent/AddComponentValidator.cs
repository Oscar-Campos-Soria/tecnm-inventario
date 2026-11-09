using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Components.AddComponent;

public class AddComponentValidator : AbstractValidator<AddComponentCommand>
{
    public AddComponentValidator(TecnmInventarioDbContext context)
    {
        RuleFor(x => x.ComponentTypeId)
            .MustAsync((id, ct) => context.ComponentTypes.AnyAsync(t => t.ComponentTypeId == id && !t.IsDeleted, ct))
            .WithMessage("El tipo de componente no existe.");

        RuleFor(x => x.Description).MaximumLength(DbFieldLengths.Components.Description);
        RuleFor(x => x.SerialNumber).MaximumLength(DbFieldLengths.Components.SerialNumber);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Description) || !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage("Describe el componente (marca, modelo, capacidad) o indica su número de serie.")
            .WithName("Description");

        RuleFor(x => x.InstalledAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddDays(1))
            .When(x => x.InstalledAt is not null)
            .WithMessage("La fecha de instalación no puede estar en el futuro.");
    }
}
