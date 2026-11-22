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
using TecnmInventario.Api.Features.Tickets.GetTicketStats;
using TecnmInventario.Api.Features.Tickets.GetTicketWorkload;
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
    public string Summary => "Inventario de equipos: buscar, listar o contar por tipo, estado (con falla, disponible…), marca o ubicación.";
    public string Source => "GetAssetsQuery + GetIncidentsQuery";
    public string? Permission => "assets.view";
    public string Description => "Busca y cuenta equipos (activos) del inventario: por tipo, estado, marca, ubicación, código, serie u hostname. Úsala para 'equipos con falla' (estado CON_FALLA), disponibles, asignados o dados de baja. Devuelve el total, el conteo por estado del mismo filtro y hasta 15 equipos.";

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

        // Preguntas compuestas ("¿cuántas hay y cuáles fallan?"): los conteos por estado del mismo filtro salen de la BD,
        // así el total y el desglose no dependen de que el modelo haga dos consultas ni de que sume.
        var byStatus = new JsonObject();
        foreach (var code in Statuses.Where(c => c != AssetStatusCodes.DadoDeBaja))
        {
            var count = code == status ? page.TotalCount : (await ctx.Mediator.Send(new GetAssetsQuery(1, 1, text, code, ctx.TypeId(type),
                location?.Id, false, maker?.Id), ct)).TotalCount;
            if (count > 0) byStatus[code] = count;
        }

        var filters = Describe(("texto", text), ("tipo", type), ("estado", status), ("marca", maker?.Name), ("ubicación", location?.Name));
        return new ToolResult($"Activos{filters} → {page.TotalCount}", new JsonObject
        {
            ["total"] = page.TotalCount,
            // Sin el filtro de estado: el total del tipo/lugar/marca y cuántos hay en cada estado.
            ["total_sin_filtro_de_estado"] = status is null ? null : byStatus.Sum(p => (int)p.Value!),
            ["por_estado"] = byStatus,
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
    public string Summary => "Ficha de UN equipo por su código: datos, estado, ubicación y a quién está asignado.";
    public string Source => "GetAssetByIdQuery + GetAssetsQuery + GetAssetAssignmentsQuery";
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
    public string Summary => "Bitácora de cambios de UN equipo por su código: quién hizo qué y cuándo.";
    public string Source => "GetAssetTimelineQuery + GetAssetAssignmentsQuery";
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
    public string Summary => "Incidentes: fallas técnicas registradas de un equipo y su diagnóstico o reparación; por equipo, estado, prioridad, fecha, tema o asignados a mí.";
    public string Source => "GetIncidentsQuery";
    public string? Permission => "incidents.view";
    public string Description => "Incidentes: el registro técnico de cada falla reportada, con título, prioridad y estado. Filtra por un activo, por tipo de equipo, abiertos, estado, prioridad, fecha o palabras del título. Para contar equipos con falla usa buscar_activos con estado CON_FALLA.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("codigo_activo", Text("código de inventario de UN activo, ej. ZZ-IA-06 (no un tipo)")),
        ("tipo_activo", AssetType(catalog, "solo si el usuario menciona un tipo de equipo")),
        ("solo_abiertos", Flag("solo los que no están resueltos ni cerrados")),
        ("estado", OneOf(Statuses)),
        ("prioridad", OneOf(Priorities)),
        ("desde", Text("reportados desde: hoy, ayer, semana, mes o AAAA-MM-DD")),
        ("tema", Text("tema o palabra clave de la falla, ej. pantalla, papel, wifi")),
        ("asignados_a_mi", Flag("solo los asignados a quien pregunta")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var code = args.Str("codigo_activo");
        var mine = args.Bool("asignados_a_mi") == true;
        var assetType = args.Enum("tipo_activo", ctx.Catalog.Types.Select(t => t.Code));
        long? assetId = code is null ? null : ctx.AssetId(code);
        var status = args.Enum("estado", Statuses);
        var priority = args.Enum("prioridad", Priorities);
        var open = args.Bool("solo_abiertos");
        var since = args.Str("desde");
        var page = await ctx.Mediator.Send(new GetIncidentsQuery(1, ToolLimits.Rows, assetId, status, priority, null, mine ? ctx.UserId : null,
            open, args.Str("tema") ?? args.Str("texto"), ctx.Since(since), ctx.TypeId(assetType)), ct);

        var filters = SearchAssetsTool.Describe(("activo", code), ("tipo", assetType), ("abiertos", open == true ? "sí" : null),
            ("estado", status), ("prioridad", priority), ("desde", since), ("tema", args.Str("tema") ?? args.Str("texto")),
            ("asignados a", mine ? "mí" : null));
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
    private static readonly string[] Priorities =
        [IncidentPriorityCodes.Baja, IncidentPriorityCodes.Media, IncidentPriorityCodes.Alta, IncidentPriorityCodes.Critica];

    public string Name => "buscar_tickets";
    public string Summary => "Tickets: solicitudes, reportes o pedidos de soporte y su seguimiento (estado de la reparación, técnico asignado, prioridad, días abierto, asignados a mí, sin asignar).";
    public string Source => "GetTicketsQuery";
    public string? Permission => "tickets.create";
    public string Description => "Tickets de la mesa de ayuda (solicitudes de ayuda). Un usuario general solo ve los suyos. Filtra por estado, prioridad, tema, sin asignar, asignados a mí o a un técnico. Trae técnico asignado y días abierto. No hay fecha estimada de entrega ni fecha límite registradas.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("solo_abiertos", Flag("solo los no terminados")),
        ("estado", OneOf(TicketStatusCodes.All)),
        ("sin_asignar", Flag("solo los que nadie atiende")),
        ("prioridad", OneOf(Priorities)),
        ("asignados_a_mi", Flag("solo los asignados a quien pregunta (técnico: \"lo que tengo pendiente\")")),
        ("tecnico", Text("usuario o nombre del técnico asignado")),
        ("desde", Text("creados desde: hoy, ayer, semana, mes o AAAA-MM-DD")),
        ("tema", Text("tema o palabra clave del ticket, ej. wifi, impresora, proyector")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var status = args.Enum("estado", TicketStatusCodes.All);
        var open = args.Bool("solo_abiertos");
        var unassigned = args.Bool("sin_asignar");
        var since = args.Str("desde");
        var priority = args.Enum("prioridad", Priorities);
        var mine = args.Bool("asignados_a_mi") == true;
        // Sin tickets.manage la query solo devuelve los propios: el filtro por técnico no aplica.
        var tech = mine ? null : ctx.Can("tickets.manage") ? ctx.User(args.Str("tecnico")) : null;
        long? assignedTo = mine ? ctx.UserId : tech?.Id;
        var page = await ctx.Mediator.Send(new GetTicketsQuery(1, ToolLimits.Rows, status, null, priority, assignedTo,
            unassigned, open, args.Str("tema") ?? args.Str("texto"), ctx.Since(since)), ct);

        var filters = SearchAssetsTool.Describe(("tema", args.Str("tema") ?? args.Str("texto")), ("abiertos", open == true ? "sí" : null),
            ("estado", status), ("prioridad", priority), ("sin asignar", unassigned == true ? "sí" : null),
            ("asignados a", mine ? "mí" : tech?.Username), ("desde", since));
        return new ToolResult($"Tickets{filters} → {page.TotalCount}", new JsonObject
        {
            ["total"] = page.TotalCount,
            ["tickets"] = new JsonArray(page.Items.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.TicketId, ["titulo"] = t.Title, ["estado"] = t.Status, ["prioridad"] = t.PriorityCode,
                ["solicitante"] = t.RequesterUsername, ["asignado_a"] = t.AssignedToUsername,
                ["activo"] = t.AssetInventoryCode, ["creado"] = ctx.Local(t.CreatedAt), ["actualizado"] = ctx.Local(t.UpdatedAt),
                // Antigüedad real; el sistema no tiene fecha límite, así que no es "atraso".
                ["dias_abierto"] = TicketStatusCodes.Open.Contains(t.Status) ? (int)(ctx.NowUtc - t.CreatedAt).TotalDays : null,
            }).ToArray()),
        });
    }
}

