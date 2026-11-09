using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;

public class UninstallSoftwareValidator : AbstractValidator<UninstallSoftwareCommand>
{
    public UninstallSoftwareValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión de la instalación que está quitando.");
    }
}
