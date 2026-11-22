using Mediator;

namespace TecnmInventario.Api.Features.Assistant.GetCapabilities;

/// <summary>Registro de capacidades del asistente que el rol del usuario puede usar (generado de las herramientas).</summary>
public sealed record GetCapabilitiesQuery : IQuery<IReadOnlyList<CapabilityResponse>>;
