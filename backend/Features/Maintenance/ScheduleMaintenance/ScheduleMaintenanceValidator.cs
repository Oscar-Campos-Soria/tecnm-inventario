using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

public class ScheduleMaintenanceValidator : AbstractValidator<ScheduleMaintenanceCommand>
{
    public ScheduleMaintenanceValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Debe indicar el activo.");
        RuleFor(x => x.MaintenanceTypeId).GreaterThan(0).WithMessage("Debe indicar el tipo de mantenimiento.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Describe qué se va a hacer.").MaximumLength(4000);

        // A futuro (un día de holgura por zona horaria) y dentro de dos años.
        RuleFor(x => x.ScheduledAt)
            .Must(d => d >= DateTime.UtcNow.AddDays(-1)).WithMessage("La fecha programada no puede estar en el pasado.")
            .Must(d => d <= DateTime.UtcNow.AddYears(2)).WithMessage("Programa con a lo más dos años de anticipación.");
    }
}
