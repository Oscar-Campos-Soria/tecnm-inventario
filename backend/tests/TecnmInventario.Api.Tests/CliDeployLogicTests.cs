using System.Security.Cryptography.X509Certificates;
using TecnmCli;

namespace TecnmInventario.Api.Tests;

/// <summary>Lógica pura del CLI tecnm: orden de scripts, retención, .env.prod y certificados.</summary>
public class CliDeployLogicTests
{
    private static string RepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join('/', parts));
    }

    [Fact]
    public void Orden_de_scripts_sale_del_README_sin_los_de_desarrollo()
    {
        var order = SqlScripts.ProductionOrder(File.ReadAllText(RepoFile("scripts", "README.md")));

        Assert.Equal("database.sql", order[0]);
        Assert.Equal("scripts/seed-database.sql", order[1]);
        Assert.Contains("scripts/deactivate-system-account.sql", order);
        Assert.DoesNotContain(order, s => s.Contains("seed-locations-demo") || s.Contains("seed-test-users"));
        Assert.All(order, s => Assert.True(File.Exists(RepoFile(s.Split('/'))), s));
    }

    [Fact]
    public void Retencion_conserva_los_14_mas_nuevos_con_sus_documentos()
    {
        var start = new DateTime(2026, 1, 1, 2, 0, 0);
        var names = Enumerable.Range(0, 16)
            .SelectMany(i => new[] { BackupFiles.DbFile(BackupFiles.Stamp(start.AddDays(i))), BackupFiles.DocsFile(BackupFiles.Stamp(start.AddDays(i))) })
            .Append("respaldo.log")
            .ToList();

        var delete = BackupFiles.ToDelete(names);

        Assert.Equal(4, delete.Count); // los 2 más viejos, base de datos y documentos
        Assert.All(delete, f => Assert.True(f.Contains("20260101") || f.Contains("20260102"), f));
        Assert.Equal(BackupFiles.Stamp(start.AddDays(15)), BackupFiles.Stamps(names)[0]);
    }

    [Fact]
    public void Env_generado_es_valido_y_los_problemas_no_muestran_valores()
    {
        var env = ProdEnv.Parse(ProdEnv.Generate("192.168.1.50"));
        Assert.Empty(ProdEnv.Problems(env));
        Assert.True(env["JWT_SECRET"].Length >= ProdEnv.MinJwtSecret);
        Assert.DoesNotContain(";", env["POSTGRES_PASSWORD"]);

        var weak = ProdEnv.Parse("PUBLIC_HOST=x\nPOSTGRES_USER=u\nPOSTGRES_DB=d\nPOSTGRES_PASSWORD=corta123\nJWT_SECRET=");
        var problems = ProdEnv.Problems(weak);
        Assert.Contains(problems, p => p.Contains("POSTGRES_PASSWORD es débil"));
        Assert.Contains(problems, p => p.Contains("Falta JWT_SECRET"));
        Assert.DoesNotContain(problems, p => p.Contains("corta123"));
    }

    [Fact]
    public void Certificado_del_servidor_lo_firma_la_CA_y_cubre_la_IP()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tecnm-certs-{Guid.NewGuid():N}");
        var otherDir = dir + "-otra";
        try
        {
            var now = DateTimeOffset.Now;
            Certificates.Issue(dir, "192.168.1.50", now);
            var first = File.ReadAllText(Path.Combine(dir, Certificates.CaCert));
            using var server = Certificates.ReadServer(dir)!;

            Assert.True(Certificates.IsTrusted(server, dir));
            var san = server.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
            Assert.Contains(san.EnumerateIPAddresses(), ip => ip.ToString() == "192.168.1.50");
            Assert.Contains("localhost", san.EnumerateDnsNames());
            Assert.True(server.NotAfter <= now.AddDays(Certificates.ServerDays + 1).DateTime);

            // Regenerar conserva la CA: los equipos no reinstalan el certificado raíz.
            Certificates.Issue(dir, "192.168.1.50", now);
            Assert.Equal(first, File.ReadAllText(Path.Combine(dir, Certificates.CaCert)));

            // Un certificado de otra CA no es de confianza para esta.
            Certificates.Issue(otherDir, "192.168.1.50", now);
            using var foreign = Certificates.ReadServer(otherDir)!;
            Assert.False(Certificates.IsTrusted(foreign, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            if (Directory.Exists(otherDir)) Directory.Delete(otherDir, recursive: true);
        }
    }
}
