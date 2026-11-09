using FluentValidation;

namespace TecnmInventario.Api.Features.Inventory.CancelInventorySession;

public class CancelInventorySessionValidator : AbstractValidator<CancelInventorySessionCommand>
{
    public CancelInventorySessionValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del conteo que está viendo (expectedVersion).");

        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5)
            .WithMessage("Explica por qué se cancela el conteo (al menos 5 caracteres).")
            .MaximumLength(500);
    }
}
