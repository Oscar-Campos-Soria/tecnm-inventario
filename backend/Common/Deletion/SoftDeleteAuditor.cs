using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Contracts;

namespace TecnmInventario.Api.Common.Deletion;

public sealed class SoftDeleteAuditor : ISoftDeleteAuditor
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentUserService _currentUser;

    public SoftDeleteAuditor(
        TecnmInventarioDbContext context,
        IAuditLogService auditLog,
        ICurrentUserService currentUser)
    {
        _context = context;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public async Task<TEntity> DeleteAsync<TEntity>(
        TEntity entity,
        long recordId,
        int expectedVersion,
        string tableName,
        object snapshotBeforeDelete,
        CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletable
    {
        // Concurrencia optimista real: comparamos contra la versión que el
        // CLIENTE vio por última vez, no contra la que se acaba de leer.
        // Si alguien más modificó/borró la fila mientras tanto, el UPDATE
        // con WHERE version=ExpectedVersion afecta 0 filas y EF Core lanza
        // DbUpdateConcurrencyException (-> 409 en el middleware).
        _context.Entry(entity).Property(nameof(ISoftDeletable.Version)).OriginalValue = expectedVersion;

        entity.IsDeleted = true;
        entity.DeletedBy = _currentUser.UserId;
        // DeletedAt, UpdatedAt y Version los actualizan los triggers de
        // Postgres (prevent_invalid_soft_delete y touch_version).

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Releemos para capturar los valores reales que pusieron los
        // triggers (deleted_at, version incrementada). No se ve afectado
        // por HasQueryFilter: verificado con prueba real en Documents.
        await _context.Entry(entity).ReloadAsync(cancellationToken);

        var afterSnapshot = new
        {
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedBy,
            entity.Version
        };

        await _auditLog.LogAsync(
            tableName: tableName,
            recordId: recordId,
            action: "DELETE",
            oldData: snapshotBeforeDelete,
            newData: afterSnapshot,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return entity;
    }
}
