using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.AdjustInventoryRecord;

public sealed record AdjustInventoryRecordCommand(
    long InventorySessionId,
    long InventoryRecordId,
    int ExpectedVersion,
    string Adjustment,
    long? IncidentId) : ICommand<AdjustInventoryRecordResponse>;
