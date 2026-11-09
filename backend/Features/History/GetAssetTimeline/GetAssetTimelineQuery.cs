using Mediator;
using TecnmInventario.Api.Features.History.GetAuditLog;

namespace TecnmInventario.Api.Features.History.GetAssetTimeline;

public sealed record GetAssetTimelineQuery(long AssetId) : IQuery<IReadOnlyList<AuditLogItem>>;