public sealed class UpcomingMaintenanceTool : IAssistantTool
{
    public string Name => "mantenimientos_proximos";
    public string Summary => "Mantenimientos preventivos vencidos o por vencer (tienen fecha programada real).";
    public string Source => "GetUpcomingMaintenanceQuery";
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
    public string Summary => "Panorama general en números: activos por estado, tipo y ubicación, incidentes abiertos, mantenimientos y licencias.";
    public string Source => "GetDashboardSummaryQuery";
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
    public string Summary => "Los equipos asignados a quien pregunta (mi computadora, mi monitor), su estado y sus tickets con el técnico que los atiende.";
    public string Source => "GetMyAssetsQuery + GetTicketsQuery";
    public string? Permission => null;
    public string Description => "Los equipos asignados al usuario que pregunta (\"mi laptop\", \"mi equipo\", \"qué equipo tengo\"), con su estado y los tickets ligados a cada equipo con su técnico asignado. No hay fecha estimada de entrega registrada.";

    public JsonObject Parameters(AiCatalog catalog) => Object();

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var mine = await ctx.Mediator.Send(new GetMyAssetsQuery(), ct);
        // Técnico y última actualización de cada ticket ligado.
        // shortcut: lee hasta 100 tickets (sin manage son solo los suyos); si un usuario tiene más, filtrar por id en la query.
        var ids = mine.SelectMany(a => a.Tickets).Select(t => t.TicketId).ToHashSet();
        var detail = ids.Count == 0 ? new Dictionary<long, GetTicketsResponse>() : (await ctx.Mediator.Send(new GetTicketsQuery(1, 100, null, null, null, null, null, null, null), ct))
            .Items.Where(t => ids.Contains(t.TicketId)).ToDictionary(t => t.TicketId);
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
                    ["id"] = t.TicketId, ["titulo"] = t.Title, ["estado"] = t.Status, ["creado"] = ctx.Local(t.CreatedAt),
                    ["asignado_a"] = detail.GetValueOrDefault(t.TicketId)?.AssignedToUsername ?? "sin técnico asignado",
                    ["actualizado"] = detail.TryGetValue(t.TicketId, out var d) ? ctx.Local(d.UpdatedAt) : null,
                }).ToArray()),
            }).ToArray()),
        });
    }
}

