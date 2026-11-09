using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Components.ReplaceComponent;

public class ReplaceComponentValidator : AbstractValidator<ReplaceComponentCommand>
{
    public ReplaceComponentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del componente que está reemplazando.");

        RuleFor(x => x.Description).MaximumLength(DbFieldLengths.Components.Description);
        RuleFor(x => x.SerialNumber).MaximumLength(DbFieldLengths.Components.SerialNumber);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Description) || !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage("Describe el componente (marca, modelo, capacidad) o indica su número de serie.")
            .WithName("Description");

        RuleFor(x => x.ReplacedAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddDays(1))
            .When(x => x.ReplacedAt is not null)
            .WithMessage("La fecha del cambio no puede estar en el futuro.");
    }
}
