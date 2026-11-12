using Mediator;

namespace TecnmInventario.Api.Features.Assistant.GetAssistantStatus;

/// <summary>¿El asistente responde y qué puede consultar este usuario? La pantalla oculta el botón si no responde.</summary>
public sealed record GetAssistantStatusQuery : IQuery<GetAssistantStatusResponse>;

public sealed record GetAssistantStatusResponse(bool Enabled, IReadOnlyList<string> Tools);
