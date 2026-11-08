using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.MarkInventoryRecordNotFound;

public class MarkInventoryRecordNotFoundValidator : AbstractValidator<MarkInventoryRecordNotFoundCommand>
{
    public MarkInventoryRecordNotFoundValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del renglón que está viendo (expectedVersion).");
    }
}
