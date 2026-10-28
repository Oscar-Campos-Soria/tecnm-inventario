using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests;

internal static class TestDb
{
    public static TecnmInventarioDbContext Create(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-lote1-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));

        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new TecnmInventarioDbContext(builder.Options);
    }
}

internal sealed class SaveChangesCounter : SaveChangesInterceptor
{
    public int Count { get; private set; }

    public void Reset() => Count = 0;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public long? UserId => 1;
    public string? Username => "admin";
    public string? RoleCode => "ADMIN";
    public System.Net.IPAddress? IpAddress => null;
}

internal sealed class FakeTransactionScope : ITransactionScope
{
    private readonly bool _commitFails;

    public FakeTransactionScope(bool commitFails = false)
    {
        _commitFails = commitFails;
    }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCount++;
        return _commitFails
            ? Task.FromException(new InvalidOperationException("El commit falló (simulado)."))
            : Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeTransactionScopeFactory : ITransactionScopeFactory
{
    public bool HasActiveTransaction { get; set; }

    public bool CommitFails { get; set; }

    public int BeginCount { get; private set; }

    public FakeTransactionScope? Scope { get; private set; }

    public Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        BeginCount++;
        Scope = new FakeTransactionScope(CommitFails);
        return Task.FromResult<ITransactionScope>(Scope);
    }
}

internal sealed record AuditEntry(
    string TableName,
    long RecordId,
    string Action,
    object? OldData,
    object? NewData);

internal sealed class RecordingAuditLogService : IAuditLogService
{
    public bool FailOnLog { get; set; }

    public List<AuditEntry> Entries { get; } = new();

    public Task LogAsync(
        string tableName,
        long recordId,
        string action,
        object? oldData,
        object? newData,
        CancellationToken cancellationToken)
    {
        if (FailOnLog)
        {
            throw new InvalidOperationException("audit_log falló (simulado).");
        }

        Entries.Add(new AuditEntry(tableName, recordId, action, oldData, newData));
        return Task.CompletedTask;
    }
}
