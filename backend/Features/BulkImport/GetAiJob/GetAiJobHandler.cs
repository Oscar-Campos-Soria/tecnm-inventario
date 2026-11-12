using Mediator;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.GetAiJob;

public sealed class GetAiJobHandler : IQueryHandler<GetAiJobQuery, AiJobSnapshot>
{
    private readonly AiImportJobs _jobs;
    private readonly ICurrentUserService _currentUser;

    public GetAiJobHandler(AiImportJobs jobs, ICurrentUserService currentUser)
    {
        _jobs = jobs;
        _currentUser = currentUser;
    }

    public ValueTask<AiJobSnapshot> Handle(GetAiJobQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        return ValueTask.FromResult(_jobs.Get(query.JobId, userId).Snapshot());
    }
}
