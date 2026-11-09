using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Auth.Shared;

/// <summary>
/// Borra las familias de refresh tokens muertas: todas sus filas vencieron hace
/// más de <see cref="RetentionDays"/> días. Se borra la familia completa en una
/// sola sentencia porque replaced_by_id apunta dentro de la misma familia, y
/// mientras una familia siga viva se conservan sus tokens revocados para que la
/// detección de reutilización siga funcionando.
/// </summary>
public static class RefreshTokenCleanup
{
    public const int RetentionDays = 30;

    public static Task<int> DeleteDeadFamiliesAsync(TecnmInventarioDbContext context, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-RetentionDays);
        return context.RefreshTokens
            .Where(t => !context.RefreshTokens.Any(o => o.FamilyId == t.FamilyId && o.ExpiresAt >= cutoff))
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Limpieza diaria de refresh tokens (BackgroundService, sin librerías).</summary>
public sealed class RefreshTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
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
                var deleted = await RefreshTokenCleanup.DeleteDeadFamiliesAsync(context, DateTime.UtcNow, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Limpieza de sesiones: {Count} refresh token(s) borrados.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un fallo (p. ej. BD caída) no debe tumbar la API: se reintenta mañana.
                _logger.LogError(ex, "Falló la limpieza de refresh tokens.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
