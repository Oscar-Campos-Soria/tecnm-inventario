using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenses;

public class GetLicensesValidator : AbstractValidator<GetLicensesQuery>
{
    public GetLicensesValidator()
    {
        RuleFor(x => x.ExpiringInDays)
            .InclusiveBetween(0, 3650).When(x => x.ExpiringInDays is not null)
            .WithMessage("Los días para vencer deben estar entre 0 y 3650.");
    }
}