/// <summary>
/// Borrador de ticket: no escribe nada. La pantalla lo muestra en una tarjeta y solo un clic del usuario lo
/// crea con POST /api/tickets (sus validaciones, transacción y auditoría). Una inyección no puede crear tickets.
/// </summary>
public sealed class ProposeTicketTool : IAssistantTool
{
    public string Name => "proponer_ticket";
    public string Summary => "Preparar un borrador de solicitud de ayuda cuando el usuario quiere reportar un problema o pedir soporte.";
    public string Source => "GetMyAssetsQuery (borrador; lo crea POST /api/tickets al confirmar)";
    public ToolKind Kind => ToolKind.Borrador;
    public string? Permission => "assistant.tickets.propose";
    public string Description => "Prepara un borrador de ticket de ayuda cuando el usuario pide abrir, levantar o reportar un problema (\"no prende mi laptop\", \"ábreme un ticket\"). No crea nada: el usuario lo revisa y confirma en una tarjeta. Si habla de su equipo y no sabes el código, usa antes mis_equipos. Para consultar tickets que ya existen usa buscar_tickets.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("categoria", OneOf((catalog.TicketCategories ?? []).Select(c => c.Code),
            "tipo de ayuda: " + string.Join(", ", (catalog.TicketCategories ?? []).Select(c => $"{c.Code} = {c.Name}")))),
        ("titulo", Text("qué pasa, en una frase corta")),
        ("descripcion", Text("solo detalles que el usuario escribió (desde cuándo, qué mensaje sale); no inventes ni preguntes; vacío si no dio")),
        ("equipo", Text("código de inventario del equipo, solo si se sabe; vacío si no"))).Required("categoria", "titulo");

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var categories = ctx.Catalog.TicketCategories ?? [];
        var code = args.Enum("categoria", categories.Select(c => c.Code))
            ?? throw new ToolArgumentException("Falta la categoría del ticket.");
        var category = categories.First(c => c.Code == code);
        var title = Clip(args.Str("titulo"), 200) ?? throw new ToolArgumentException("Falta el título del ticket.");
        var description = Clip(args.Str("descripcion"), 4000);

        long? assetId = null;
        var equipo = args.Str("equipo");
        if (equipo is not null)
        {
            if (ctx.Can("assets.view")) assetId = ctx.AssetId(equipo);
            else
            {
                // Sin inventario completo solo puede ligar sus equipos (POST /api/tickets lo vuelve a revisar).
                var mine = await ctx.Mediator.Send(new GetMyAssetsQuery(), ct);
                assetId = mine.FirstOrDefault(a => string.Equals(a.InventoryCode, equipo, StringComparison.OrdinalIgnoreCase))?.AssetId
                    ?? throw new ToolArgumentException($"«{equipo}» no es uno de los equipos asignados al usuario; consulta mis_equipos.");
            }
        }

        return new ToolResult($"Borrador de ticket «{title}» (aún sin crear)", new JsonObject
        {
            ["borrador"] = new JsonObject
            {
                ["categoria_id"] = category.Id, ["categoria"] = category.Name, ["titulo"] = title,
                ["descripcion"] = description, ["activo_id"] = assetId, ["equipo"] = assetId is null ? null : equipo,
            },
            ["aviso"] = "Aún no existe: se crea solo si el usuario pulsa «Crear ticket» en la tarjeta de abajo.",
        });
    }

    private static string? Clip(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}

