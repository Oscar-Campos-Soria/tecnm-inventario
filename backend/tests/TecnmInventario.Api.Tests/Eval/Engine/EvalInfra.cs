using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TecnmInventario.Api.Tests.Eval.Engine;

/// <summary>Una llamada al modelo tal como la vio el proxy: tokens y tiempos que reporta llama.cpp.</summary>
public sealed record ModelCall(DateTime At, long Ms, string Kind, int Tools, int Messages, int RequestBytes,
    int PromptTokens, int CompletionTokens, int CachedTokens, double PromptMs, double PredictedMs);

/// <summary>
/// Proxy HTTP entre la API y llama.cpp: no cambia nada del asistente (la API solo apunta Ai:BaseUrl aquí) y
/// registra cada /v1/chat/completions con usage y timings. Las corridas son secuenciales: lo registrado entre dos
/// marcas es de un caso.
/// </summary>
public sealed class LlamaProxy : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly HttpClient _target;
    private readonly ConcurrentQueue<ModelCall> _calls = new();
    private readonly CancellationTokenSource _stop = new();
    // Volcado opcional de cada request (para repetir la carga real en pruebas de memoria): TECNM_EVAL_DUMP=<carpeta>.
    private readonly string? _dump = Environment.GetEnvironmentVariable("TECNM_EVAL_DUMP");
    private int _seq;

    public LlamaProxy(string listenPrefix, string targetBaseUrl)
    {
        if (_dump is not null) Directory.CreateDirectory(_dump);
        _target = new HttpClient { BaseAddress = new Uri(targetBaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(6) };
        _listener.Prefixes.Add(listenPrefix);
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public List<ModelCall> Drain()
    {
        var list = new List<ModelCall>();
        while (_calls.TryDequeue(out var c)) list.Add(c);
        return list;
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var path = ctx.Request.Url!.PathAndQuery.TrimStart('/');
            using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.HttpMethod), path);
            if (body.Length > 0) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            var watch = Stopwatch.StartNew();
            using var response = await _target.SendAsync(request);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            watch.Stop();
            if (path.Contains("chat/completions") && response.IsSuccessStatusCode) Record(body, bytes, watch.ElapsedMilliseconds);
            ctx.Response.StatusCode = (int)response.StatusCode;
            ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
        catch
        {
            ctx.Response.StatusCode = 502;
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private void Record(string requestBody, byte[] responseBytes, long ms)
    {
        if (_dump is not null)
            File.WriteAllText(Path.Combine(_dump, $"{Interlocked.Increment(ref _seq):D4}.json"), requestBody);
        var req = JsonNode.Parse(requestBody)!;
        var res = JsonNode.Parse(responseBytes)!;
        var kind = req["tools"] is JsonArray { Count: > 0 } ? "tools" : req["response_format"] is not null ? "json" : "chat";
        int I(JsonNode? n) => n is null ? 0 : (int)n.GetValue<double>();
        double D(JsonNode? n) => n is null ? 0 : n.GetValue<double>();
        _calls.Enqueue(new ModelCall(DateTime.UtcNow, ms, kind, (req["tools"] as JsonArray)?.Count ?? 0,
            (req["messages"] as JsonArray)?.Count ?? 0, Encoding.UTF8.GetByteCount(requestBody),
            I(res["usage"]?["prompt_tokens"]), I(res["usage"]?["completion_tokens"]), I(res["timings"]?["cache_n"]),
            D(res["timings"]?["prompt_ms"]), D(res["timings"]?["predicted_ms"])));
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        _target.Dispose();
    }
}

/// <summary>Muestra de recursos del contenedor del modelo y de la tarjeta (si hay nvidia-smi).</summary>
public sealed record ResourceSample(DateTime At, double CpuPercent, double MemMiB, double? GpuPercent, double? GpuMemMiB);

/// <summary>docker stats + nvidia-smi en bucle mientras corre la evaluación.</summary>
public sealed partial class ResourceSampler : IDisposable
{
    private readonly ConcurrentQueue<ResourceSample> _samples = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly string _container;
    private bool _gpu = true;

    [GeneratedRegex(@"^([\d.]+)([KMG]i?B)", RegexOptions.IgnoreCase)]
    private static partial Regex Size();

    public ResourceSampler(string container)
    {
        _container = container;
        _ = Task.Run(LoopAsync);
    }

    public List<ResourceSample> Between(DateTime from, DateTime to) => _samples.Where(s => s.At >= from && s.At <= to).ToList();

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var stats = (await RunAsync("docker", $"stats {_container} --no-stream --format \"{{{{.CPUPerc}}}}|{{{{.MemUsage}}}}\"")).Trim();
                var parts = stats.Split('|');
                double? gpu = null, gpuMem = null;
                if (_gpu)
                {
                    var smi = await RunAsync("nvidia-smi", "--query-gpu=utilization.gpu,memory.used --format=csv,noheader,nounits");
                    var g = smi.Split(',', StringSplitOptions.TrimEntries);
                    if (g.Length == 2 && double.TryParse(g[0], CultureInfo.InvariantCulture, out var gu) && double.TryParse(g[1], CultureInfo.InvariantCulture, out var gm))
                        (gpu, gpuMem) = (gu, gm);
                    else _gpu = false;
                }
                if (parts.Length == 2 && double.TryParse(parts[0].TrimEnd('%'), CultureInfo.InvariantCulture, out var cpu))
                    _samples.Enqueue(new ResourceSample(DateTime.UtcNow, cpu, MiB(parts[1].Split('/')[0].Trim()), gpu, gpuMem));
            }
            catch
            {
                // Sin docker o sin nvidia-smi: la evaluación sigue sin esas métricas.
            }
            await Task.Delay(500);
        }
    }

    private static double MiB(string text)
    {
        var m = Size().Match(text);
        if (!m.Success) return 0;
        var value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Value.ToUpperInvariant()[0] switch { 'G' => value * 1024, 'K' => value / 1024, _ => value };
    }

    private static async Task<string> RunAsync(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output;
    }

    public void Dispose() => _stop.Cancel();
}
