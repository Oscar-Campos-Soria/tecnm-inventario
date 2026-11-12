using FluentValidation;
using TecnmInventario.Api.Features.BulkImport.Shared;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.StartAiConvert;

public class StartAiConvertValidator : AbstractValidator<StartAiConvertCommand>
{
    public StartAiConvertValidator()
    {
        RuleFor(x => x.File.Content).Must(c => c.Length > 0).WithMessage("El archivo está vacío.")
            .Must(c => c.Length <= ImportFile.MaxBytes).WithMessage("El archivo pesa más de 5 MB.");
        RuleFor(x => x.File.Extension).Must(e => SourceReader.Extensions.Contains(e))
            .WithMessage("Sube un archivo .xlsx, .csv, .pdf, .jpg o .png.");
    }
}
