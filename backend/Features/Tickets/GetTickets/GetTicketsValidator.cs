using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Tickets.GetTickets;

public class GetTicketsValidator : AbstractValidator<GetTicketsQuery>
{
    public GetTicketsValidator()
    {
        RuleFor(x => x.Status)
            .Must(code => TicketStatusCodes.All.Contains(code!.Trim().ToUpperInvariant()))
            .WithMessage("El estado de ticket especificado no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .When(x => x.Search is not null);

        RuleFor(x => x.OnlyOpen)
            .Must(_ => false)
            .When(x => x.OnlyOpen == true && !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Use 'status' o 'onlyOpen', pero no ambos a la vez.");

        RuleFor(x => x.Unassigned)
            .Must(_ => false)
            .When(x => x.Unassigned == true && x.AssignedTo.HasValue)
            .WithMessage("Use 'assignedTo' o 'unassigned', pero no ambos a la vez.");
    }
}
