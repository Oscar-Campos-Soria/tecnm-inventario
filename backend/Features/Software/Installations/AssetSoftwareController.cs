using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Software.Installations.GetAssetSoftware;
using TecnmInventario.Api.Features.Software.Installations.InstallSoftware;
using TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;

namespace TecnmInventario.Api.Features.Software.Installations;

[ApiController]
[Route("api/assets/{assetId:long}/software")]
[Authorize(Policy = "software.manage")]
public class AssetSoftwareController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssetSoftwareController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetAssetSoftwareResponse>>> GetAssetSoftware(
        long assetId, [FromQuery] bool includeHistory, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAssetSoftwareQuery(assetId, includeHistory), cancellationToken));

    public sealed record InstallSoftwareBody(long SoftwareId, string? VersionInstalled, DateTime? InstalledAt);

    [HttpPost]
    public async Task<ActionResult<InstallSoftwareResponse>> Install(
        long assetId, InstallSoftwareBody body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new InstallSoftwareCommand(assetId, body.SoftwareId, body.VersionInstalled, body.InstalledAt),
            cancellationToken);
        return CreatedAtAction(nameof(GetAssetSoftware), new { assetId }, result);
    }

    [HttpPost("{assetSoftwareId:long}/uninstall")]
    public async Task<ActionResult<UninstallSoftwareResponse>> Uninstall(
        long assetId,
        long assetSoftwareId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new UninstallSoftwareCommand(assetId, assetSoftwareId, expectedVersion), cancellationToken));
}
