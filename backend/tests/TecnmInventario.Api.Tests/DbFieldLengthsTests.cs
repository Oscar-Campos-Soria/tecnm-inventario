using Npgsql;
using TecnmInventario.Api.Common.Domain;
using Xunit.Sdk;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Comprueba que las constantes de longitud usadas por los validadores
/// coincidan con las columnas reales del esquema inventario. Si no hay BD
/// disponible, el test se omite (no falla) para no romper CI sin PostgreSQL.
/// </summary>
public class DbFieldLengthsTests
{
    private static readonly Dictionary<(string Table, string Column), int> Expected = new()
    {
        [("assets", "inventory_code")] = DbFieldLengths.Assets.InventoryCode,
        [("assets", "serial_number")] = DbFieldLengths.Assets.SerialNumber,
        [("assets", "hostname")] = DbFieldLengths.Assets.Hostname,
        [("roles", "code")] = DbFieldLengths.Roles.Code,
        [("roles", "name")] = DbFieldLengths.Roles.Name,
        [("app_users", "username")] = DbFieldLengths.Users.Username,
        [("app_users", "email")] = DbFieldLengths.Users.Email,
        [("app_users", "first_name")] = DbFieldLengths.Users.FirstName,
        [("app_users", "last_name")] = DbFieldLengths.Users.LastName,
        [("acquisitions", "purchase_order")] = DbFieldLengths.Acquisitions.PurchaseOrder,
        [("acquisitions", "invoice_number")] = DbFieldLengths.Acquisitions.InvoiceNumber,
        [("acquisitions", "currency_code")] = DbFieldLengths.Acquisitions.CurrencyCode,
        [("documents", "file_name")] = DbFieldLengths.Documents.FileName,
        [("documents", "mime_type")] = DbFieldLengths.Documents.MimeType,
        [("suppliers", "name")] = DbFieldLengths.Suppliers.Name,
        [("suppliers", "tax_id")] = DbFieldLengths.Suppliers.TaxId,
        [("suppliers", "contact_name")] = DbFieldLengths.Suppliers.ContactName,
        [("suppliers", "email")] = DbFieldLengths.Suppliers.Email,
        [("suppliers", "phone")] = DbFieldLengths.Suppliers.Phone,
        [("asset_warranties", "warranty_number")] = DbFieldLengths.Warranties.WarrantyNumber
    };

    [Fact]
    public async Task Constantes_de_longitud_coinciden_con_information_schema()
    {
        var connectionString = ResolveConnectionString();
        if (connectionString is null)
        {
            throw SkipException.ForSkip("No hay cadena de conexión ni database/.env disponible.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw SkipException.ForSkip($"PostgreSQL no disponible: {ex.GetType().Name}.");
        }

        var actual = new Dictionary<(string Table, string Column), int>();
        await using (var command = new NpgsqlCommand(
            "select table_name, column_name, character_maximum_length " +
            "from information_schema.columns where table_schema = 'inventario' " +
            "and character_maximum_length is not null", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                actual[(reader.GetString(0), reader.GetString(1))] = reader.GetInt32(2);
            }
        }

        var mismatches = Expected
            .Where(pair => !actual.TryGetValue(pair.Key, out var length) || length != pair.Value)
            .Select(pair => actual.TryGetValue(pair.Key, out var length)
                ? $"{pair.Key.Table}.{pair.Key.Column}: constante={pair.Value}, BD={length}"
                : $"{pair.Key.Table}.{pair.Key.Column}: no existe en information_schema")
            .ToList();

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static string? ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var root = FindRepoRoot();
        if (root is null)
        {
            return null;
        }

        var envPath = Path.Combine(root, "database", ".env");
        if (!File.Exists(envPath))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim().Trim('"');
        }

        if (!values.TryGetValue("POSTGRES_USER", out var user) ||
            !values.TryGetValue("POSTGRES_PASSWORD", out var password) ||
            !values.TryGetValue("POSTGRES_DB", out var database))
        {
            return null;
        }

        return $"Host=localhost;Port=5433;Database={database};Username={user};Password={password};Timeout=3";
    }

    private static string? FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "database")) &&
                File.Exists(Path.Combine(directory.FullName, "database", ".env")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
