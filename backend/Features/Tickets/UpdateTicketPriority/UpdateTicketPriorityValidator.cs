using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketPriority;

public class UpdateTicketPriorityValidator : AbstractValidator<UpdateTicketPriorityCommand>
{
    public UpdateTicketPriorityValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del ticket que está viendo (expectedVersion).");

        RuleFor(x => x.PriorityId)
            .GreaterThan(0).WithMessage("Debe indicar la prioridad.");
    }
}
