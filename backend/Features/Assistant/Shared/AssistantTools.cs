using System.Text.Json.Nodes;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Features.Assets.Assignments.GetAssetAssignments;
using TecnmInventario.Api.Features.Assets.GetAssetById;
using TecnmInventario.Api.Features.Assets.GetAssets;
using TecnmInventario.Api.Features.Assets.GetMyAssets;
using TecnmInventario.Api.Features.Dashboard.GetSummary;
using TecnmInventario.Api.Features.History.GetAssetTimeline;
using TecnmInventario.Api.Features.Incidents.GetIncidents;
using TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;
using TecnmInventario.Api.Features.Tickets.GetTickets;
using static TecnmInventario.Api.Features.Assistant.Shared.ToolSchema;

namespace TecnmInventario.Api.Features.Assistant.Shared;

// Herramientas del asistente: cada una envía una query existente (sus validadores y filtros por
// usuario aplican igual que desde la pantalla) y devuelve un resultado corto. Pocas y anchas: cada
// definición cuesta ~180 tokens de los 4096 del turno.

internal static class ToolLimits
{
    public const int Rows = 15;
}

public sealed class SearchAssetsTool : IAssistantTool
{
    private static readonly string[] Statuses =
        [AssetStatusCodes.Disponible, AssetStatusCodes.Asignado, AssetStatusCodes.EnMantenimiento, AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja];

    public string Name => "buscar_activos";
    public string? Permission => "assets.view";
    public string Description => "Busca y cuenta equipos (activos) del inventario: por tipo, estado, marca, ubicación, código, serie u hostname. Úsala para 'equipos con falla' (estado CON_FALLA), disponibles, asignados o dados de baja. Devuelve el total y hasta 15 equipos.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("texto", Text("código de inventario, número de serie o hostname (parcial), ej. ZZ-IA-01, MXL123, sw-core")),
        ("tipo", AssetType(catalog)),
        ("estado", OneOf(Statuses, "CON_FALLA = equipos con falla (tienen incidentes abiertos); DADO_DE_BAJA = dados de baja")),
        ("marca", Text("fabricante, ej. HP, Dell, Lenovo")),
        ("ubicacion", Text("lugar: edificio, planta, laboratorio u oficina (incluye lo que contiene)")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var type = args.Enum("tipo", ctx.Catalog.Types.Select(t => t.Code));
        if (type is not null && !ctx.Mentioned(type)) type = null; // tipo que nadie pidió
        var status = args.Enum("estado", Statuses);
        var location = ctx.Location(args.Str("ubicacion") ?? ctx.MentionedLocation());
        var maker = ctx.Manufacturer(args.Str("marca"));
        var text = args.Str("texto");
        var page = await ctx.Mediator.Send(new GetAssetsQuery(1, ToolLimits.Rows, text, status, ctx.TypeId(type),
            location?.Id, false, maker?.Id), ct);

        // Equipos con falla: se agrega qué falla tienen (si el rol puede ver incidentes) para no obligar a otra consulta.
        var faults = new Dictionary<string, string>();
        if (ctx.Can("incidents.view") && page.Items.Any(a => a.StatusCode == AssetStatusCodes.ConFalla))
        {
            var open = await ctx.Mediator.Send(new GetIncidentsQuery(1, 100, null, null, null, null, null, true, null), ct);
            foreach (var group in open.Items.GroupBy(i => i.AssetInventoryCode))
                faults[group.Key] = string.Join("; ", group.Select(i => i.Title));
        }

        var filters = Describe(("texto", text), ("tipo", type), ("estado", status), ("marca", maker?.Name), ("ubicación", location?.Name));
        return new ToolResult($"Activos{filters} → {page.TotalCount}", new JsonObject
        {
            ["total"] = page.TotalCount,
            ["activos"] = new JsonArray(page.Items.Select(a => (JsonNode)new JsonObject
            {
                ["codigo"] = a.InventoryCode, ["tipo"] = a.AssetTypeName, ["marca"] = a.ManufacturerName,
                ["modelo"] = a.ModelName, ["estado"] = a.StatusCode, ["ubicacion"] = a.LocationName,
                ["serie"] = a.SerialNumber, ["hostname"] = a.Hostname, ["falla_abierta"] = faults.GetValueOrDefault(a.InventoryCode),
            }).ToArray()),
        });
    }

