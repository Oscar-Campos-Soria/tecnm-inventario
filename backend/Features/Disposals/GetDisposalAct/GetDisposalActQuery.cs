using Mediator;
using TecnmInventario.Api.Common.Pdf;

namespace TecnmInventario.Api.Features.Disposals.GetDisposalAct;

public sealed record GetDisposalActQuery(long AssetDisposalId) : IQuery<GeneratedFile>;
