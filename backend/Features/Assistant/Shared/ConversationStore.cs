using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>Un turno: la pregunta, el trabajo con herramientas (mensajes assistant/tool) y la respuesta final.</summary>
public sealed record ConversationTurn(string Question, IReadOnlyList<JsonObject> Work, string Answer);

/// <summary>Una conversación de un usuario, con un candado para no responder dos preguntas a la vez.</summary>
public sealed class Conversation(long userId, DateTime now)
{
    public const int MaxTurns = 8;

    public Guid Id { get; } = Guid.NewGuid();
    public long UserId { get; } = userId;
    public DateTime LastUsed { get; set; } = now;
    public List<ConversationTurn> Turns { get; } = [];
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public void Add(ConversationTurn turn)
    {
        Turns.Add(turn);
        if (Turns.Count > MaxTurns) Turns.RemoveAt(0);
    }
}

/// <summary>
/// Memoria de conversación por usuario. Se borra a las 24 h sin uso y nunca se comparte entre usuarios.
/// shortcut: en memoria; se pierde si la API se reinicia (el usuario empieza otra). Pasar a la BD si hace falta.
/// </summary>
public sealed class ConversationStore(TimeProvider clock)
{
    public static readonly TimeSpan Keep = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<Guid, Conversation> _items = new();

    /// <summary>La conversación del usuario, o una nueva si no existe, expiró o es de otro.</summary>
    public Conversation GetOrStart(long userId, Guid? id)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in _items.Values.Where(c => now - c.LastUsed > Keep)) _items.TryRemove(old.Id, out _);

        if (id is { } wanted && _items.TryGetValue(wanted, out var found) && found.UserId == userId)
        {
            found.LastUsed = now;
            return found;
        }
        var conversation = new Conversation(userId, now);
        _items[conversation.Id] = conversation;
        return conversation;
    }

    public bool Remove(long userId, Guid id) =>
        _items.TryGetValue(id, out var c) && c.UserId == userId && _items.TryRemove(id, out _);
}
