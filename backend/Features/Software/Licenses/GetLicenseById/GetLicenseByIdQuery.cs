using Mediator;

namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseById;

public sealed record GetLicenseByIdQuery(long LicenseId) : IQuery<GetLicenseByIdResponse>;
