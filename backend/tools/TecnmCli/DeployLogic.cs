using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace TecnmCli;

/// <summary>Orden de los scripts SQL de producción, leído de scripts/README.md.</summary>
internal static class SqlScripts
{
    private static readonly Regex ScriptPath = new(@"-f\s+(\S+\.sql)", RegexOptions.Compiled);

    /// <summary>
    /// Rutas relativas a la raíz del repo, en el orden del README. Se omiten los
    /// marcados como "OPCIONAL"/"solo desarrollo" y los que piden variables (-v).
    /// </summary>
    public static IReadOnlyList<string> ProductionOrder(string readme)
    {
        var result = new List<string>();
        foreach (var raw in readme.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("psql", StringComparison.Ordinal)) continue;
            if (line.Contains("OPCIONAL", StringComparison.OrdinalIgnoreCase)
                || line.Contains("solo desarrollo", StringComparison.OrdinalIgnoreCase)
                || line.Contains(" -v ", StringComparison.Ordinal))
                continue;

            var match = ScriptPath.Match(line);
            if (!match.Success) continue;
            var path = match.Groups[1].Value;
            result.Add(path.StartsWith("../", StringComparison.Ordinal) ? path[3..] : $"scripts/{path}");
        }
        return result;
    }
}

/// <summary>Nombres y retención de respaldos (un par base de datos + documentos por fecha).</summary>
internal static class BackupFiles
{
    public const int Keep = 14;
    public const string DbPrefix = "tecnm-db-";
    public const string DocsPrefix = "tecnm-docs-";
    public const string DbExtension = ".dump";
    public const string DocsExtension = ".tar.gz";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    public static string Stamp(DateTime now) => now.ToString(StampFormat, CultureInfo.InvariantCulture);
    public static string DbFile(string stamp) => DbPrefix + stamp + DbExtension;
    public static string DocsFile(string stamp) => DocsPrefix + stamp + DocsExtension;

