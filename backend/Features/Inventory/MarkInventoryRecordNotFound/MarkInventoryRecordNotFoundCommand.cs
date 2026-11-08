using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.MarkInventoryRecordNotFound;

public sealed record MarkInventoryRecordNotFoundCommand(long InventorySessionId, long InventoryRecordId, int ExpectedVersion)
    : ICommand<MarkInventoryRecordNotFoundResponse>;
