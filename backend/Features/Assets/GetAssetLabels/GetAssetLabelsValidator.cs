using FluentValidation;

namespace TecnmInventario.Api.Features.Assets.GetAssetLabels;

public class GetAssetLabelsValidator : AbstractValidator<GetAssetLabelsQuery>
{
    public const int MaxLabels = 90;

    public GetAssetLabelsValidator()
    {
        RuleFor(x => x.AssetIds)
            .NotEmpty().WithMessage("Elige al menos un activo para imprimir su etiqueta.")
            .Must(ids => ids.Count <= MaxLabels).WithMessage($"Imprime a lo más {MaxLabels} etiquetas a la vez (tres hojas).");
    }
}
