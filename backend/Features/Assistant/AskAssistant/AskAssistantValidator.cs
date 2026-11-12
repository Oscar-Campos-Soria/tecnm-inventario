using FluentValidation;

namespace TecnmInventario.Api.Features.Assistant.AskAssistant;

public class AskAssistantValidator : AbstractValidator<AskAssistantCommand>
{
    public const int MaxQuestion = 500;

    public AskAssistantValidator()
    {
        RuleFor(x => x.Question).NotEmpty().WithMessage("Escribe una pregunta.")
            .MaximumLength(MaxQuestion).WithMessage($"La pregunta no puede pasar de {MaxQuestion} caracteres.");
    }
}