    /// <summary>Fechas de respaldo con base de datos presente, de la más nueva a la más vieja.</summary>
    public static IReadOnlyList<string> Stamps(IEnumerable<string> fileNames) => fileNames
        .Where(f => f.StartsWith(DbPrefix, StringComparison.Ordinal) && f.EndsWith(DbExtension, StringComparison.Ordinal))
        .Select(f => f[DbPrefix.Length..^DbExtension.Length])
        .Where(s => DateTime.TryParseExact(s, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        .OrderByDescending(s => s, StringComparer.Ordinal)
        .ToList();

    /// <summary>Archivos que sobran al conservar solo los <see cref="Keep"/> respaldos más nuevos.</summary>
    public static IReadOnlyList<string> ToDelete(IEnumerable<string> fileNames)
    {
        var names = fileNames.ToList();
        var old = Stamps(names).Skip(Keep).ToHashSet();
        return names.Where(f => old.Any(s => f == DbFile(s) || f == DocsFile(s))).ToList();
    }
}

/// <summary>database/.env.prod: generación con secretos aleatorios y validación.</summary>
internal static class ProdEnv
{
    public const int MinDbPassword = 24;
    public const int MinJwtSecret = 64;

    public static string Generate(string publicHost) => string.Join('\n',
        "# Generado por tecnm. No se versiona ni se comparte.",
        $"PUBLIC_HOST={publicHost}",
        "POSTGRES_USER=tecnm_prod",
        "POSTGRES_DB=tecnm_inventario",
        $"POSTGRES_PASSWORD={Secret(32)}",
        $"JWT_SECRET={Secret(64)}",
        "");

    // Base64 URL: sin ';', comillas ni '=' que rompan la cadena de conexión o el .env.
    private static string Secret(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static Dictionary<string, string> Parse(string text) => text
        .Split('\n')
        .Select(l => l.Trim())
        .Where(l => l.Length > 0 && !l.StartsWith('#') && l.Contains('='))
        .Select(l => (Key: l[..l.IndexOf('=')].Trim(), Value: l[(l.IndexOf('=') + 1)..].Trim()))
        .GroupBy(p => p.Key)
        .ToDictionary(g => g.Key, g => g.Last().Value);

    /// <summary>Problemas que impiden instalar o actualizar (vacío = válido). Nunca incluye los valores.</summary>
    public static IReadOnlyList<string> Problems(IReadOnlyDictionary<string, string> env)
    {
        var problems = new List<string>();
        foreach (var key in new[] { "PUBLIC_HOST", "POSTGRES_USER", "POSTGRES_DB", "POSTGRES_PASSWORD", "JWT_SECRET" })
            if (!env.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
                problems.Add($"Falta {key}.");
        if (env.TryGetValue("POSTGRES_PASSWORD", out var pw) && pw.Length is > 0 and < MinDbPassword)
            problems.Add($"POSTGRES_PASSWORD es débil: mínimo {MinDbPassword} caracteres.");
        if (env.TryGetValue("JWT_SECRET", out var jwt) && jwt.Length is > 0 and < MinJwtSecret)
            problems.Add($"JWT_SECRET es débil: mínimo {MinJwtSecret} caracteres.");
        return problems;
    }
}

/// <summary>CA propia y certificado del servidor (sin librerías: System.Security.Cryptography).</summary>
internal static class Certificates
{
    public const string CaCert = "ca.crt";
    public const string CaKey = "ca.key";
    public const string ServerCert = "server.crt";
    public const string ServerKey = "server.key";

    // 825 días: el máximo que aceptan Apple/Chrome para certificados de servidor.
    public const int ServerDays = 825;

    /// <summary>Crea la CA si no existe y emite el certificado del servidor para el host (IP o nombre) y localhost.</summary>
    public static void Issue(string dir, string publicHost, DateTimeOffset now)
    {
        Directory.CreateDirectory(dir);
        using var ca = LoadOrCreateCa(dir, now);

        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={publicHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));

        var san = new SubjectAlternativeNameBuilder();
        if (IPAddress.TryParse(publicHost, out var ip)) san.AddIpAddress(ip); else san.AddDnsName(publicHost);
        if (publicHost != "localhost") san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());

        using var server = request.Create(ca, now.AddDays(-1), now.AddDays(ServerDays), RandomNumberGenerator.GetBytes(16));
        // nginx recibe la cadena completa: servidor + CA.
        WriteSecret(Path.Combine(dir, ServerKey), key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(dir, ServerCert), server.ExportCertificatePem() + "\n" + ca.ExportCertificatePem() + "\n");
    }

    private static X509Certificate2 LoadOrCreateCa(string dir, DateTimeOffset now)
    {
        var certPath = Path.Combine(dir, CaCert);
        var keyPath = Path.Combine(dir, CaKey);
        if (File.Exists(certPath) && File.Exists(keyPath))
            return X509Certificate2.CreateFromPemFile(certPath, keyPath);

        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=TecNM Inventario CA, O=TecNM", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var ca = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));
        WriteSecret(keyPath, key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(certPath, ca.ExportCertificatePem() + "\n");
        return X509Certificate2.CreateFromPemFile(certPath, keyPath);
    }

    /// <summary>Primer certificado (el del servidor) del archivo PEM, o null.</summary>
    public static X509Certificate2? ReadServer(string dir)
    {
        var path = Path.Combine(dir, ServerCert);
        return File.Exists(path) ? X509Certificate2.CreateFromPem(File.ReadAllText(path)) : null;
    }

    /// <summary>Valida la cadena contra la CA propia (si existe ca.crt) o contra los certificados del sistema.</summary>
    public static bool IsTrusted(X509Certificate2 certificate, string dir)
    {
        var caPath = Path.Combine(dir, CaCert);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (File.Exists(caPath))
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(X509Certificate2.CreateFromPem(File.ReadAllText(caPath)));
        }
        return chain.Build(certificate);
    }

    /// <summary>Archivo legible solo por el dueño en Linux (en Windows hereda los permisos de la carpeta).</summary>
    public static void WriteSecret(string path, string content)
    {
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
