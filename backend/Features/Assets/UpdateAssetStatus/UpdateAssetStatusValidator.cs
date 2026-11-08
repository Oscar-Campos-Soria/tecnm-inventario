using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

public class UpdateAssetStatusValidator : AbstractValidator<UpdateAssetStatusCommand>
{
    // DADO_DE_BAJA no se alcanza por aquí: exige la baja formal (POST /api/disposals,
    // con folio, motivo y precondiciones) para que no haya bajas sin acta.
    private static readonly HashSet<string> ValidCodes = new()
    {
        AssetStatusCodes.Disponible, AssetStatusCodes.Asignado, AssetStatusCodes.EnMantenimiento,
        AssetStatusCodes.ConFalla
    };

    private readonly TecnmInventarioDbContext _context;

    public UpdateAssetStatusValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.NewStatusCode)
            .Must(code => code != AssetStatusCodes.DadoDeBaja)
            .WithMessage("Para dar de baja un activo use la baja formal (folio, motivo y acta).")
            .Must(code => code == AssetStatusCodes.DadoDeBaja || ValidCodes.Contains(code))
            .WithMessage($"Estado inválido. Valores permitidos: {string.Join(", ", ValidCodes)}.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Debe indicar el motivo del cambio de estado.")
            .MaximumLength(500);

        RuleFor(x => x)
            .MustAsync(BeAValidTransition)
            .WithMessage("Esa transición de estado no está permitida.")
            .WithName("NewStatusCode")
            .When(x => ValidCodes.Contains(x.NewStatusCode));
    }

    private async Task<bool> BeAValidTransition(UpdateAssetStatusCommand command, CancellationToken ct)
    {
        var currentCode = await _context.Assets
            .Where(a => a.AssetId == command.AssetId && !a.IsDeleted)
            .Select(a => a.AssetStatus.Code)
            .FirstOrDefaultAsync(ct);

        // Si el activo no existe, no es responsabilidad del validator decirlo
        // (el Handler lanza 404); aquí solo evitamos un falso "transición
        // inválida" cuando en realidad el problema es otro.
        if (currentCode is null)
        {
            return true;
        }

        return AssetStatusTransitions.IsAllowed(currentCode, command.NewStatusCode);
    }
}