    internal static string Describe(params (string Label, string? Value)[] filters)
    {
        var used = filters.Where(f => f.Value is not null).Select(f => $"{f.Label} {f.Value}").ToList();
        return used.Count == 0 ? "" : ": " + string.Join(" · ", used);
    }
}

public sealed class AssetDetailTool : IAssistantTool
{
    public string Name => "detalle_activo";
    public string? Permission => "assets.view";
    public string Description => "Ficha de un activo por su código de inventario: tipo, marca, modelo, serie, estado, ubicación y a quién está asignado.";

    public JsonObject Parameters(AiCatalog catalog) =>
        Object(("codigo", Text("código de inventario, ej. TEC-2026-0001"))).Required("codigo");

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var id = ctx.AssetId(args.Str("codigo"));
        var asset = await ctx.Mediator.Send(new GetAssetByIdQuery(id), ct);
        var listed = (await ctx.Mediator.Send(new GetAssetsQuery(1, 1, asset.InventoryCode, IncludeDisposed: true), ct)).Items.FirstOrDefault();
        var assignment = (await ctx.Mediator.Send(new GetAssetAssignmentsQuery(id), ct)).FirstOrDefault(a => a.IsActive);
        return new ToolResult($"Ficha de {asset.InventoryCode}", new JsonObject
        {
            ["codigo"] = asset.InventoryCode, ["tipo"] = asset.AssetTypeCode, ["marca"] = asset.ManufacturerName,
            ["modelo"] = asset.ModelName, ["serie"] = asset.SerialNumber, ["hostname"] = asset.Hostname,
            ["estado"] = asset.StatusCode, ["ubicacion"] = listed?.LocationName, ["notas"] = asset.Notes,
            ["asignado_a"] = assignment?.AssignedToUsername,
            ["asignado_desde"] = assignment is null ? null : ctx.Local(assignment.AssignedAt),
        });
    }
}

public sealed class AssetHistoryTool : IAssistantTool
{
    private static readonly Dictionary<string, string> Tables = new()
    {
        ["assets"] = "activo", ["asset_assignments"] = "asignación", ["asset_movements"] = "movimiento",
        ["incidents"] = "incidente", ["maintenance_records"] = "mantenimiento", ["asset_disposals"] = "baja",
        ["asset_components"] = "componente", ["documents"] = "documento", ["acquisition_assets"] = "compra",
    };

    public string Name => "historial_activo";
    public string? Permission => "history.view";
    public string Description => "Historial de cambios de un activo (altas, movimientos, asignaciones, incidentes, mantenimientos), del más reciente al más antiguo.";

    public JsonObject Parameters(AiCatalog catalog) =>
        Object(("codigo", Text("código de inventario del activo"))).Required("codigo");

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var code = args.Str("codigo");
        var id = ctx.AssetId(code);
        var events = await ctx.Mediator.Send(new GetAssetTimelineQuery(id), ct);
        var recent = events.OrderByDescending(e => e.PerformedAt).Take(ToolLimits.Rows).ToList();
        // El historial dice "asignación creada" pero no a quién: se agrega la asignación vigente.
        var assignment = (await ctx.Mediator.Send(new GetAssetAssignmentsQuery(id), ct)).FirstOrDefault(a => a.IsActive);
        return new ToolResult($"Historial de {code} → {events.Count} eventos", new JsonObject
        {
            ["total"] = events.Count,
            ["asignado_actualmente_a"] = assignment?.AssignedToUsername,
            ["eventos"] = new JsonArray(recent.Select(e => (JsonNode)new JsonObject
            {
                ["fecha"] = ctx.Local(e.PerformedAt),
                ["que"] = $"{Tables.GetValueOrDefault(e.TableName, e.TableName)} {Verb(e.Action)}",
                ["usuario"] = e.PerformedByUsername,
            }).ToArray()),
        });
    }

    private static string Verb(string action) => action switch { "INSERT" => "creado", "UPDATE" => "modificado", _ => "eliminado" };
}

