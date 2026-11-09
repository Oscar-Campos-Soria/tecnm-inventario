using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Components.AddComponent;
using TecnmInventario.Api.Features.Components.DeleteComponent;
using TecnmInventario.Api.Features.Components.GetAssetComponents;
using TecnmInventario.Api.Features.Components.RemoveComponent;
using TecnmInventario.Api.Features.Components.ReplaceComponent;

namespace TecnmInventario.Api.Features.Components;

[ApiController]
[Route("api/assets/{assetId:long}/components")]
[Authorize(Policy = "components.manage")]
public class ComponentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComponentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetAssetComponentsResponse>>> GetComponents(
        long assetId, [FromQuery] bool includeRemoved, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAssetComponentsQuery(assetId, includeRemoved), cancellationToken));

    public sealed record AddComponentBody(long ComponentTypeId, string? Description, string? SerialNumber, DateTime? InstalledAt);

    [HttpPost]
    public async Task<ActionResult<ComponentWriteResponse>> Add(
        long assetId, AddComponentBody body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new AddComponentCommand(assetId, body.ComponentTypeId, body.Description, body.SerialNumber, body.InstalledAt),
            cancellationToken);
        return CreatedAtAction(nameof(GetComponents), new { assetId }, result);
    }

    public sealed record ReplaceComponentBody(string? Description, string? SerialNumber, DateTime? ReplacedAt);

    [HttpPost("{assetComponentId:long}/replace")]
    public async Task<ActionResult<ComponentWriteResponse>> Replace(
        long assetId, long assetComponentId, [FromQuery] int expectedVersion,
        ReplaceComponentBody body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ReplaceComponentCommand(
                assetId, assetComponentId, expectedVersion, body.Description, body.SerialNumber, body.ReplacedAt),
            cancellationToken);
        return CreatedAtAction(nameof(GetComponents), new { assetId }, result);
    }

    public sealed record RemoveComponentBody(DateTime? RemovedAt);

    [HttpPost("{assetComponentId:long}/remove")]
    public async Task<ActionResult<ComponentWriteResponse>> Remove(
        long assetId, long assetComponentId, [FromQuery] int expectedVersion,
        RemoveComponentBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new RemoveComponentCommand(assetId, assetComponentId, expectedVersion, body.RemovedAt), cancellationToken));

    [HttpDelete("{assetComponentId:long}")]
    public async Task<ActionResult<ComponentWriteResponse>> Delete(
        long assetId, long assetComponentId, [FromQuery] int expectedVersion, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new DeleteComponentCommand(assetId, assetComponentId, expectedVersion), cancellationToken));
}
