using Mediator;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.AskAssistant;

/// <summary>Pregunta al asistente. Solo lee (herramientas = queries existentes); no escribe en la BD.</summary>
public sealed record AskAssistantCommand(Guid? ConversationId, string Question) : ICommand<AskAssistantResponse>;

public sealed record AskAssistantResponse(Guid ConversationId, string Answer, IReadOnlyList<AssistantStep> Steps);
