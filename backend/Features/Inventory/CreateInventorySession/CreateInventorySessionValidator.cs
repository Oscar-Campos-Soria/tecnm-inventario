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

public class CreateInventorySessionValidator : AbstractValidator<CreateInventorySessionCommand>
{
    public CreateInventorySessionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ponle nombre al conteo.").MaximumLength(150);
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Elige qué edificio o área se va a contar.");
    }
}
