using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Assets.GetAssets;

public class GetAssetsValidator : AbstractValidator<GetAssetsQuery>
{
    private static readonly string[] ValidStatusCodes =
    {
        AssetStatusCodes.Disponible, AssetStatusCodes.Asignado, AssetStatusCodes.EnMantenimiento,
        AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja
    };

    public GetAssetsValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);

        RuleFor(x => x.StatusCode)
            .Must(code => ValidStatusCodes.Contains(code!.Trim().ToUpperInvariant()))
            .WithMessage("El estado de activo especificado no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.StatusCode));
    }
}
