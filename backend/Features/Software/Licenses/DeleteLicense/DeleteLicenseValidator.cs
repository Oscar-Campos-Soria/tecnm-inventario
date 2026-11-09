using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Licenses.DeleteLicense;

public class DeleteLicenseValidator : AbstractValidator<DeleteLicenseCommand>
{
    public DeleteLicenseValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión de la licencia que está eliminando.");
    }
}
