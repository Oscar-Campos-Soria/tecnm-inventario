using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Licenses.AssignLicense;

public class AssignLicenseValidator : AbstractValidator<AssignLicenseCommand>
{
    public AssignLicenseValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Debe indicar el equipo.");
    }
}
