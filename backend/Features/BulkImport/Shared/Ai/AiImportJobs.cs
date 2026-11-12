using System.Collections.Concurrent;
using System.Threading.Channels;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Pdf;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

public static class AiJobStatus
{
    public const string Queued = "EN_COLA";
    public const string Running = "PROCESANDO";
    public const string Done = "LISTO";
    public const string Failed = "ERROR";
}

/// <summary>Foto del trabajo para responder al cliente (sin el archivo ni el resultado).</summary>
public sealed record AiJobSnapshot(
    Guid Id, string FileName, string Status, string Step, int Done, int Total, int Found, int ToReview, string? Error);

public sealed class AiImportJob(long userId, ImportFile file, DateTime createdAt)
{
    private readonly object _lock = new();
    private string _status = AiJobStatus.Queued, _step = "En espera de turno";
    private int _done, _total, _found, _toReview;
    private string? _error;

    public Guid Id { get; } = Guid.NewGuid();
    public long UserId { get; } = userId;
    public string FileName { get; } = file.FileName;
    public DateTime CreatedAt { get; } = createdAt;
    /// <summary>Se suelta al terminar: no guardar 5 MB por trabajo durante una hora.</summary>
    public ImportFile? File { get; private set; } = file;
    public GeneratedFile? Result { get; private set; }

    public bool IsActive { get { lock (_lock) return _status is AiJobStatus.Queued or AiJobStatus.Running; } }

    public void Progress(string step, int done, int total, int found)
    {
        lock (_lock) { _status = AiJobStatus.Running; _step = step; _done = done; _total = total; _found = found; }
    }

    public void Complete(GeneratedFile result, int found, int toReview)
    {
        lock (_lock)
        {
            _status = AiJobStatus.Done; _step = "Listo"; _done = _total; _found = found; _toReview = toReview;
            Result = result; File = null;
        }
    }

    public void Fail(string error)
    {
        lock (_lock) { _status = AiJobStatus.Failed; _step = "Error"; _error = error; File = null; }
    }

    public AiJobSnapshot Snapshot()
    {
        lock (_lock) return new(Id, FileName, _status, _step, _done, _total, _found, _toReview, _error);
    }
}

/// <summary>
/// Trabajos de conversión con IA. El modelo atiende un turno: un trabajador los procesa en orden.
/// shortcut: viven en memoria; si la API se reinicia se pierden y el usuario vuelve a subir el archivo.
/// Guardarlos en la BD si eso molesta.
/// </summary>
public sealed class AiImportJobs
{
    public const int MaxQueued = 5;
    public static readonly TimeSpan Keep = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, AiImportJob> _jobs = new();
    private readonly Channel<AiImportJob> _queue = Channel.CreateUnbounded<AiImportJob>();
    private readonly TimeProvider _clock;
    private readonly object _enqueueLock = new();

    public AiImportJobs(TimeProvider clock)
    {
        _clock = clock;
    }

    public ChannelReader<AiImportJob> Reader => _queue.Reader;

    public AiImportJob Enqueue(long userId, ImportFile file)
    {
        lock (_enqueueLock)
        {
            Purge();
            if (_jobs.Values.Any(j => j.UserId == userId && j.IsActive))
                throw new BusinessRuleViolationException("Ya tienes un archivo en conversión. Espera a que termine.");
            if (_jobs.Values.Count(j => j.IsActive) >= MaxQueued)
                throw new BusinessRuleViolationException("Hay demasiados archivos en conversión. Intenta en unos minutos.");

            var job = new AiImportJob(userId, file, _clock.GetUtcNow().UtcDateTime);
            _jobs[job.Id] = job;
            _queue.Writer.TryWrite(job);
            return job;
        }
    }

    /// <summary>Solo el dueño lo ve; para cualquier otro "no existe".</summary>
    public AiImportJob Get(Guid id, long userId) =>
        _jobs.TryGetValue(id, out var job) && job.UserId == userId
            ? job
            : throw new KeyNotFoundException("La conversión no existe o ya expiró.");

    private void Purge()
    {
        var limit = _clock.GetUtcNow().UtcDateTime - Keep;
        foreach (var job in _jobs.Values.Where(j => !j.IsActive && j.CreatedAt < limit))
            _jobs.TryRemove(job.Id, out _);
    }
}
