using FluentValidation;

namespace TecnmInventario.Api.Features.Components.RemoveComponent;

public class RemoveComponentValidator : AbstractValidator<RemoveComponentCommand>
{
    public RemoveComponentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del componente que está retirando.");

        RuleFor(x => x.RemovedAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddDays(1))
            .When(x => x.RemovedAt is not null)
            .WithMessage("La fecha de retiro no puede estar en el futuro.");
    }
}
