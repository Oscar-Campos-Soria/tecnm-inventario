using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.AdjustInventoryRecord;

public class AdjustInventoryRecordValidator : AbstractValidator<AdjustInventoryRecordCommand>
{
    public AdjustInventoryRecordValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del renglón que está viendo (expectedVersion).");
        RuleFor(x => x.Adjustment)
            .Must(a => a is not null && InventoryAdjustmentCodes.All.Contains(a))
            .WithMessage("Ajuste no válido: MOVIMIENTO, INCIDENTE o SIN_CAMBIO.");
        RuleFor(x => x.IncidentId)
            .NotNull().WithMessage("Indica el incidente levantado por el equipo no localizado.")
            .When(x => x.Adjustment == InventoryAdjustmentCodes.Incidente);
    }
}
