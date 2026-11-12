using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.GetAiStatus;

public sealed class GetAiStatusHandler : IQueryHandler<GetAiStatusQuery, GetAiStatusResponse>
{
    private readonly ILocalAiClient _ai;

    public GetAiStatusHandler(ILocalAiClient ai)
    {
        _ai = ai;
    }

    public async ValueTask<GetAiStatusResponse> Handle(GetAiStatusQuery query, CancellationToken cancellationToken)
        => new(await _ai.IsAvailableAsync(cancellationToken));
}