public sealed class NewsTool : IAssistantTool
{
    public string Name => "novedades";
    public string Summary => "Qué hay de nuevo: incidentes y tickets creados desde una fecha.";
    public string Source => "GetIncidentsQuery + GetTicketsQuery";
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
    public string Summary => "Manual del sistema: cómo se hace algo, qué significa, reglas, estados, pantallas y permisos por rol.";
    public string Source => "ManualIndex (BM25 sobre Features/Assistant/Manual)";
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

public sealed class WorkloadTool : IAssistantTool
{
    public string Name => "carga_tecnicos";
    public string Summary => "Carga de trabajo por técnico: tickets e incidentes abiertos de cada uno, críticos, el más antiguo y los sin asignar.";
    public string Source => "GetTicketWorkloadQuery";
    public string? Permission => "tickets.manage";
    public string Description => "Carga de trabajo pendiente de cada persona con algo asignado: tickets abiertos, críticos, incidentes abiertos y días que lleva abierto su ticket más antiguo; más los tickets e incidentes sin asignar. El sistema no tiene fechas límite: la antigüedad no es atraso.";

    public JsonObject Parameters(AiCatalog catalog) => Object();

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var w = await ctx.Mediator.Send(new GetTicketWorkloadQuery(), ct);
        return new ToolResult($"Carga de trabajo → {w.Assignees.Count} personas con pendientes", new JsonObject
        {
            ["por_persona"] = new JsonArray(w.Assignees.Select(a => (JsonNode)new JsonObject
            {
                ["usuario"] = a.Username, ["nombre"] = a.FullName, ["tickets_abiertos"] = a.OpenTickets,
                ["tickets_criticos"] = a.CriticalOpenTickets, ["incidentes_abiertos"] = a.OpenIncidents,
                ["dias_ticket_mas_antiguo"] = a.OldestOpenTicketAt is null ? null : (int)(ctx.NowUtc - a.OldestOpenTicketAt.Value).TotalDays,
            }).ToArray()),
            ["tickets_sin_asignar"] = w.UnassignedOpenTickets,
            ["incidentes_sin_asignar"] = w.UnassignedOpenIncidents,
        });
    }
}

public sealed class TicketStatsTool : IAssistantTool
{
    public string Name => "estadisticas_tickets";
    public string Summary => "Estadísticas de solicitudes en un periodo: cuántas se recibieron, resolvieron, cerraron y cuántas siguen pendientes.";
    public string Source => "GetTicketStatsQuery";
    public string? Permission => "tickets.create";
    public string Description => "Movimiento de tickets en un periodo: recibidos (creados), resueltos, cerrados y cancelados en ese lapso, horas promedio de resolución y pendientes ahora, con los recibidos por prioridad y categoría. Un usuario general solo cuenta los suyos.";

    public JsonObject Parameters(AiCatalog catalog) => Object(
        ("desde", Text("inicio del periodo: hoy, ayer, semana, mes o AAAA-MM-DD (semana si no se indica)")),
        ("hasta", Text("último día del periodo (incluido): hoy, ayer o AAAA-MM-DD; ahora si no se indica")));

    public async Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext ctx, CancellationToken ct)
    {
        var since = args.Str("desde") ?? "semana";
        var until = args.Str("hasta");
        var from = ctx.Since(since)!.Value;
        var to = until is null ? ctx.NowUtc : ctx.Since(until)!.Value.AddDays(1); // "hasta" incluye ese día
        var s = await ctx.Mediator.Send(new GetTicketStatsQuery(from, to), ct);
        static JsonObject Counts(IEnumerable<CodeCount> items)
        {
            var o = new JsonObject();
            foreach (var i in items) o[i.Code] = i.Count;
            return o;
        }
        return new ToolResult($"Tickets desde {since}{(until is null ? "" : $" hasta {until}")} → {s.Created} recibidos, {s.Resolved} resueltos", new JsonObject
        {
            ["desde"] = ctx.Local(s.From), ["hasta"] = ctx.Local(s.To),
            ["recibidos"] = s.Created, ["resueltos"] = s.Resolved, ["cerrados"] = s.Closed, ["cancelados"] = s.Cancelled,
            ["recibidos_que_siguen_abiertos"] = s.CreatedStillOpen, ["horas_promedio_resolucion"] = s.AvgResolutionHours,
            ["pendientes_ahora"] = s.OpenNow,
            ["recibidos_por_prioridad"] = Counts(s.CreatedByPriority), ["recibidos_por_categoria"] = Counts(s.CreatedByCategory),
        });
    }
}
