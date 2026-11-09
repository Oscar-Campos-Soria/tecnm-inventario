using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.History.GetAuditLog;

/// <summary>Consulta de la bitácora (solo lectura; audit_log es inmutable).</summary>
public sealed class GetAuditLogHandler : IQueryHandler<GetAuditLogQuery, PagedResult<AuditLogItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAuditLogHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<AuditLogItem>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.TableName))
        {
            var table = query.TableName.Trim();
            logs = logs.Where(l => l.TableName == table);
        }
        if (query.RecordId.HasValue)
        {
            var recordId = query.RecordId.Value;
            logs = logs.Where(l => l.RecordId == recordId);
        }
        if (query.UserId.HasValue)
        {
            var userId = query.UserId.Value;
            logs = logs.Where(l => l.PerformedBy == userId);
        }
        if (query.From.HasValue)
        {
            var from = query.From.Value.ToUniversalTime();
            logs = logs.Where(l => l.PerformedAt >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.ToUniversalTime();
            logs = logs.Where(l => l.PerformedAt <= to);
        }

        var total = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(l => l.AuditLogId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new AuditLogItem(
                l.AuditLogId, l.TableName, l.RecordId, l.Action, l.PerformedBy,
                l.PerformedByNavigation != null ? l.PerformedByNavigation.Username : null,
                l.PerformedAt, l.OldData, l.NewData))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogItem>(items, query.PageNumber, query.PageSize, total);
    }
}
