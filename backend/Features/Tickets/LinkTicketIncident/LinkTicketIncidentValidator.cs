using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.LinkTicketIncident;

public class LinkTicketIncidentValidator : AbstractValidator<LinkTicketIncidentCommand>
{
    public LinkTicketIncidentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del ticket que está viendo (expectedVersion).");

        RuleFor(x => x.IncidentId)
            .GreaterThan(0).WithMessage("Debe indicar el incidente a vincular.");
    }
}