public sealed class SearchIncidentsTool : IAssistantTool
{
    private static readonly string[] Statuses =
        [IncidentStatusCodes.Abierto, IncidentStatusCodes.EnDiagnostico, IncidentStatusCodes.EnReparacion, IncidentStatusCodes.Resuelto, IncidentStatusCodes.Cerrado];
    private static readonly string[] Priorities =
        [IncidentPriorityCodes.Baja, IncidentPriorityCodes.Media, IncidentPriorityCodes.Alta, IncidentPriorityCodes.Critica];

    public string Name => "buscar_incidentes";
    public string? Permission => "incidents.view";
    public string Description => "Incidentes: el registro técnico de cada falla reportada, con título, prioridad y estado. Filtra por un activo, por tipo de equipo, abiertos, estado, prioridad, fecha o palabras del título. Para contar equipos con falla usa buscar_activos con estado CON_FALLA.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("codigo_activo", Text("código de inventario de UN activo, ej. ZZ-IA-06 (no un tipo)")),
        ("tipo_activo", AssetType(catalog, "solo si el usuario menciona un tipo de equipo")),
        ("solo_abiertos", Flag("solo los que no están resueltos ni cerrados")),
        ("estado", OneOf(Statuses)),
        ("prioridad", OneOf(Priorities)),
        ("desde", Text("reportados desde: hoy, ayer, semana, mes o AAAA-MM-DD")),
        ("tema", Text("tema o palabra clave de la falla, ej. pantalla, papel, wifi")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var code = args.Str("codigo_activo");
        var assetType = args.Enum("tipo_activo", ctx.Catalog.Types.Select(t => t.Code));
        long? assetId = code is null ? null : ctx.AssetId(code);
        var status = args.Enum("estado", Statuses);
        var priority = args.Enum("prioridad", Priorities);
        var open = args.Bool("solo_abiertos");
        var since = args.Str("desde");
        var page = await ctx.Mediator.Send(new GetIncidentsQuery(1, ToolLimits.Rows, assetId, status, priority, null, null,
            open, args.Str("tema") ?? args.Str("texto"), ctx.Since(since), ctx.TypeId(assetType)), ct);

        var filters = SearchAssetsTool.Describe(("activo", code), ("tipo", assetType), ("abiertos", open == true ? "sí" : null),
            ("estado", status), ("prioridad", priority), ("desde", since), ("tema", args.Str("tema") ?? args.Str("texto")));
        return new ToolResult($"Incidentes{filters} → {page.TotalCount}", new JsonObject
        {
            ["total"] = page.TotalCount,
            ["incidentes"] = new JsonArray(page.Items.Select(i => (JsonNode)new JsonObject
            {
                ["id"] = i.IncidentId, ["activo"] = i.AssetInventoryCode, ["titulo"] = i.Title, ["estado"] = i.StatusCode,
                ["prioridad"] = i.PriorityCode, ["reportado"] = ctx.Local(i.ReportedAt), ["asignado_a"] = i.AssignedToUsername,
            }).ToArray()),
        });
    }
}

