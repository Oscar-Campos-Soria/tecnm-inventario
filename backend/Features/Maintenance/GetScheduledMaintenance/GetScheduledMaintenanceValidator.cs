using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.GetScheduledMaintenance;

public class GetScheduledMaintenanceValidator : AbstractValidator<GetScheduledMaintenanceQuery>
{
    public GetScheduledMaintenanceValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 730).WithMessage("Los días deben estar entre 1 y 730.");
    }
}
