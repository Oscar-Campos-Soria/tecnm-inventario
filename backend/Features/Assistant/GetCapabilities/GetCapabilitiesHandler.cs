using System.Text.Json.Nodes;
using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Features.Assistant.GetCapabilities;

public sealed class GetCapabilitiesHandler : IQueryHandler<GetCapabilitiesQuery, IReadOnlyList<CapabilityResponse>>
{
    private readonly AssistantOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUser;
    private readonly TecnmInventarioDbContext _db;

    public GetCapabilitiesHandler(AssistantOrchestrator orchestrator, ICurrentUserService currentUser, TecnmInventarioDbContext db)
    {
        _orchestrator = orchestrator;
        _currentUser = currentUser;
        _db = db;
    }

    public async ValueTask<IReadOnlyList<CapabilityResponse>> Handle(GetCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        var role = _currentUser.RoleCode ?? throw new ForbiddenAccessException("Sesión no válida.");
        var catalog = await AiCatalog.LoadAsync(_db, cancellationToken);
        var tools = await _orchestrator.AllowedToolsAsync(role, cancellationToken);
        return tools.Select(t => new CapabilityResponse(t.Name, t.Summary, t.Description, t.Kind.ToString(), t.Permission, t.Source,
            Parameters(t.Parameters(catalog)))).ToList();
    }

    private static List<CapabilityParameter> Parameters(JsonObject schema)
    {
        var required = (schema["required"] as JsonArray ?? []).Select(n => n?.ToString()).ToHashSet();
        return (schema["properties"] as JsonObject ?? []).Select(p => new CapabilityParameter(
            p.Key,
            p.Value?["type"]?.ToString() ?? "string",
            required.Contains(p.Key),
            p.Value?["description"]?.ToString(),
            (p.Value?["enum"] as JsonArray)?.Select(v => v!.ToString()).ToList())).ToList();
    }
}
