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

public sealed record ScanInventoryAssetResponse(
    long InventoryRecordId,
    long AssetId,
    string InventoryCode,
    string Status,
    // Ya estaba registrado como encontrado (escaneo repetido): no cambia nada.
    bool AlreadyChecked,
    string AssetStatusCode);
