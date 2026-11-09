using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.History.GetAuditLog;

public sealed record GetAuditLogQuery(
    int PageNumber,
    int PageSize,
    string? TableName,
    long? RecordId,
    long? UserId,
    DateTime? From,
    DateTime? To) : IQuery<PagedResult<AuditLogItem>>;
