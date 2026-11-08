using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Locations.Shared;

namespace TecnmInventario.Api.Features.Inventory.CreateInventorySession;

public sealed record CreateInventorySessionResponse(long InventorySessionId, string Name, int ExpectedAssets, int Version);
