using Mediator;

namespace TecnmInventario.Api.Features.BulkImport.GetAiStatus;

/// <summary>¿El asistente IA está encendido? La pantalla oculta la conversión si no.</summary>
public sealed record GetAiStatusQuery : IQuery<GetAiStatusResponse>;

public sealed record GetAiStatusResponse(bool Enabled);
