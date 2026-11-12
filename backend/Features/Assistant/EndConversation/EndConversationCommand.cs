using Mediator;

namespace TecnmInventario.Api.Features.Assistant.EndConversation;

/// <summary>Borra la memoria de una conversación (botón "nueva conversación"). Solo la del propio usuario.</summary>
public sealed record EndConversationCommand(Guid ConversationId) : ICommand<Unit>;
