using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests;

internal static class TestDb
{
    public static TecnmInventarioDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-lote1-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TecnmInventarioDbContext(options);
    }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public long? UserId => 1;
    public string? Username => "admin";
    public string? RoleCode => "ADMIN";
    public System.Net.IPAddress? IpAddress => null;
}
