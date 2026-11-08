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

public class ScanInventoryAssetValidator : AbstractValidator<ScanInventoryAssetCommand>
{
    public ScanInventoryAssetValidator()
    {
        RuleFor(x => x.InventoryCode).NotEmpty().WithMessage("Escanea o escribe el código de inventario.").MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
