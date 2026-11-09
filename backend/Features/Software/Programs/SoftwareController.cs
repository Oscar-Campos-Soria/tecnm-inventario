using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Software.Programs.CreateSoftware;
using TecnmInventario.Api.Features.Software.Programs.DeleteSoftware;
using TecnmInventario.Api.Features.Software.Programs.GetSoftware;
using TecnmInventario.Api.Features.Software.Programs.UpdateSoftware;

namespace TecnmInventario.Api.Features.Software.Programs;

[ApiController]
[Route("api/software")]
[Authorize(Policy = "software.manage")]
public class SoftwareController : ControllerBase
{
    private readonly IMediator _mediator;

    public SoftwareController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GetSoftwareResponse>>> GetSoftware(
        [FromQuery] PagedRequest pagination,
        [FromQuery] string? search,
        [FromQuery] long? softwareTypeId,
        CancellationToken cancellationToken)
    {
        var query = new GetSoftwareQuery(pagination.PageNumber, pagination.PageSize, search, softwareTypeId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CreateSoftwareResponse>> CreateSoftware(
        CreateSoftwareCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSoftware), new { }, result);
    }

    public sealed record UpdateSoftwareBody(string Name, string? Vendor, long? SoftwareTypeId);

    [HttpPut("{softwareId:long}")]
    public async Task<ActionResult<UpdateSoftwareResponse>> UpdateSoftware(
        long softwareId,
        [FromQuery] int expectedVersion,
        UpdateSoftwareBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSoftwareCommand(softwareId, expectedVersion, body.Name, body.Vendor, body.SoftwareTypeId);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{softwareId:long}")]
    public async Task<ActionResult<DeleteSoftwareResponse>> DeleteSoftware(
        long softwareId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new DeleteSoftwareCommand(softwareId, expectedVersion), cancellationToken));
}
