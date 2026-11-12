using Mediator;

namespace TecnmInventario.Api.Features.Assets.GetMyAssets;

/// <summary>
/// Equipos con asignación vigente del usuario que pregunta ("Mis equipos"). Cada quien ve solo lo suyo,
/// sin assets.view: así el usuario general reporta fallas de su equipo sin ver el inventario completo.
/// </summary>
public sealed record GetMyAssetsQuery : IQuery<IReadOnlyList<MyAssetResponse>>;
