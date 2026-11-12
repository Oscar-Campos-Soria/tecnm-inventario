using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.AskAssistant;

public sealed class AskAssistantHandler : ICommandHandler<AskAssistantCommand, AskAssistantResponse>
{
    private readonly ILocalAiClient _ai;
    private readonly ConversationStore _conversations;
    private readonly AssistantOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUser;

    public AskAssistantHandler(ILocalAiClient ai, ConversationStore conversations, AssistantOrchestrator orchestrator, ICurrentUserService currentUser)
    {
        _ai = ai;
        _conversations = conversations;
        _orchestrator = orchestrator;
        _currentUser = currentUser;
    }

    public async ValueTask<AskAssistantResponse> Handle(AskAssistantCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        var role = _currentUser.RoleCode ?? throw new ForbiddenAccessException("Sesión no válida.");
        if (!await _ai.IsAvailableAsync(cancellationToken))
            throw new BusinessRuleViolationException("El asistente IA no está disponible en este momento.");

        var conversation = _conversations.GetOrStart(userId, command.ConversationId);
        // Una pregunta a la vez por conversación: la siguiente depende de la memoria de la anterior.
        await conversation.Lock.WaitAsync(cancellationToken);
        try
        {
            var (answer, steps) = await _orchestrator.AskAsync(conversation, role, command.Question.Trim(), cancellationToken);
            return new AskAssistantResponse(conversation.Id, answer, steps);
        }
        catch (AiUnavailableException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }
        finally
        {
            conversation.Lock.Release();
        }
    }
}
