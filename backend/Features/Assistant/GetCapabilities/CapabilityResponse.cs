namespace TecnmInventario.Api.Features.Assistant.GetCapabilities;

public sealed record CapabilityParameter(string Name, string Type, bool Required, string? Description, IReadOnlyList<string>? Values);

/// <summary>
/// Kind: Consulta (lee) o Borrador (prepara algo que el usuario confirma en pantalla). Permission null: cualquier
/// usuario del asistente. Source: queries que ejecuta (los handlers aplican sus validaciones y filtros por usuario).
/// </summary>
public sealed record CapabilityResponse(
    string Id,
    string Summary,
    string Description,
    string Kind,
    string? Permission,
    string Source,
    IReadOnlyList<CapabilityParameter> Parameters);
