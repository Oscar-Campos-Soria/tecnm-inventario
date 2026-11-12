using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;

namespace TecnmInventario.Api.Features.BulkImport.Shared.Ai;

/// <summary>Procesa los trabajos de conversión uno por uno (el modelo atiende un turno a la vez).</summary>
public sealed class AiImportWorker : BackgroundService
{
    private readonly AiImportJobs _jobs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiImportWorker> _logger;

    public AiImportWorker(AiImportJobs jobs, IServiceScopeFactory scopeFactory, ILogger<AiImportWorker> logger)
    {
        _jobs = jobs;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AiImportProcessor>().ProcessAsync(job, stoppingToken);
                _logger.LogInformation("Conversión IA {JobId} lista: {Found} equipos.", job.Id, job.Snapshot().Found);
            }
            catch (Exception ex) when (ex is BusinessRuleViolationException or AiUnavailableException)
            {
                job.Fail(ex.Message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                job.Fail("La API se detuvo durante la conversión. Vuelve a subir el archivo.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la conversión IA {JobId}.", job.Id);
                job.Fail("Ocurrió un error inesperado al convertir el archivo.");
            }
        }
    }
}
