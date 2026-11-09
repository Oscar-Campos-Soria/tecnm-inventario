using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.CreateMaintenance;

public class CreateMaintenanceValidator : AbstractValidator<CreateMaintenanceCommand>
{
    public CreateMaintenanceValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Debe indicar el activo.");
        RuleFor(x => x.MaintenanceTypeId).GreaterThan(0).WithMessage("Debe indicar el tipo de mantenimiento.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Describa qué se hizo.")
            .MaximumLength(4000);

        RuleFor(x => x.PartsUsed).MaximumLength(2000).When(x => x.PartsUsed is not null);

        // Se registra lo ya realizado (no a futuro; un día de holgura por zona horaria).
        RuleFor(x => x.PerformedAt)
            .Must(date => date <= DateTime.UtcNow.AddDays(1))
            .WithMessage("La fecha de realización no puede ser futura.");

        RuleFor(x => x.NextMaintenanceAt)
            .Must((cmd, next) => next!.Value > cmd.PerformedAt)
            .WithMessage("La próxima fecha debe ser posterior a la realización.")
            .When(x => x.NextMaintenanceAt.HasValue);

        RuleFor(x => x.IncidentId)
            .NotNull().WithMessage("Para resolver el incidente, liga el correctivo a ese incidente.")
            .When(x => x.ResolveIncident);

        RuleFor(x => x.IncidentSolution).MaximumLength(4000).When(x => x.IncidentSolution is not null);

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.")
            .LessThan(10_000_000_000m)
            .When(x => x.Cost.HasValue);
    }
}
