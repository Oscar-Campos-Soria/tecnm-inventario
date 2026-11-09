using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Reports.GetReport;

namespace TecnmInventario.Api.Features.Reports;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "reports.view")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/reports/assets?format=xlsx&status=ASIGNADO
    [HttpGet("{report}")]
    public async Task<IActionResult> Get(
        string report,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? status = null,
        [FromQuery] long? assetTypeId = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _mediator.Send(
            new GetReportQuery(report, format.ToLowerInvariant(), status, assetTypeId, from, to), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
