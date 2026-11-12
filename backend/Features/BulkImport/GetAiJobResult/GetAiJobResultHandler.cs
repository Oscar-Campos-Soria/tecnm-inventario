using Mediator;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.GetAiJobResult;

public sealed class GetAiJobResultHandler : IQueryHandler<GetAiJobResultQuery, GeneratedFile>
{
    private readonly AiImportJobs _jobs;
    private readonly ICurrentUserService _currentUser;

    public GetAiJobResultHandler(AiImportJobs jobs, ICurrentUserService currentUser)
    {
        _jobs = jobs;
        _currentUser = currentUser;
    }

    public ValueTask<GeneratedFile> Handle(GetAiJobResultQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        var job = _jobs.Get(query.JobId, userId);
        return ValueTask.FromResult(job.Result
            ?? throw new BusinessRuleViolationException("La conversión todavía no termina."));
    }
}
