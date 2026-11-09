using System.Text;
using ClosedXML.Excel;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Features.BulkImport.CommitImport;
using TecnmInventario.Api.Features.BulkImport.PreviewImport;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Carga masiva de activos: lectura de CSV (comillas, punto y coma, encabezados
/// con acentos) y Excel, mismas reglas que el alta individual y todo-o-nada al
/// confirmar, sobre PostgreSQL real.
/// </summary>
[Trait("Category", "Integration")]
public sealed class BulkImportIntegrationTests : PostgresTestBase
{
    public BulkImportIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    private static ImportFile Csv(string text) => new("activos.csv", Encoding.UTF8.GetBytes("﻿" + text));

    [Fact]
    public void Parser_lee_csv_con_punto_y_coma_comillas_y_encabezados_con_acentos()
    {
        var rows = AssetImportParser.Parse(Csv(
            "Código Inventario;Tipo;Notas;Columna extra\r\nTEC-1;PC;\"Dice \"\"hola\"\"; con punto y coma\";x\r\n\r\nTEC-2;PC;;\r\n"));

        Assert.Equal(2, rows.Count);
        Assert.Equal(("TEC-1", 2), (rows[0][AssetImportParser.Code], rows[0].RowNumber));
        Assert.Equal("Dice \"hola\"; con punto y coma", rows[0][AssetImportParser.Notes]);
        Assert.Null(rows[1][AssetImportParser.Notes]);
        Assert.Equal(4, rows[1].RowNumber);
    }

    [Fact]
    public void Parser_rechaza_columnas_obligatorias_faltantes_y_demasiadas_filas()
    {
        Assert.Throws<BusinessRuleViolationException>(() => AssetImportParser.Parse(Csv("codigo_inventario,serie\nTEC-1,123\n")));
        var many = "codigo_inventario,tipo\n" + string.Concat(Enumerable.Range(1, ImportFile.MaxRows + 1).Select(i => $"T-{i},PC\n"));
        Assert.Throws<BusinessRuleViolationException>(() => AssetImportParser.Parse(Csv(many)));
    }

    [DockerFact]
    public async Task Excel_valido_crea_todo_y_audita_y_con_un_error_no_crea_nada()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var existing = h.AddAsset(Common.Domain.AssetStatusCodes.Disponible);
        var typeCode = h.Context.AssetTypes.First().Code;
        var typeName = h.Context.AssetTypes.First().Name;
        var user = new FakeCurrentUser();
        var checker = new AssetImportChecker(h.Context);

        // Con errores: código ya usado, repetido en el archivo, tipo y ubicación inexistentes.
        var bad = Xlsx(
            new[] { existing.InventoryCode, typeCode, "INT-ALM" },
            new[] { "ZZ-IMP-1", "NO_EXISTE", "INT-ALM" },
            new[] { "ZZ-IMP-2", typeName, "NO-EXISTE" },
            new[] { "ZZ-IMP-2", typeCode, "" });
        var preview = await new PreviewImportHandler(checker).Handle(new PreviewImportQuery(bad), default);
        Assert.Equal((4, 0), (preview.TotalRows, preview.ValidRows));
        Assert.Contains(preview.Rows[0].Errors, e => e.Contains("ya está en uso"));
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("NO_EXISTE"));
        Assert.Contains(preview.Rows[2].Errors, e => e.Contains("NO-EXISTE"));
        Assert.Contains(preview.Rows[3].Errors, e => e.Contains("se repite"));

        var commit = new CommitImportHandler(h.Context, checker, user, new AuditLogService(h.Context, user));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => commit.Handle(new CommitImportCommand(bad), default).AsTask());
        Assert.Equal(0, await db.CountAsync("assets", "inventory_code like 'ZZ-IMP-%'"));

        // Válido: tipo por código o por nombre, ubicación por código.
        var good = Xlsx(
            new[] { "ZZ-IMP-1", typeCode, "INT-ALM" },
            new[] { "ZZ-IMP-2", typeName.ToUpperInvariant(), "" });
        var result = await commit.Handle(new CommitImportCommand(good), default);

        Assert.Equal(2, result.Created);
        Assert.Equal(2, await db.CountAsync("assets", "inventory_code like 'ZZ-IMP-%'"));
        Assert.Equal(2, await db.CountAsync("audit_log", "table_name = 'assets' and action = 'INSERT' and new_data ->> 'Source' = 'CARGA_MASIVA'"));
    }

    private static ImportFile Xlsx(params string[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Activos");
        sheet.Cell(1, 1).Value = "codigo_inventario";
        sheet.Cell(1, 2).Value = "tipo";
        sheet.Cell(1, 3).Value = "ubicacion";
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return new ImportFile("activos.xlsx", stream.ToArray());
    }
}
