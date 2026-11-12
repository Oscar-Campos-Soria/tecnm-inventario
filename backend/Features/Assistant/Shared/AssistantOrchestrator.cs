using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>Lo que hizo el asistente en una pregunta: herramienta, qué consultó (en palabras), datos o error.</summary>
public sealed record AssistantStep(string Tool, string Summary, JsonNode? Data, string? Error);

/// <summary>
/// Pensar → consultar → responder. El modelo elige herramientas (tool calling nativo de llama.cpp); la API
/// valida argumentos, revisa el permiso y ejecuta queries existentes; el modelo redacta con esos resultados.
/// Solo lectura: ninguna herramienta envía comandos y el modelo nunca escribe SQL.
/// </summary>
public sealed partial class AssistantOrchestrator
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^\W*(como|se puede|puedo|que pasa|que significa|que es un|que es una|cual es la diferencia|diferencia|para que sirve|quien puede|donde)\b")]
    private static partial System.Text.RegularExpressions.Regex HowToQuestion();

    [System.Text.RegularExpressions.GeneratedRegex(@"\bmis (tickets|solicitudes|reportes|pendientes)\b")]
    private static partial System.Text.RegularExpressions.Regex MyTicketsQuestion();

    public const int MaxRounds = 4;
    public const int MaxToolChars = 3000;
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(120);
    // Turnos recientes completos (con resultados de herramientas); los anteriores solo pregunta y respuesta.
    private const int DetailedTurns = 2;

    private readonly ILocalAiClient _ai;
    private readonly IEnumerable<IAssistantTool> _tools;
    private readonly IRolePermissionService _permissions;
    private readonly IMediator _mediator;
    private readonly TecnmInventarioDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(ILocalAiClient ai, IEnumerable<IAssistantTool> tools, IRolePermissionService permissions,
        IMediator mediator, TecnmInventarioDbContext db, TimeProvider clock, ILogger<AssistantOrchestrator> logger)
    {
        _ai = ai;
        _tools = tools;
        _permissions = permissions;
        _mediator = mediator;
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Herramientas que el rol puede usar: el modelo ni siquiera ve las demás.</summary>
    public async Task<IReadOnlyList<IAssistantTool>> AllowedToolsAsync(string roleCode, CancellationToken ct)
    {
        var allowed = new List<IAssistantTool>();
        foreach (var tool in _tools)
            if (tool.Permission is null || await _permissions.HasPermissionAsync(roleCode, tool.Permission, ct))
                allowed.Add(tool);
        return allowed;
    }

    public async Task<(string Answer, IReadOnlyList<AssistantStep> Steps)> AskAsync(
        Conversation conversation, string roleCode, string question, CancellationToken ct)
    {
        var caller = ct;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
        budget.CancelAfter(Budget);
        ct = budget.Token;

        var now = _clock.GetUtcNow().UtcDateTime;
        var catalog = await AiCatalog.LoadAsync(_db, ct);
        var tools = await AllowedToolsAsync(roleCode, ct);
        var granted = new HashSet<string>();
        foreach (var permission in _tools.Select(t => t.Permission).OfType<string>().Distinct())
            if (await _permissions.HasPermissionAsync(roleCode, permission, ct)) granted.Add(permission);
        var userText = string.Join(" ", conversation.Turns.TakeLast(3).Select(t => t.Question).Append(question));
        var context = new ToolContext(_mediator, _db, catalog, now, granted, userText, question);
        var missing = _tools.Where(t => !tools.Contains(t)).Select(t => t.Name).ToList();
        var byName = tools.ToDictionary(t => t.Name);
        // Las prohibidas también se muestran, sin parámetros: si el modelo las pide recibe "sin permiso" (no se ejecuta
        // nada) y responde con eso, en vez de inventar una respuesta con otra herramienta.
        var definitions = new JsonArray(_tools.Select(t => (JsonNode)new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = tools.Contains(t) ? t.Description : $"{t.Description} (el rol {roleCode} no tiene permiso)",
                ["parameters"] = tools.Contains(t) ? t.Parameters(catalog) : ToolSchema.Object(),
            },
        }).ToArray());

        var messages = new JsonArray { Msg("system", SystemPrompt(context, roleCode, missing)) };
        for (var i = 0; i < conversation.Turns.Count; i++)
        {
            var turn = conversation.Turns[i];
            messages.Add(Msg("user", turn.Question));
            if (i >= conversation.Turns.Count - DetailedTurns)
                foreach (var m in turn.Work) messages.Add(m.DeepClone());
            messages.Add(Msg("assistant", turn.Answer));
        }
        messages.Add(Msg("user", question));

        var steps = new List<AssistantStep>();
        var work = new List<JsonObject>();
        var done = new Dictionary<string, string>();
        string? answer = null;

        // Consultas previas donde el modelo de 2B elige mal (medido en la evaluación): en "cómo…", "¿se puede…?",
        // "¿qué pasa…?" responde de memoria, y "mis tickets" lo confunde con mis_equipos. Se consulta antes y se le
        // entrega como si él lo hubiera pedido.
        var plain = AiText.Plain(question);
        var prefetch = new List<(string Tool, JsonObject Args)>();
        if (HowToQuestion().IsMatch(plain)) prefetch.Add(("buscar_manual", new JsonObject { ["consulta"] = question }));
        if (MyTicketsQuestion().IsMatch(plain)) prefetch.Add(("buscar_tickets", new JsonObject()));
        foreach (var (tool, args) in prefetch.Where(p => byName.ContainsKey(p.Tool)))
        {
            var call = new AiToolCall($"pre{steps.Count}", tool, args.ToJsonString());
            var (step, content) = await RunAsync(call, byName, context, ct);
            steps.Add(step);
            done[call.Name + call.Arguments] = content;
            var request = new JsonObject
            {
                ["role"] = "assistant",
                ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = call.Id, ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.Arguments },
                }),
            };
            var result = new JsonObject { ["role"] = "tool", ["tool_call_id"] = call.Id, ["content"] = content };
            foreach (var m in new[] { request, result })
            {
                messages.Add(m.DeepClone());
                work.Add(m);
            }
        }
        try
        {
            for (var round = 0; round < MaxRounds && answer is null; round++)
            {
                var reply = TextToolCalls.Recover(await _ai.ChatAsync(messages, definitions, ct));
                if (reply.ToolCalls.Count == 0)
                {
                    answer = reply.Content;
                    break;
                }
                messages.Add(reply.Message.DeepClone());
                work.Add(reply.Message);
                foreach (var call in reply.ToolCalls)
                {
                    // El modelo a veces repite la misma consulta: se reusa el resultado.
                    var key = call.Name + call.Arguments;
                    if (!done.TryGetValue(key, out var content))
                    {
                        var (step, result) = await RunAsync(call, byName, context, ct);
                        steps.Add(step);
                        content = result;
                        done[key] = content;
                    }
                    var toolMessage = new JsonObject { ["role"] = "tool", ["tool_call_id"] = call.Id, ["content"] = content };
                    messages.Add(toolMessage.DeepClone());
                    work.Add(toolMessage);
                }
            }
            // Se acabaron las vueltas: que responda con lo que ya consultó, sin más herramientas.
            answer ??= (await _ai.ChatAsync(messages, null, ct)).Content;
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            throw new BusinessRuleViolationException("El asistente tardó demasiado. Intenta con una pregunta más concreta.");
        }

        // Una llamada a herramienta escrita como texto nunca se le muestra al usuario.
        if (string.IsNullOrWhiteSpace(answer) || answer.Contains("<function=", StringComparison.Ordinal) || answer.Contains("<tool_call>", StringComparison.Ordinal))
            answer = steps.Count == 0
                ? "No pude generar una respuesta. Intenta reformular la pregunta."
                : "No pude completar la respuesta. Esto es lo que consulté: " + string.Join("; ", steps.Select(s => s.Summary)) + ". Intenta una pregunta más concreta.";
        answer = answer.Trim();
        conversation.Add(new ConversationTurn(question, work, answer));
        return (answer, steps);
    }

    private async Task<(AssistantStep Step, string Content)> RunAsync(
        AiToolCall call, IReadOnlyDictionary<string, IAssistantTool> tools, ToolContext context, CancellationToken ct)
    {
        if (!tools.TryGetValue(call.Name, out var tool))
            return Fail(call.Name, _tools.Any(t => t.Name == call.Name)
                ? $"El rol del usuario no tiene permiso para consultar esto ({call.Name}). Díselo; no respondas con otra información."
                : $"La herramienta «{call.Name}» no existe.");

        JsonObject args;
        try
        {
            args = JsonNode.Parse(string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return Fail(call.Name, "Los argumentos no son JSON válido.");
        }

        var watch = Stopwatch.StartNew();
        try
        {
            var result = await tool.ExecuteAsync(new ToolArgs(args), context, ct);
            _logger.LogInformation("Asistente: {Tool} en {Ms} ms", call.Name, watch.ElapsedMilliseconds);
            var content = result.Data.ToJsonString();
            if (content.Length > MaxToolChars)
                content = content[..MaxToolChars] + " …(recortado; usa el total y pide filtros más específicos)";
            return (new AssistantStep(call.Name, result.Summary, result.Data, null), content);
        }
        catch (Exception ex) when (ex is ToolArgumentException or KeyNotFoundException or BusinessRuleViolationException
                                       or ForbiddenAccessException or ValidationException)
        {
            return Fail(call.Name, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Asistente: falló la herramienta {Tool}", call.Name);
            return Fail(call.Name, "Error interno al consultar.");
        }
    }

    private static (AssistantStep, string) Fail(string tool, string error) =>
        (new AssistantStep(tool, "No se pudo consultar", null, error), new JsonObject { ["error"] = error }.ToJsonString());

    private static JsonObject Msg(string role, string content) => new() { ["role"] = role, ["content"] = content };

    private static readonly Dictionary<string, string> Areas = new()
    {
        ["buscar_incidentes"] = "incidentes", ["historial_activo"] = "historial de activos",
        ["mantenimientos_proximos"] = "mantenimientos", ["resumen_inventario"] = "resumen general del inventario",
        ["buscar_tickets"] = "tickets", ["buscar_activos"] = "activos", ["detalle_activo"] = "fichas de activos",
    };

    private static string SystemPrompt(ToolContext context, string roleCode, IReadOnlyList<string> missing) => $"""
        Eres el asistente del sistema de inventario de activos tecnológicos del TecNM. Respondes en español, breve y claro.
        Hoy es {context.Today:yyyy-MM-dd} (hora de México). El usuario tiene el rol {roleCode}.
        Reglas:
        - Para cualquier dato del sistema (activos, incidentes, tickets, mantenimientos, historial) usa las herramientas. Nunca inventes datos, cifras, códigos ni nombres, y no afirmes que algo existe o no existe sin haberlo consultado.
        - Para "cómo se hace…", pantallas, reglas o permisos usa buscar_manual. Para "qué pasó hoy" o "qué hay de nuevo" usa novedades.
        - Si la pregunta pide varias cosas, consulta todas antes de responder.
        - En cada herramienta pon todos los filtros que el usuario mencionó (tipo, ubicación, estado, marca, tema) y ninguno que no haya mencionado.
        - Responde solo con lo que devolvieron las herramientas. Si "total" es mayor que los elementos mostrados, menciona el total. Si no hay resultados, dilo.
        - Para preguntas de seguimiento ("¿y cuáles…?", "¿quién la tiene?") usa los resultados anteriores o vuelve a consultar con el filtro nuevo.
        - No puedes crear, modificar ni borrar nada. Si te lo piden, explica que se hace desde la pantalla correspondiente.
        - Si no hay una herramienta para lo que piden, dilo: puede ser que su rol no tenga ese permiso.{(missing.Count == 0 ? "" : $"\n        - El rol {roleCode} NO tiene permiso para consultar: {string.Join(", ", missing.Select(m => Areas.GetValueOrDefault(m, m)).Distinct())}. Si preguntan por eso, responde que su rol no tiene permiso para verlo; no respondas con otra información.")}
        - Si la pregunta no tiene que ver con el sistema, explica amablemente que solo ayudas con el inventario.
        """;
}
