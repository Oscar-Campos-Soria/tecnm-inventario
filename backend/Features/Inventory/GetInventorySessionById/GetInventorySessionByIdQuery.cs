using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessionById;

public sealed record GetInventorySessionByIdQuery(long InventorySessionId) : IQuery<InventorySessionDetail>;
