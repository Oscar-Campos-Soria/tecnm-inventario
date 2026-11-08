using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.ResolveTicket;

public class ResolveTicketValidator : AbstractValidator<ResolveTicketCommand>
{
    public ResolveTicketValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del ticket que está viendo (expectedVersion).");

        RuleFor(x => x.Resolution)
            .NotEmpty().WithMessage("Describa qué se hizo para resolver el ticket.")
            .MaximumLength(4000);
    }
}
