using Mediator;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.EndConversation;

public sealed class EndConversationHandler : ICommandHandler<EndConversationCommand, Unit>
{
    private readonly ConversationStore _conversations;
    private readonly ICurrentUserService _currentUser;

    public EndConversationHandler(ConversationStore conversations, ICurrentUserService currentUser)
    {
        _conversations = conversations;
        _currentUser = currentUser;
    }

    public ValueTask<Unit> Handle(EndConversationCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        if (!_conversations.Remove(userId, command.ConversationId))
            throw new KeyNotFoundException("La conversación no existe o ya expiró.");
        return ValueTask.FromResult(Unit.Value);
    }
}
