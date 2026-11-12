using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ia";

    /// <summary>llama-server (servicio "ia"). Vacío = asistente apagado.</summary>
    public string BaseUrl { get; set; } = "";
}

/// <summary>Una herramienta que el modelo pidió ejecutar. Arguments es el JSON tal cual lo escribió el modelo.</summary>
public sealed record AiToolCall(string Id, string Name, string Arguments);

/// <summary>
/// Respuesta de un turno de conversación: texto final o herramientas por ejecutar.
/// Message es el mensaje del asistente tal cual, para agregarlo al historial antes de los resultados.
/// </summary>
public sealed record AiChatReply(string? Content, IReadOnlyList<AiToolCall> ToolCalls, JsonObject Message);

/// <summary>Modelo local (llama.cpp, API compatible con OpenAI). Sin SDK: HttpClient.</summary>
public interface ILocalAiClient
{
    /// <summary>¿Configurado y respondiendo? (cacheado 30 s).</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);

    /// <summary>Respuesta JSON forzada por <paramref name="schema"/> (gramática de llama.cpp).</summary>
    Task<JsonNode> CompleteJsonAsync(string system, string user, JsonObject schema, CancellationToken ct);

    /// <summary>Texto de una imagen (oficio escaneado, foto).</summary>
    Task<string> TranscribeAsync(byte[] image, string mimeType, CancellationToken ct);

    /// <summary>
    /// Un turno de conversación con herramientas (tool calling nativo de llama.cpp con --jinja).
    /// <paramref name="messages"/> en formato OpenAI (system, user, assistant, tool); no se modifica.
    /// </summary>
    Task<AiChatReply> ChatAsync(JsonArray messages, JsonArray? tools, CancellationToken ct);
}

public sealed class LocalAiClient : ILocalAiClient
{
    private const string HealthCacheKey = "ia:health";
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly bool _configured;

    public LocalAiClient(HttpClient http, IOptions<AiOptions> options, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
        _configured = Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var baseUri);
        if (_configured) _http.BaseAddress = baseUri;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (!_configured) return false;
        if (_cache.TryGetValue(HealthCacheKey, out bool cached)) return cached;
        bool healthy;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            healthy = (await _http.GetAsync("health", timeout.Token)).IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            healthy = false;
        }
        _cache.Set(HealthCacheKey, healthy, TimeSpan.FromSeconds(30));
        return healthy;
    }

    public async Task<JsonNode> CompleteJsonAsync(string system, string user, JsonObject schema, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                // Copia: un JsonNode solo puede tener un padre y el esquema se reusa en cada bloque.
                ["json_schema"] = new JsonObject { ["schema"] = schema.DeepClone() },
            },
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["temperature"] = 0,
        };
        var content = (await SendAsync(body, ct))["content"]?.GetValue<string>()
            ?? throw new AiUnavailableException("Respuesta inesperada del asistente IA.");
        try
        {
            return JsonNode.Parse(content) ?? throw new AiUnavailableException("La IA devolvió una respuesta vacía.");
        }
        catch (JsonException)
        {
            // Con gramática esto solo pasa si se cortó por longitud (bloque demasiado grande).
            throw new AiUnavailableException("La IA devolvió una respuesta incompleta.");
        }
    }

    public async Task<string> TranscribeAsync(byte[] image, string mimeType, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["messages"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JsonObject { ["url"] = $"data:{mimeType};base64,{Convert.ToBase64String(image)}" },
                    },
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = "Transcribe exactamente todo el texto de este documento, línea por línea, sin agregar nada.",
                    }),
            }),
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["temperature"] = 0,
        };
        return (await SendAsync(body, ct))["content"]?.GetValue<string>()
            ?? throw new AiUnavailableException("Respuesta inesperada del asistente IA.");
    }

    public async Task<AiChatReply> ChatAsync(JsonArray messages, JsonArray? tools, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["messages"] = messages.DeepClone(),
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["temperature"] = 0,
        };
        if (tools is { Count: > 0 }) body["tools"] = tools.DeepClone();

        var message = await SendAsync(body, ct);
        var calls = (message["tool_calls"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(c => new AiToolCall(
                c["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                c["function"]?["name"]?.GetValue<string>() ?? "",
                c["function"]?["arguments"]?.GetValue<string>() ?? "{}"))
            .ToList();
        var content = message["content"]?.GetValue<string>();
        return new AiChatReply(string.IsNullOrWhiteSpace(content) ? null : content, calls, message);
    }

    /// <summary>POST a chat/completions; devuelve el mensaje del asistente (choices[0].message).</summary>
    private async Task<JsonObject> SendAsync(JsonObject body, CancellationToken ct)
    {
        if (!_configured) throw new AiUnavailableException("El asistente IA no está configurado.");
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("v1/chat/completions", body, ct);
        }
        catch (HttpRequestException)
        {
            _cache.Remove(HealthCacheKey);
            throw new AiUnavailableException("El asistente IA no responde.");
        }
        if (!response.IsSuccessStatusCode)
            throw new AiUnavailableException($"El asistente IA respondió {(int)response.StatusCode}.");

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        return json?["choices"]?[0]?["message"] as JsonObject
            ?? throw new AiUnavailableException("Respuesta inesperada del asistente IA.");
    }
}

public sealed class AiUnavailableException(string message) : Exception(message);
