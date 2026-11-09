using FluentValidation;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.CommitImport;

public class CommitImportValidator : AbstractValidator<CommitImportCommand>
{
    public CommitImportValidator()
    {
        RuleFor(x => x.File).SetValidator(new ImportFileValidator());
    }
}
