using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mediator;
using Microsoft.Extensions.Caching.Memory;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.AskAssistant;

public sealed partial class AskAssistantHandler : ICommandHandler<AskAssistantCommand, AskAssistantResponse>
{
    // Misma primera pregunta del mismo usuario: se responde sin el modelo (cada respuesta cuesta segundos y ~2-3 mil tokens).
    // shortcut: la respuesta puede tener hasta 5 min de antigüedad; si se nota, bajar CacheFor o invalidar al escribir.
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private readonly ILocalAiClient _ai;
    private readonly ConversationStore _conversations;
    private readonly AssistantOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AskAssistantHandler> _logger;

    public AskAssistantHandler(ILocalAiClient ai, ConversationStore conversations, AssistantOrchestrator orchestrator,
        ICurrentUserService currentUser, IMemoryCache cache, ILogger<AskAssistantHandler> logger)
    {
        _ai = ai;
        _conversations = conversations;
        _orchestrator = orchestrator;
        _currentUser = currentUser;
        _cache = cache;
        _logger = logger;
    }

    public async ValueTask<AskAssistantResponse> Handle(AskAssistantCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        var role = _currentUser.RoleCode ?? throw new ForbiddenAccessException("Sesión no válida.");
        // Sin caracteres invisibles: lo que el modelo lee es lo mismo que la persona ve.
        var question = AiText.Clean(command.Question).Trim();
        if (question.Length == 0) throw new BusinessRuleViolationException("Escribe una pregunta.");
        if (!await _ai.IsAvailableAsync(cancellationToken))
            throw new BusinessRuleViolationException("El asistente IA no está disponible en este momento.");

        var conversation = _conversations.GetOrStart(userId, command.ConversationId);
        // Una pregunta a la vez por conversación: la siguiente depende de la memoria de la anterior.
        await conversation.Lock.WaitAsync(cancellationToken);
        try
        {
            // Por usuario y rol (nunca se cruzan datos entre personas) y solo sin conversación previa: el seguimiento
            // ("¿y cuáles…?") depende de lo anterior.
            var key = conversation.Turns.Count == 0 ? $"assistant:answer:{userId}:{role}:{Spaces().Replace(AiText.Plain(question), " ")}" : null;
            if (key is not null && _cache.TryGetValue(key, out CachedAnswer? hit) && hit is not null)
            {
                conversation.Add(new ConversationTurn(question, hit.Work.Select(w => (JsonObject)w.DeepClone()).ToList(), hit.Answer));
                _logger.LogInformation("Asistente: pregunta rol={Role} desde caché", role);
                return new AskAssistantResponse(conversation.Id, hit.Answer, hit.Steps);
            }

            var (answer, steps) = await _orchestrator.AskAsync(conversation, role, question, cancellationToken);
            // Un borrador de ticket no se repite (se crearía dos veces) ni se guarda una respuesta con errores.
            if (key is not null && steps.All(s => s.Error is null && s.Tool != "proponer_ticket"))
                _cache.Set(key, new CachedAnswer(answer, steps, conversation.Turns[^1].Work.Select(w => (JsonObject)w.DeepClone()).ToList()), CacheFor);
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

    private sealed record CachedAnswer(string Answer, IReadOnlyList<AssistantStep> Steps, IReadOnlyList<JsonObject> Work);
}
