using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.ScanInventoryAsset;

/// <param name="InventoryCode">Lo que trae la etiqueta (QR, lector USB o tecleado).</param>
/// <param name="LocationFoundId">Dónde se encontró; sin valor, el área del conteo.</param>
public sealed record ScanInventoryAssetCommand(
    long InventorySessionId,
    string InventoryCode,
    long? LocationFoundId,
    string? Notes) : ICommand<ScanInventoryAssetResponse>;
