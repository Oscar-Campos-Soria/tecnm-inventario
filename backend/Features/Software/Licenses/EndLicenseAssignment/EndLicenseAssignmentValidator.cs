using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;

public class EndLicenseAssignmentValidator : AbstractValidator<EndLicenseAssignmentCommand>
{
    public EndLicenseAssignmentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión de la asignación que está liberando.");
    }
}
