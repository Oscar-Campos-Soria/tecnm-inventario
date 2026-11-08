using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.AddTicketComment;

public class AddTicketCommentValidator : AbstractValidator<AddTicketCommentCommand>
{
    public AddTicketCommentValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("El comentario no puede estar vacío.")
            .MaximumLength(4000);
    }
}
