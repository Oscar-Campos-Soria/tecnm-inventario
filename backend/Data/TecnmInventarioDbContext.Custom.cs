using Microsoft.EntityFrameworkCore;

namespace TecnmInventario.Api.Data;

public partial class TecnmInventarioDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var versionProp = entityType.FindProperty("Version");
            if (versionProp is not null && versionProp.ClrType == typeof(int))
                versionProp.IsConcurrencyToken = true;
        }
    }
}
