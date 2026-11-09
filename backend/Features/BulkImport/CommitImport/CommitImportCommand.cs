using Mediator;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.CommitImport;

public sealed record CommitImportCommand(ImportFile File) : ICommand<CommitImportResponse>, ITransactionalCommand;

public sealed record CommitImportResponse(int Created, IReadOnlyList<long> AssetIds);
