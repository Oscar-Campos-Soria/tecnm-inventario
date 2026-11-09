using Mediator;
using TecnmInventario.Api.Features.Incidents.Shared;

namespace TecnmInventario.Api.Features.Incidents.ResolveIncident;

public sealed class ResolveIncidentHandler : ICommandHandler<ResolveIncidentCommand, ResolveIncidentResponse>
{
    private readonly IncidentResolver _resolver;

    public ResolveIncidentHandler(IncidentResolver resolver)
    {
        _resolver = resolver;
    }

    public async ValueTask<ResolveIncidentResponse> Handle(
        ResolveIncidentCommand command, CancellationToken cancellationToken)
    {
        var incident = await _resolver.LoadAsync(command.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException("El incidente solicitado no existe.");

        // Concurrencia optimista contra la versión que vio el cliente.
        _resolver.ExpectVersion(incident, command.ExpectedVersion);
        return await _resolver.ResolveAsync(incident, command.Solution, command.Diagnosis, cancellationToken);
    }
}
