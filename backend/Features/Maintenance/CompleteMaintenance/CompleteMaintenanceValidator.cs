using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

public class CompleteMaintenanceValidator : AbstractValidator<CompleteMaintenanceCommand>
{
    public CompleteMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Debe indicar la versión que está viendo (expectedVersion).");
        RuleFor(x => x.PerformedAt)
            .Must(d => d <= DateTime.UtcNow.AddDays(1)).WithMessage("La fecha de realización no puede ser futura.");
        RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description is not null);
        RuleFor(x => x.PartsUsed).MaximumLength(2000).When(x => x.PartsUsed is not null);
        RuleFor(x => x.NextMaintenanceAt)
            .Must((cmd, next) => next!.Value > cmd.PerformedAt)
            .WithMessage("La próxima fecha debe ser posterior a la realización.")
            .When(x => x.NextMaintenanceAt.HasValue);
        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.")
            .LessThan(10_000_000_000m)
            .When(x => x.Cost.HasValue);
    }
}
