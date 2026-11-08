using Microsoft.Extensions.Options;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Tickets.Shared;

/// <summary>
/// Tarea en segundo plano (BackgroundService de .NET, sin librerías extra):
/// al arrancar y luego cada hora cierra los tickets resueltos sin respuesta.
/// </summary>
public sealed class TicketAutoCloseService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TicketOptions> _options;
    private readonly ILogger<TicketAutoCloseService> _logger;

    public TicketAutoCloseService(
        IServiceScopeFactory scopeFactory,
        IOptions<TicketOptions> options,
        ILogger<TicketAutoCloseService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TecnmInventarioDbContext>();
                var closed = await TicketAutoCloser.CloseExpiredAsync(
                    context, _options.Value.AutoCloseDays, DateTime.UtcNow, _logger, stoppingToken);
                if (closed > 0)
                    _logger.LogInformation("Cierre automático: {Count} ticket(s) cerrados.", closed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un fallo (p. ej. BD caída) no debe tumbar la API: se reintenta en la siguiente pasada.
                _logger.LogError(ex, "Falló el cierre automático de tickets.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
