using FluentValidation;

namespace TecnmInventario.Api.Features.Disposals.CreateDisposal;

public class CreateDisposalValidator : AbstractValidator<CreateDisposalCommand>
{
    public CreateDisposalValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Debe indicar el activo.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del activo que está viendo (expectedVersion).");

        RuleFor(x => x.DisposalReasonId).GreaterThan(0).WithMessage("Debe indicar el motivo de la baja.");

        RuleFor(x => x.Folio)
            .NotEmpty().WithMessage("El folio del acta es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Dictamen).MaximumLength(4000).When(x => x.Dictamen is not null);

        // Una baja no se registra a futuro.
        RuleFor(x => x.DisposedAt)
            .Must(date => date!.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("La fecha de baja no puede ser futura.")
            .When(x => x.DisposedAt.HasValue);
    }
}
