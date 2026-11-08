using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.CreateTicket;

public class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Debe indicar el tipo de ticket.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.PriorityId).GreaterThan(0).When(x => x.PriorityId.HasValue);
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue);
        RuleFor(x => x.LocationId).GreaterThan(0).When(x => x.LocationId.HasValue);
    }
}
