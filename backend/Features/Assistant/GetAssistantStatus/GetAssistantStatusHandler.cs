using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.GetAssistantStatus;

public sealed class GetAssistantStatusHandler : IQueryHandler<GetAssistantStatusQuery, GetAssistantStatusResponse>
{
    private readonly ILocalAiClient _ai;
    private readonly AssistantOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUser;

    public GetAssistantStatusHandler(ILocalAiClient ai, AssistantOrchestrator orchestrator, ICurrentUserService currentUser)
    {
        _ai = ai;
        _orchestrator = orchestrator;
        _currentUser = currentUser;
    }

    public async ValueTask<GetAssistantStatusResponse> Handle(GetAssistantStatusQuery query, CancellationToken cancellationToken)
    {
        var role = _currentUser.RoleCode ?? throw new ForbiddenAccessException("Sesión no válida.");
        var tools = await _orchestrator.AllowedToolsAsync(role, cancellationToken);
        return new GetAssistantStatusResponse(await _ai.IsAvailableAsync(cancellationToken), tools.Select(t => t.Name).ToList());
    }
}
