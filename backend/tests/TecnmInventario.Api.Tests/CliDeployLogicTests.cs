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

    [Fact]
    public void Env_set_cambia_o_agrega_sin_tocar_lo_demas()
    {
        var text = "# comentario\nPUBLIC_HOST=a\nJWT_SECRET=x=y\n";

        var added = ProdEnv.Set(text, "COMPOSE_PROFILES", "ia");
        Assert.Equal("# comentario\nPUBLIC_HOST=a\nJWT_SECRET=x=y\nCOMPOSE_PROFILES=ia\n", added);
        Assert.True(AiModel.Enabled(ProdEnv.Parse(added)));

        var cleared = ProdEnv.Set(added, "COMPOSE_PROFILES", "");
        Assert.Equal("# comentario\nPUBLIC_HOST=a\nJWT_SECRET=x=y\nCOMPOSE_PROFILES=\n", cleared);
        Assert.False(AiModel.Enabled(ProdEnv.Parse(cleared)));
        Assert.Equal("x=y", ProdEnv.Parse(cleared)["JWT_SECRET"]);
    }

    [Fact]
    public void Modelo_solo_se_acepta_con_tamano_y_huella_exactos()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "abc");
            const string sha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"; // SHA256("abc")

            Assert.True(AiModel.Matches(path, 3, sha));
            Assert.False(AiModel.Matches(path, 4, sha));
            Assert.False(AiModel.Matches(path, 3, new string('0', 64)));
            Assert.False(AiModel.Matches(path + ".no-existe", 3, sha));
            Assert.Equal(["Qwen3-1.7B-Q4_K_M.gguf"],
                AiModel.Stale(["Qwen3-1.7B-Q4_K_M.gguf", .. AiModel.Files.Select(f => f.FileName), "notas.txt"]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Argumentos_aceptan_nombres_de_antes_opciones_y_banderas()
    {
        var legacy = CliArgs.Parse(["ia", "instalar", "--desde", "/mnt/usb"]);
        Assert.Equal(["ai", "install"], legacy.Words);
        Assert.Equal("/mnt/usb", legacy.Get("from"));

        var args = CliArgs.Parse(["restore", "--backup=20261116-160800", "--yes", "--json"]);
        Assert.Equal("restore", args.Command);
        Assert.Equal("20261116-160800", args.Get("backup"));
        Assert.True(args.Has("yes") && args.Has("json"));
        Assert.Equal("menu", CliArgs.Parse([]).Command);
        Assert.Equal("backup", CliArgs.Parse(["respaldar"]).Command);
    }

    [Theory]
    [InlineData("start", "--bogus")]
    [InlineData("logs", "--tail")]
    [InlineData("restore", "--yes=si")]
    public void Opcion_invalida_es_error_de_uso(string command, string option)
    {
        var ex = Assert.Throws<CliException>(() => CliArgs.Parse([command, option]));
        Assert.Equal(ExitCodes.Usage, ex.ExitCode);
        Assert.Equal("USAGE", ex.Code);
    }

    [Fact]
    public void Destructiva_sin_yes_y_dato_faltante_tienen_su_codigo()
    {
        Assert.Equal(ExitCodes.NeedsConfirmation, CliException.NeedsYes("Restaurar reemplaza los datos.").ExitCode);
        var missing = Assert.Throws<CliException>(() => CliArgs.Parse(["restore"]).Require("backup", "fecha"));
        Assert.Equal(ExitCodes.Usage, missing.ExitCode);
        Assert.Contains("--backup", missing.Message);
    }

    [Fact]
    public void Contenedores_se_leen_en_los_dos_formatos_de_compose()
    {
        const string lines = "{\"Service\":\"api\",\"State\":\"running\",\"Health\":\"healthy\",\"Status\":\"Up 2 minutes\"}\n{\"Service\":\"db\",\"State\":\"exited\",\"Status\":\"Exited (0)\"}";
        var fromLines = Deployment.ParseContainers(lines);
        Assert.Equal(["api", "db"], fromLines.Select(c => c.Service));
        Assert.Equal("", fromLines[1].Health);

        var fromArray = Deployment.ParseContainers("[{\"Service\":\"web\",\"State\":\"running\"}]");
        Assert.Equal("web", Assert.Single(fromArray).Service);
        Assert.Empty(Deployment.ParseContainers("no es json"));
    }

    [Theory]
    [InlineData("auto", 8192L, false, "nvidia")]
    [InlineData("auto", 2048L, false, "cpu")]    // NVIDIA chica: no cabe, procesador
    [InlineData("auto", 2048L, true, "vulkan")]  // ...salvo que haya otra tarjeta en Linux
    [InlineData("auto", null, true, "vulkan")]
    [InlineData("auto", null, false, "cpu")]     // Windows con AMD/Intel o sin tarjeta
    [InlineData("cpu", 8192L, true, "cpu")]      // lo pedido gana
    [InlineData("nvidia", null, false, "nvidia")]
    public void Aceleracion_de_la_IA_se_elige_segun_la_tarjeta(string requested, long? vram, bool renderNode, string expected) =>
        Assert.Equal(expected, AiAccel.Choose(requested, vram, renderNode));

    [Fact]
    public void Lee_memoria_de_nvidia_smi_y_capas_de_los_logs()
    {
        Assert.Equal(8192, AiAccel.ParseVram("8192\n4096\n"));
        Assert.Null(AiAccel.ParseVram("NVIDIA-SMI has failed"));
        Assert.Equal(25, AiAccel.OffloadedLayers("load_tensors: offloaded 0/25 layers to GPU\nload_tensors: offloaded 25/25 layers to GPU"));
        Assert.Equal(0, AiAccel.OffloadedLayers("main: server is listening"));
        Assert.Null(AiAccel.OverrideFile("cpu"));
        Assert.Equal("docker-compose.ia-nvidia.yml", AiAccel.OverrideFile("nvidia"));
        Assert.All(AiAccel.Modes.Select(AiAccel.OverrideFile).OfType<string>(), f => Assert.True(File.Exists(RepoFile("database", f)), f));
        Assert.Equal(6, AiAccel.Threads(12));
        Assert.Equal(2, AiAccel.Threads(2));
    }
}
