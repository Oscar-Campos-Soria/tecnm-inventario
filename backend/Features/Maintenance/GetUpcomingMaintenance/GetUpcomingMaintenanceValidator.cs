using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;

public class GetUpcomingMaintenanceValidator : AbstractValidator<GetUpcomingMaintenanceQuery>
{
    public GetUpcomingMaintenanceValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
    }
}
