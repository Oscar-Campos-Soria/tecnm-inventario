using System.Globalization;
using System.Text.Json.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assistant.Shared;

/// <summary>
/// Herramienta de solo lectura que el modelo puede pedir. Ejecuta una query existente de Mediator
/// (nunca un comando ni SQL del modelo). Permission null = cualquier usuario del asistente.
/// </summary>
public interface IAssistantTool
{
    string Name { get; }
    string? Permission { get; }
    string Description { get; }
    JsonObject Parameters(AiCatalog catalog);
    Task<ToolResult> ExecuteAsync(ToolArgs args, ToolContext context, CancellationToken ct);
}

/// <summary>Summary: qué se consultó, en palabras (lo ve el usuario). Data: resultado compacto (lo ve el modelo y la pantalla).</summary>
public sealed record ToolResult(string Summary, JsonNode Data);

/// <summary>Argumento inválido: se le explica al modelo para que corrija, no se ejecuta nada.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>Lo que necesitan las herramientas: Mediator, catálogos y traducción de nombres a ids.</summary>
public sealed class ToolContext(IMediator mediator, TecnmInventarioDbContext db, AiCatalog catalog, DateTime nowUtc,
    IReadOnlySet<string>? permissions = null, string userText = "", string question = "")
{
    // Palabras con que la gente nombra cada tipo (además del código y el nombre del catálogo).
    private static readonly Dictionary<string, string[]> TypeWords = new()
    {
        ["PC"] = ["pc", "pcs", "cpu", "cpus", "computadora", "computadoras", "escritorio"], ["LAPTOP"] = ["laptop", "laptops", "portatil", "portatiles"],
        ["PRINTER"] = ["impresora", "impresoras"], ["PROJECTOR"] = ["proyector", "proyectores"], ["MONITOR"] = ["monitor", "monitores", "pantalla", "pantallas"],
        ["SWITCH"] = ["switch", "switches"], ["ROUTER"] = ["router", "routers", "ruteador"], ["SERVER"] = ["servidor", "servidores"],
        ["UPS"] = ["ups", "nobreak", "break"], ["ACCESS_POINT"] = ["acceso", "ap", "aps"], ["CAMERA"] = ["camara", "camaras"], ["RACK"] = ["rack", "racks"],
    };

    /// <summary>Lo que el usuario escribió en esta conversación (pregunta actual y anteriores).</summary>
    public string UserText { get; } = userText;

    /// <summary>¿El usuario mencionó este tipo de equipo? El modelo a veces agrega tipos que nadie pidió.</summary>
    public bool Mentioned(string typeCode)
    {
        var words = AiText.WordsOf(UserText).ToHashSet();
        var name = Catalog.Types.FirstOrDefault(t => t.Code == typeCode).Name ?? "";
        return words.Contains(typeCode.ToLowerInvariant().Replace("_", ""))
            || TypeWords.GetValueOrDefault(typeCode, []).Any(words.Contains)
            || AiText.Tokens(name).Any(t => t.Length > 3 && (words.Contains(t) || words.Contains(t + "s") || words.Contains(t + "es")));
    }

    /// <summary>Ubicación que la pregunta actual menciona con claridad (para no perder "en el laboratorio de redes").</summary>
    public string? MentionedLocation()
    {
        var (code, strong) = Catalog.MatchLocation(question);
        return strong ? code : null;
    }

    // shortcut: México centro es UTC-6 fijo desde 2022 (sin horario de verano); si se usa en otra zona, configurarla.
    public static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-6);

    public IMediator Mediator { get; } = mediator;
    public AiCatalog Catalog { get; } = catalog;
    public DateTime NowUtc { get; } = nowUtc;
    public DateOnly Today => DateOnly.FromDateTime(NowUtc + LocalOffset);

    /// <summary>¿El rol tiene este permiso? Para herramientas que combinan áreas (ej. novedades).</summary>
    public bool Can(string permission) => permissions?.Contains(permission) ?? false;

    public long? TypeId(string? code) => code is null ? null
        : db.AssetTypes.AsNoTracking().Where(t => t.Code == code && !t.IsDeleted).Select(t => (long?)t.AssetTypeId).FirstOrDefault()
          ?? throw new ToolArgumentException($"El tipo «{code}» no existe.");

    /// <summary>"laboratorio de redes" -> ubicación del catálogo (incluye sub-ubicaciones al buscar).</summary>
    public (long Id, string Name)? Location(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var byCode = Catalog.Locations.FirstOrDefault(l => string.Equals(l.Code, text.Trim(), StringComparison.OrdinalIgnoreCase));
        var code = byCode.Code ?? Catalog.MatchLocation(text).Code;
        if (string.IsNullOrEmpty(code)) throw new ToolArgumentException($"No encontré la ubicación «{text}».");
        var path = Catalog.Locations.First(l => l.Code == code).Path;
        return (db.Locations.AsNoTracking().Where(l => l.Code == code).Select(l => l.LocationId).First(), path);
    }

    public (long Id, string Name)? Manufacturer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var wanted = AiText.Norm(name);
        var match = db.Manufacturers.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new { m.ManufacturerId, m.Name }).AsEnumerable()
            .FirstOrDefault(m => AiText.Norm(m.Name) == wanted);
        return match is null
            ? throw new ToolArgumentException($"El fabricante «{name}» no está en el catálogo.")
            : (match.ManufacturerId, match.Name);
    }

    /// <summary>Código de inventario exacto (sin importar mayúsculas) -> id.</summary>
    public long AssetId(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ToolArgumentException("Falta el código de inventario.");
        var wanted = code.Trim().ToLower();
        return db.Assets.AsNoTracking().Where(a => !a.IsDeleted && a.InventoryCode.ToLower() == wanted)
            .Select(a => (long?)a.AssetId).FirstOrDefault()
            ?? throw new ToolArgumentException($"No existe un activo con código «{code}».");
    }

    /// <summary>"hoy", "ayer", "semana", "mes" o fecha AAAA-MM-DD (hora local) -> inicio de ese día en UTC.</summary>
    public DateTime? Since(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var day = AiText.Plain(text.Trim()) switch
        {
            "hoy" => Today,
            "ayer" => Today.AddDays(-1),
            "semana" or "esta semana" or "ultima semana" => Today.AddDays(-7),
            "mes" or "este mes" or "ultimo mes" => Today.AddDays(-30),
            var t when DateOnly.TryParseExact(t[..Math.Min(10, t.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) => d,
            _ => throw new ToolArgumentException($"Fecha «{text}» no válida: usa hoy, ayer, semana, mes o AAAA-MM-DD."),
        };
        return DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - LocalOffset, DateTimeKind.Utc);
    }

    public string Local(DateTime utc) => (utc + LocalOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>Lectura tolerante de los argumentos que escribió el modelo.</summary>
public sealed class ToolArgs(JsonObject json)
{
    private static readonly HashSet<string> Wildcards = new(StringComparer.OrdinalIgnoreCase) { "*", "todos", "todas", "cualquiera", "all", "null", "ninguno" };

    public string? Str(string key)
    {
        var node = json[key];
        if (node is null) return null;
        var text = node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : node.ToJsonString();
        // El modelo a veces manda comodines en vez de omitir el filtro.
        return string.IsNullOrWhiteSpace(text) || Wildcards.Contains(text.Trim()) ? null : text.Trim();
    }

    public bool? Bool(string key) => json[key]?.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        System.Text.Json.JsonValueKind.String => bool.TryParse(json[key]!.GetValue<string>(), out var b) ? b : null,
        _ => null,
    };

    public int? Int(string key) => json[key]?.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.Number => json[key]!.GetValue<int>(),
        System.Text.Json.JsonValueKind.String => int.TryParse(json[key]!.GetValue<string>(), out var i) ? i : null,
        _ => null,
    };

    /// <summary>Valor de una lista cerrada; acepta mayúsculas/minúsculas y espacios en vez de guion bajo.</summary>
    public string? Enum(string key, IEnumerable<string> allowed)
    {
        var value = Str(key);
        if (value is null) return null;
        var wanted = value.ToUpperInvariant().Replace(' ', '_');
        return allowed.FirstOrDefault(a => a == wanted)
            ?? throw new ToolArgumentException($"«{value}» no es válido para {key}; opciones: {string.Join(", ", allowed)}.");
    }
}

