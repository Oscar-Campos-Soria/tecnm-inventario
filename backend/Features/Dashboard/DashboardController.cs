using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Dashboard.GetSummary;

namespace TecnmInventario.Api.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "dashboard.view")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GetDashboardSummaryResponse>> GetSummary(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetDashboardSummaryQuery(), cancellationToken));
}
