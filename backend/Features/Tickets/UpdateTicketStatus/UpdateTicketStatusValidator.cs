using FluentValidation;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

public class UpdateTicketStatusValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del ticket que está viendo (expectedVersion).");

        RuleFor(x => x.NewStatus)
            .NotEmpty()
            .Must(code => code is not null
                && TicketStatusTransitions.ManualTargets.Contains(code.Trim().ToUpperInvariant()))
            .WithMessage("Estado destino no válido. Para resolver use el endpoint /resolve.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null);
    }
}
