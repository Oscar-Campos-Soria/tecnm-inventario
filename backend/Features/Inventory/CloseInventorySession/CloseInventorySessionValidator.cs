using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.CloseInventorySession;

public class CloseInventorySessionValidator : AbstractValidator<CloseInventorySessionCommand>
{
    public CloseInventorySessionValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del conteo que está viendo (expectedVersion).");
    }
}