/// <summary>Ayudas para declarar parámetros (JSON Schema) sin repetir.</summary>
public static class ToolSchema
{
    public static JsonObject Object(params (string Name, JsonObject Schema)[] properties)
    {
        var props = new JsonObject();
        foreach (var (name, schema) in properties) props[name] = schema;
        return new JsonObject { ["type"] = "object", ["properties"] = props, ["required"] = new JsonArray() };
    }

    public static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };
    public static JsonObject Flag(string description) => new() { ["type"] = "boolean", ["description"] = description };
    public static JsonObject Number(string description) => new() { ["type"] = "integer", ["description"] = description };

    public static JsonObject OneOf(IEnumerable<string> values, string? description = null)
    {
        var schema = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()) };
        if (description is not null) schema["description"] = description;
        return schema;
    }

    /// <summary>Tipo de equipo con su nombre: el modelo entiende "pcs" o "computadoras" si ve "PC = Computadora de Escritorio".</summary>
    public static JsonObject AssetType(AiCatalog catalog, string what = "tipo de equipo") =>
        OneOf(catalog.Types.Select(t => t.Code), $"{what}: " + string.Join(", ", catalog.Types.Select(t => $"{t.Code} = {t.Name}")));

    public static JsonObject Required(this JsonObject schema, params string[] names)
    {
        schema["required"] = new JsonArray(names.Select(n => (JsonNode)n).ToArray());
        return schema;
    }
}
