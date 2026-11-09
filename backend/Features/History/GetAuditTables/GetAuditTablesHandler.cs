using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.History.GetAuditTables;

public sealed class GetAuditTablesHandler : IQueryHandler<GetAuditTablesQuery, IReadOnlyList<string>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAuditTablesHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<string>> Handle(GetAuditTablesQuery query, CancellationToken cancellationToken)
        => await _context.AuditLogs.AsNoTracking()
            .Select(l => l.TableName).Distinct().OrderBy(t => t)
            .ToListAsync(cancellationToken);
}