public sealed class SearchTicketsTool : IAssistantTool
{
    public string Name => "buscar_tickets";
    public string? Permission => "tickets.create";
    public string Description => "Tickets de la mesa de ayuda (solicitudes de ayuda). Úsala para \"mis tickets\" o \"mis solicitudes\": un usuario general solo ve los suyos. Si preguntan por un tema (wifi, impresora, internet, proyector), pon esa palabra en tema.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("solo_abiertos", Flag("solo los no terminados")),
        ("estado", OneOf(TicketStatusCodes.All)),
        ("sin_asignar", Flag("solo los que nadie atiende")),
        ("desde", Text("creados desde: hoy, ayer, semana, mes o AAAA-MM-DD")),
        ("tema", Text("tema o palabra clave del ticket, ej. wifi, impresora, proyector")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var status = args.Enum("estado", TicketStatusCodes.All);
        var open = args.Bool("solo_abiertos");
        var unassigned = args.Bool("sin_asignar");
        var since = args.Str("desde");
        var page = await ctx.Mediator.Send(new GetTicketsQuery(1, ToolLimits.Rows, status, null, null, null,
            unassigned, open, args.Str("tema") ?? args.Str("texto"), ctx.Since(since)), ct);

        var filters = SearchAssetsTool.Describe(("tema", args.Str("tema") ?? args.Str("texto")), ("abiertos", open == true ? "sí" : null),
            ("estado", status), ("sin asignar", unassigned == true ? "sí" : null), ("desde", since));
        return new ToolResult($"Tickets{filters} → {page.TotalCount}", new JsonObject
        {
            ["total"] = page.TotalCount,
            ["tickets"] = new JsonArray(page.Items.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.TicketId, ["titulo"] = t.Title, ["estado"] = t.Status, ["prioridad"] = t.PriorityCode,
                ["solicitante"] = t.RequesterUsername, ["asignado_a"] = t.AssignedToUsername,
                ["activo"] = t.AssetInventoryCode, ["creado"] = ctx.Local(t.CreatedAt),
            }).ToArray()),
        });
    }
}

public sealed class UpcomingMaintenanceTool : IAssistantTool
{
    public string Name => "mantenimientos_proximos";
    public string? Permission => "maintenance.manage";
    public string Description => "Mantenimientos preventivos que vencen en los próximos días o ya vencidos.";

    public JsonObject Parameters(AiCatalog catalog) => Object(("dias", Number("ventana en días, 1 a 365 (30 si no se indica)")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var days = Math.Clamp(args.Int("dias") ?? 30, 1, 365);
        var items = await ctx.Mediator.Send(new GetUpcomingMaintenanceQuery(days), ct);
        return new ToolResult($"Mantenimientos en los próximos {days} días → {items.Count}", new JsonObject
        {
            ["total"] = items.Count,
            ["mantenimientos"] = new JsonArray(items.Take(ToolLimits.Rows).Select(m => (JsonNode)new JsonObject
            {
                ["activo"] = m.AssetInventoryCode, ["tipo"] = m.TypeName,
                ["toca"] = m.NextMaintenanceAt is null ? null : ctx.Local(m.NextMaintenanceAt.Value),
                ["descripcion"] = m.Description,
            }).ToArray()),
        });
    }
}

public sealed class InventorySummaryTool : IAssistantTool
{
    public string Name => "resumen_inventario";
    public string? Permission => "dashboard.view";
    public string Description => "Panorama general: total de activos por estado, tipo y ubicación, incidentes abiertos por prioridad, mantenimientos y licencias por vencer.";

    public JsonObject Parameters(AiCatalog catalog) => Object();

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var s = await ctx.Mediator.Send(new GetDashboardSummaryQuery(), ct);
        static JsonObject Counts(IEnumerable<CountItem> items)
        {
            var o = new JsonObject();
            foreach (var i in items) o[i.Name] = i.Count;
            return o;
        }
        return new ToolResult("Resumen del inventario", new JsonObject
        {
            ["total_activos"] = s.TotalAssets,
            ["por_estado"] = Counts(s.AssetsByStatus),
            ["por_tipo"] = Counts(s.AssetsByType),
            ["por_ubicacion"] = Counts(s.AssetsByLocation.Take(10)),
            ["incidentes_abiertos_por_prioridad"] = Counts(s.OpenIncidentsByPriority),
            ["mantenimientos_vencidos"] = s.PreventiveOverdue,
            ["mantenimientos_por_vencer"] = s.PreventiveDueSoon,
            ["licencias_vencidas"] = s.LicensesExpired,
            ["licencias_por_vencer"] = s.LicensesExpiringSoon,
            ["ventana_dias"] = s.WindowDays,
        });
    }
}

public sealed class MyAssetsTool : IAssistantTool
{
    public string Name => "mis_equipos";
    public string? Permission => null;
    public string Description => "Los equipos asignados al usuario que pregunta (\"mi laptop\", \"mi equipo\", \"qué equipo tengo\"), con su estado y los tickets ligados a cada equipo. Para \"mis tickets\" usa buscar_tickets.";

