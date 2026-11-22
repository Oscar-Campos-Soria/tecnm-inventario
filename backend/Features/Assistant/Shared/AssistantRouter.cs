using System.Text.Json.Nodes;
using TecnmInventario.Api.Common.Ai;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>
/// Qué hacer con un mensaje. Kind: consulta | accion | charla | aclarar | sin_permiso. Tools: capacidades elegidas
/// (solo de las permitidas: el esquema no admite otras). Reply: respuesta directa en charla y aclarar (sin otra
/// llamada al modelo). DeniedArea: en sin_permiso, el área que pidió.
/// </summary>
public sealed record RouteDecision(string Kind, IReadOnlyList<string> Tools, string DeniedArea, string Reply);

public static class RouteKinds
{
    public const string Charla = "charla";
    public const string Consulta = "consulta";
    public const string Accion = "accion";
    public const string Aclarar = "aclarar";
    public const string SinPermiso = "sin_permiso";

    public static readonly string[] All = [Consulta, Accion, Charla, Aclarar, SinPermiso];
}

/// <summary>
/// Router: una llamada corta al mismo modelo con salida JSON forzada por esquema (gramática de llama.cpp). Ve solo
/// el resumen de una línea de cada capacidad que el rol tiene y los nombres de las áreas que no tiene; no ve datos,
/// no ejecuta nada y no decide permisos (el backend los vuelve a revisar al ejecutar). Si falla, el orquestador sigue
/// sin router (todas las herramientas permitidas): nunca queda peor que antes.
/// Medido en CPU: cada token generado cuesta ~60 ms, así que el esquema (anyOf) solo pide lo que cada tipo necesita.
/// Sin maxLength: en la gramática de llama.cpp un tope de largo bajó la generación a 0.5 tokens/s; el tope lo pone max_tokens.
/// </summary>
public static class AssistantRouter
{
    public const int MaxTokens = 160;
    private const int ContextChars = 300;

    public static string SystemPrompt(string roleCode, IReadOnlyList<IAssistantTool> tools, IReadOnlyList<string> deniedAreas) => $"""
        Clasificas mensajes para el asistente del sistema de inventario y mesa de ayuda del TecNM. No respondes datos: decides qué hacer.
        Entiende errores de dedo, palabras coloquiales y frases sin acentos o incompletas; usa la conversación previa para entender seguimientos.
        tipo:
        - consulta: pide información del sistema: equipos, solicitudes o tickets, reparaciones, fallas, técnicos, mantenimientos, cifras, o cómo se hace algo.
        - accion: quiere crear o reportar algo: levantar un ticket, reportar una falla, pedir soporte.
        - charla: saludo, agradecimiento, despedida, plática, o un tema que no tiene que ver con el sistema.
        - aclarar: no se puede saber qué quiere ni con la conversación previa.
        - sin_permiso: pide información de un área que el rol no tiene (lista de abajo), y no es sobre lo suyo propio.
        En consulta y accion elige solo las herramientas necesarias (normalmente una). En charla responde breve y amable (si el tema es ajeno, di que solo ayudas con el inventario y la mesa de ayuda); en aclarar haz una pregunta concreta.
        Herramientas del rol {roleCode}:
        {string.Join("\n", tools.Select(t => $"- {t.Name}: {t.Summary}"))}
        Áreas sin permiso para el rol {roleCode}: {(deniedAreas.Count == 0 ? "ninguna" : string.Join(", ", deniedAreas))}.
        """;

    public static JsonObject Schema(IReadOnlyList<IAssistantTool> tools, IReadOnlyList<string> deniedAreas)
    {
        static JsonArray Values(IEnumerable<string> values) => new(values.Select(v => (JsonNode)v).ToArray());
        static JsonObject Shape(params (string Name, JsonObject Schema)[] props)
        {
            var properties = new JsonObject();
            foreach (var (name, schema) in props) properties[name] = schema;
            return new JsonObject
            {
                ["type"] = "object", ["properties"] = properties,
                ["required"] = Values(props.Select(p => p.Name)), ["additionalProperties"] = false,
            };
        }
        static JsonObject OneOf(IEnumerable<string> values) => new() { ["type"] = "string", ["enum"] = Values(values) };

        var options = new JsonArray
        {
            Shape(("tipo", OneOf([RouteKinds.Charla, RouteKinds.Aclarar])), ("respuesta", new JsonObject { ["type"] = "string" })),
        };
        if (tools.Count > 0)
            options.Add(Shape(("tipo", OneOf([RouteKinds.Consulta, RouteKinds.Accion])),
                ("herramientas", new JsonObject { ["type"] = "array", ["items"] = OneOf(tools.Select(t => t.Name)), ["minItems"] = 1, ["maxItems"] = 3 })));
        if (deniedAreas.Count > 0)
            options.Add(Shape(("tipo", OneOf([RouteKinds.SinPermiso])), ("area", OneOf(deniedAreas))));
        return new JsonObject { ["anyOf"] = options };
    }

    /// <summary>Lo que el router lee: el turno anterior (recortado) para los seguimientos y el mensaje.</summary>
    public static string UserPrompt(Conversation conversation, string question)
    {
        var last = conversation.Turns.LastOrDefault();
        if (last is null) return $"Mensaje: {question}";
        static string Clip(string text) => text.Length <= ContextChars ? text : text[..ContextChars] + "…";
        return $"Conversación previa:\nUsuario: {Clip(last.Question)}\nAsistente: {Clip(last.Answer)}\n\nMensaje: {question}";
    }

    /// <summary>Lee la decisión; descarta lo que no cuadre (herramienta ajena, tipo desconocido).</summary>
    public static RouteDecision? Parse(JsonNode? json, IReadOnlyList<IAssistantTool> tools, IReadOnlyList<string> deniedAreas)
    {
        if (json is not JsonObject o) return null;
        var kind = (o["tipo"] as JsonValue)?.ToString();
        if (kind is null || !RouteKinds.All.Contains(kind)) return null;
        var names = tools.Select(t => t.Name).ToHashSet();
        var chosen = (o["herramientas"] as JsonArray ?? []).Select(n => n?.ToString()).OfType<string>()
            .Where(names.Contains).Distinct().ToList();
        var area = (o["area"] as JsonValue)?.ToString() ?? "";
        if (!deniedAreas.Contains(area)) area = "";
        var reply = AiText.Clean((o["respuesta"] as JsonValue)?.ToString()).Trim();
        return new RouteDecision(kind, chosen, area, reply);
    }
}
