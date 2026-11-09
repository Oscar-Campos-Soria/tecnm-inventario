using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.History.GetAssetTimeline;
using TecnmInventario.Api.Features.History.GetAuditLog;
using TecnmInventario.Api.Features.History.GetAuditTables;

namespace TecnmInventario.Api.Features.History;

/// <summary>Historial de auditoría (solo lectura; permiso history.view, solo ADMIN).</summary>
[ApiController]
[Route("api/history")]
[Authorize(Policy = "history.view")]
public class HistoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public HistoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogItem>>> GetAuditLog(
        [FromQuery] PagedRequest pagination,
        [FromQuery] string? table,
        [FromQuery] long? recordId,
        [FromQuery] long? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetAuditLogQuery(pagination.PageNumber, pagination.PageSize, table, recordId, userId, from, to), cancellationToken));

    [HttpGet("tables")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetTables(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAuditTablesQuery(), cancellationToken));

    [HttpGet("assets/{assetId:long}")]
    public async Task<ActionResult<IReadOnlyList<AuditLogItem>>> GetAssetTimeline(long assetId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAssetTimelineQuery(assetId), cancellationToken));
}