    public JsonObject Parameters(AiCatalog catalog) => Object();

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var mine = await ctx.Mediator.Send(new GetMyAssetsQuery(), ct);
        return new ToolResult($"Mis equipos → {mine.Count}", new JsonObject
        {
            ["total"] = mine.Count,
            ["equipos"] = new JsonArray(mine.Select(a => (JsonNode)new JsonObject
            {
                ["codigo"] = a.InventoryCode, ["tipo"] = a.AssetTypeName, ["marca"] = a.ManufacturerName, ["modelo"] = a.ModelName,
                ["serie"] = a.SerialNumber, ["estado"] = a.StatusCode, ["ubicacion"] = a.LocationName,
                ["asignado_desde"] = ctx.Local(a.AssignedAt),
                ["tickets"] = new JsonArray(a.Tickets.Select(t => (JsonNode)new JsonObject
                {
                    ["id"] = t.TicketId, ["titulo"] = t.Title, ["estado"] = t.Status,
                }).ToArray()),
            }).ToArray()),
        });
    }
}

public sealed class NewsTool : IAssistantTool
{
    public string Name => "novedades";
    public string? Permission => null;
    public string Description => "Qué pasó o qué hay de nuevo: incidentes reportados y tickets creados desde una fecha (hoy si no se indica).";

    public JsonObject Parameters(AiCatalog catalog) => Object(("desde", Text("hoy, ayer, semana, mes o AAAA-MM-DD")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var since = args.Str("desde") ?? "hoy";
        var from = ctx.Since(since);
        var data = new JsonObject { ["desde"] = since };
        var parts = new List<string>();
        // Cada parte con su propio permiso: un usuario general no ve incidentes y solo ve sus tickets.
        if (ctx.Can("incidents.view"))
        {
            var incidents = await ctx.Mediator.Send(new GetIncidentsQuery(1, ToolLimits.Rows, null, null, null, null, null, null, null, from), ct);
            data["incidentes_nuevos"] = incidents.TotalCount;
            data["incidentes"] = new JsonArray(incidents.Items.Select(i => (JsonNode)new JsonObject
            {
                ["activo"] = i.AssetInventoryCode, ["titulo"] = i.Title, ["prioridad"] = i.PriorityCode, ["estado"] = i.StatusCode,
            }).ToArray());
            parts.Add($"{incidents.TotalCount} incidentes");
        }
        if (ctx.Can("tickets.create"))
        {
            var tickets = await ctx.Mediator.Send(new GetTicketsQuery(1, ToolLimits.Rows, null, null, null, null, null, null, null, from), ct);
            data["tickets_nuevos"] = tickets.TotalCount;
            data["tickets"] = new JsonArray(tickets.Items.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.TicketId, ["titulo"] = t.Title, ["estado"] = t.Status, ["activo"] = t.AssetInventoryCode,
            }).ToArray());
            parts.Add($"{tickets.TotalCount} tickets");
        }
        return new ToolResult($"Novedades desde {since} → {string.Join(", ", parts)}", data);
    }
}

public sealed class SearchManualTool(ManualIndex manual) : IAssistantTool
{
    public string Name => "buscar_manual";
    public string? Permission => null;
    public string Description => "Manual del sistema. Úsala SIEMPRE para: cómo se hace algo, qué significa algo, diferencias entre conceptos, qué pasa cuando…, reglas, estados, pantallas y qué puede hacer cada rol. No respondas eso de memoria.";

    public JsonObject Parameters(AiCatalog catalog) =>
        Object(("consulta", Text("qué se quiere saber, ej. cómo dar de baja un equipo"))).Required("consulta");

    public Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var query = args.Str("consulta") ?? throw new ToolArgumentException("Falta la consulta.");
        var found = manual.Search(query);
        return Task.FromResult(new ToolResult($"Manual: «{query}» → {string.Join(", ", found.Select(f => f.Title))}", new JsonObject
        {
            ["fragmentos"] = new JsonArray(found.Select(f => (JsonNode)new JsonObject
            {
                ["seccion"] = f.Title,
                ["texto"] = f.Text.Length > 900 ? f.Text[..900] + "…" : f.Text,
            }).ToArray()),
        }));
    }
}
