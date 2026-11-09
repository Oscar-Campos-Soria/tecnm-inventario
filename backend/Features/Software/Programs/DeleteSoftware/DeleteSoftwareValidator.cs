using FluentValidation;

namespace TecnmInventario.Api.Features.Software.Programs.DeleteSoftware;

public class DeleteSoftwareValidator : AbstractValidator<DeleteSoftwareCommand>
{
    public DeleteSoftwareValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del programa que está eliminando.");
    }
}
