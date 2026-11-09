using FluentValidation;

namespace TecnmInventario.Api.Features.Components.DeleteComponent;

public class DeleteComponentValidator : AbstractValidator<DeleteComponentCommand>
{
    public DeleteComponentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del componente que está eliminando.");
    }
}
