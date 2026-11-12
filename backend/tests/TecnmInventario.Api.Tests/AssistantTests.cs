using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Tests;

/// <summary>Orquestador del asistente con un modelo simulado: permisos, varias herramientas, errores, vueltas y memoria.</summary>
public class AssistantTests
{
    private static AiToolCall Call(string name, string args = "{}") => new(Guid.NewGuid().ToString("N"), name, args);

    private static (AssistantOrchestrator Orchestrator, ScriptedAi Ai, CountingTool Assets, CountingTool Incidents) Build(
        params AiChatReply[] replies)
    {
        var db = TestDb.Create();
        db.AssetTypes.Add(new AssetType { Code = "LAPTOP", Name = "Laptop" });
        db.SaveChanges();
        var ai = new ScriptedAi(replies);
        var assets = new CountingTool("buscar_activos", "assets.view");
        var incidents = new CountingTool("buscar_incidentes", "incidents.view");
        var orchestrator = new AssistantOrchestrator(ai, [assets, incidents], new RolePermissions("assets.view"),
            null!, db, TimeProvider.System, NullLogger<AssistantOrchestrator>.Instance);
        return (orchestrator, ai, assets, incidents);
    }

    private static AiChatReply Tools(params AiToolCall[] calls)
    {
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments },
            }).ToArray()),
        };
        return new AiChatReply(null, calls, message);
    }

    private static AiChatReply Text(string text) => new(text, [], new JsonObject { ["role"] = "assistant", ["content"] = text });

    [Fact]
    public async Task Solo_ve_y_ejecuta_las_herramientas_que_su_rol_permite()
    {
        var (orchestrator, ai, assets, incidents) = Build(
            Tools(Call("buscar_activos", """{"tipo":"LAPTOP"}"""), Call("buscar_incidentes")),
            Text("Hay 3 laptops."));
        var conversation = new Conversation(1, DateTime.UtcNow);

        var (answer, steps) = await orchestrator.AskAsync(conversation, "USUARIO_GENERAL", "laptops e incidentes", CancellationToken.None);

        Assert.Equal("Hay 3 laptops.", answer);
        Assert.Equal(["buscar_activos", "buscar_incidentes"], ai.ToolNamesSeen[0]!); // la prohibida va sin parámetros
        Assert.Equal(1, assets.Runs);
        Assert.Equal(0, incidents.Runs);
        Assert.Contains("no tiene permiso", steps.Single(s => s.Tool == "buscar_incidentes").Error);
        // El modelo recibió los dos resultados (uno con error) antes de responder.
        var toolMessages = ai.MessagesSeen[1].OfType<JsonObject>().Where(m => (string?)m["role"] == "tool").ToList();
        Assert.Equal(2, toolMessages.Count);
        Assert.Contains("\"total\":3", (string?)toolMessages[0]["content"]);
    }

    [Fact]
    public async Task Argumentos_invalidos_vuelven_al_modelo_como_error_sin_ejecutar()
    {
        var (orchestrator, ai, assets, _) = Build(
            Tools(Call("buscar_activos", """{"tipo":"NAVE_ESPACIAL"}""")),
            Tools(Call("buscar_activos", "no es json")),
            Text("No encontré ese tipo."));

        var (_, steps) = await orchestrator.AskAsync(new Conversation(1, DateTime.UtcNow), "ADMIN", "naves", CancellationToken.None);

        Assert.Equal(0, assets.Runs);
        Assert.Contains("no es válido para tipo", steps[0].Error);
        Assert.Equal("Los argumentos no son JSON válido.", steps[1].Error);
    }

    [Fact]
    public async Task Consulta_repetida_se_reusa_y_al_agotar_vueltas_responde_sin_herramientas()
    {
        var same = """{"tipo":"LAPTOP"}""";
        var (orchestrator, ai, assets, _) = Build(
            Tools(Call("buscar_activos", same)), Tools(Call("buscar_activos", same)),
            Tools(Call("buscar_activos", same)), Tools(Call("buscar_activos", same)),
            Text("Respuesta final."));

        var (answer, steps) = await orchestrator.AskAsync(new Conversation(1, DateTime.UtcNow), "ADMIN", "x", CancellationToken.None);

        Assert.Equal("Respuesta final.", answer);
        Assert.Equal(1, assets.Runs);
        Assert.Single(steps);
        Assert.Equal(AssistantOrchestrator.MaxRounds + 1, ai.ToolNamesSeen.Count);
        Assert.Null(ai.ToolNamesSeen[^1]); // la última llamada va sin herramientas
    }

    [Fact]
    public async Task La_memoria_lleva_las_preguntas_anteriores_y_los_resultados_recientes()
    {
        var (orchestrator, ai, _, _) = Build(
            Tools(Call("buscar_activos", """{"tipo":"LAPTOP"}""")), Text("Hay 3 laptops."),
            Text("Las Lenovo son 1."));
        var conversation = new Conversation(1, DateTime.UtcNow);

        await orchestrator.AskAsync(conversation, "ADMIN", "¿cuántas laptops hay?", CancellationToken.None);
        await orchestrator.AskAsync(conversation, "ADMIN", "¿y cuáles son Lenovo?", CancellationToken.None);

        var second = ai.MessagesSeen[^1].OfType<JsonObject>().Select(m => ((string?)m["role"], (string?)m["content"])).ToList();
        Assert.Equal(("user", "¿cuántas laptops hay?"), second[1]);
        Assert.Contains(second, m => m.Item1 == "tool" && m.Item2!.Contains("\"total\":3"));
        Assert.Equal(("assistant", "Hay 3 laptops."), second[^2]);
        Assert.Equal(("user", "¿y cuáles son Lenovo?"), second[^1]);
        Assert.Equal(2, conversation.Turns.Count);
    }

    [Fact]
    public async Task Llamada_escrita_como_texto_se_ejecuta_y_la_prohibida_responde_sin_permiso()
    {
        var (orchestrator, ai, assets, incidents) = Build(
            Text("<tool_call>\n<function=buscar_activos>\n<parameter=tipo>\nLAPTOP\n</parameter>\n</function>\n</tool_call>"),
            Tools(Call("buscar_incidentes")),
            Text("Tu rol no puede ver incidentes."));

        var (answer, steps) = await orchestrator.AskAsync(new Conversation(1, DateTime.UtcNow), "USUARIO_GENERAL", "x", CancellationToken.None);

        Assert.Equal(1, assets.Runs);
        Assert.Equal(0, incidents.Runs);
        Assert.Contains("no tiene permiso", steps[1].Error);
        Assert.Equal("Tu rol no puede ver incidentes.", answer);
        // El modelo ve la prohibida pero sin parámetros.
        Assert.Equal(["buscar_activos", "buscar_incidentes"], ai.ToolNamesSeen[0]!);
    }

    [Fact]
    public void Tipo_que_nadie_menciono_no_cuenta_y_el_lugar_se_toma_de_la_pregunta()
    {
        var catalog = new AiCatalog(
            [("PC", "Computadora de Escritorio"), ("ROUTER", "Router"), ("PRINTER", "Impresora")],
            [("ED-B-P1-LAB", "Edificio B, Planta 1, Laboratorio de redes")]);
        var ctx = new ToolContext(null!, TestDb.Create(), catalog, DateTime.UtcNow, null,
            userText: "¿qué pcs e impresoras hay en el laboratorio de redes?", question: "¿qué pcs e impresoras hay en el laboratorio de redes?");

        Assert.True(ctx.Mentioned("PC"));
        Assert.True(ctx.Mentioned("PRINTER"));
        Assert.False(ctx.Mentioned("ROUTER"));
        Assert.Equal("ED-B-P1-LAB", ctx.MentionedLocation());
        Assert.Null(new ToolContext(null!, TestDb.Create(), catalog, DateTime.UtcNow, question: "¿cuántas laptops hay?").MentionedLocation());
    }

    [Fact]
    public async Task Pregunta_de_como_consulta_el_manual_antes_y_nunca_muestra_tool_call()
    {
        var db = TestDb.Create();
        var manual = new CountingTool("buscar_manual", null);
        var ai = new ScriptedAi(Text("<tool_call><function=buscar_manual><parameter=consulta>x</parameter></function></tool_call>"),
            Text("<tool_call><function=buscar_manual><parameter=consulta>x</parameter></function></tool_call>"),
            Text("<tool_call><function=buscar_manual><parameter=consulta>x</parameter></function></tool_call>"),
            Text("<tool_call><function=buscar_manual><parameter=consulta>x</parameter></function></tool_call>"),
            Text("<tool_call>sigue</tool_call>"));
        var orchestrator = new AssistantOrchestrator(ai, [manual], new RolePermissions(), null!, db, TimeProvider.System,
            NullLogger<AssistantOrchestrator>.Instance);

        var (answer, steps) = await orchestrator.AskAsync(new Conversation(1, DateTime.UtcNow), "ADMIN", "¿Cómo doy de baja un equipo?", CancellationToken.None);

        // El manual se consultó antes de la primera vuelta del modelo y su resultado ya iba en los mensajes.
        Assert.Equal("buscar_manual", steps[0].Tool);
        Assert.Contains(ai.MessagesSeen[0].OfType<JsonObject>(), m => (string?)m["role"] == "tool");
        Assert.DoesNotContain("<tool_call>", answer);
        Assert.StartsWith("No pude completar la respuesta", answer);
    }

    [Fact]
    public async Task Mis_tickets_se_consultan_antes_aunque_el_modelo_elija_otra_herramienta()
    {
        var tickets = new CountingTool("buscar_tickets", null);
        var mine = new CountingTool("mis_equipos", null);
        var ai = new ScriptedAi(Text("No tienes tickets."));
        var orchestrator = new AssistantOrchestrator(ai, [tickets, mine], new RolePermissions(), null!, TestDb.Create(),
            TimeProvider.System, NullLogger<AssistantOrchestrator>.Instance);

        var (_, steps) = await orchestrator.AskAsync(new Conversation(1, DateTime.UtcNow), "USUARIO_GENERAL", "¿Cuáles son mis tickets?", CancellationToken.None);

        Assert.Equal(["buscar_tickets"], steps.Select(s => s.Tool));
        Assert.Equal(1, tickets.Runs);
        Assert.Contains(ai.MessagesSeen[0].OfType<JsonObject>(), m => (string?)m["role"] == "tool");
    }

    [Fact]
    public void Memoria_solo_para_su_dueno()
    {
        var store = new ConversationStore(TimeProvider.System);
        var mine = store.GetOrStart(1, null);

        Assert.Same(mine, store.GetOrStart(1, mine.Id));
        Assert.NotSame(mine, store.GetOrStart(2, mine.Id)); // otro usuario recibe una nueva
        Assert.False(store.Remove(2, mine.Id));
        Assert.True(store.Remove(1, mine.Id));
    }

    [Fact]
    public void Fechas_y_listas_cerradas_se_interpretan_en_hora_de_Mexico()
    {
        var now = new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc); // 11-nov 21:00 en México
        var ctx = new ToolContext(null!, TestDb.Create(), new AiCatalog([], []), now);

        Assert.Equal(new DateOnly(2026, 11, 11), ctx.Today);
        Assert.Equal(new DateTime(2026, 11, 11, 6, 0, 0, DateTimeKind.Utc), ctx.Since("hoy"));
        Assert.Equal(new DateTime(2026, 11, 10, 6, 0, 0, DateTimeKind.Utc), ctx.Since("Ayer"));
        Assert.Equal(new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc), ctx.Since("2026-11-01"));
        Assert.Throws<ToolArgumentException>(() => ctx.Since("el otro día"));

        var args = new ToolArgs(new JsonObject { ["estado"] = "con falla" });
        Assert.Equal("CON_FALLA", args.Enum("estado", ["DISPONIBLE", "CON_FALLA"]));
    }

    private sealed class ScriptedAi(params AiChatReply[] replies) : ILocalAiClient
    {
        private int _next;
        public List<JsonArray> MessagesSeen { get; } = [];
        public List<string[]?> ToolNamesSeen { get; } = [];

        public Task<AiChatReply> ChatAsync(JsonArray messages, JsonArray? tools, CancellationToken ct)
        {
            MessagesSeen.Add((JsonArray)messages.DeepClone());
            ToolNamesSeen.Add(tools?.Select(t => (string)t!["function"]!["name"]!).ToArray());
            return Task.FromResult(replies[_next++]);
        }

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<JsonNode> CompleteJsonAsync(string system, string user, JsonObject schema, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(byte[] image, string mimeType, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CountingTool(string name, string? permission) : IAssistantTool
    {
        public int Runs { get; private set; }
        public string Name => name;
        public string? Permission => permission;
        public string Description => name;

        public JsonObject Parameters(AiCatalog catalog) => ToolSchema.Object(("tipo", ToolSchema.OneOf(catalog.Types.Select(t => t.Code))));

        public Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext context, CancellationToken ct)
        {
            args.Enum("tipo", context.Catalog.Types.Select(t => t.Code));
            Runs++;
            return Task.FromResult(new ToolResult("ok", new JsonObject { ["total"] = 3 }));
        }
    }

    private sealed class RolePermissions(params string[] userGeneral) : IRolePermissionService
    {
        public Task<bool> HasPermissionAsync(string roleCode, string permissionCode, CancellationToken cancellationToken) =>
            Task.FromResult(roleCode == "ADMIN" || userGeneral.Contains(permissionCode));

        public void InvalidateCache(string roleCode) { }
    }
}
