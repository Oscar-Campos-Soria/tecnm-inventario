using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.AssignTicket;

public class AssignTicketValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del ticket que está viendo (expectedVersion).");

        RuleFor(x => x.AssignedToUserId)
            .GreaterThan(0).WithMessage("Debe indicar el técnico a asignar.");
    }
}
