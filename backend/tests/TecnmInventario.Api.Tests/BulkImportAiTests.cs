using System.Text.Json.Nodes;
using ClosedXML.Excel;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.BulkImport.Shared;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace TecnmInventario.Api.Tests;

/// <summary>Carga masiva con IA: lector de archivos, reglas sin IA, cola de trabajos y proceso completo con IA simulada.</summary>
public class BulkImportAiTests
{
    private static readonly AiCatalog Catalog = new(
        [("LAPTOP", "Laptop"), ("PC", "Computadora de Escritorio"), ("PROJECTOR", "Proyector"), ("ROUTER", "Router"), ("SWITCH", "Switch de Red")],
        [
            ("ED-B", "Edificio B"), ("ED-B-P1", "Edificio B, Planta 1"),
            ("ED-B-P1-LAB", "Edificio B, Planta 1, Laboratorio de redes"),
            ("ED-B-P1-LAB-R1", "Edificio B, Planta 1, Laboratorio de redes, Rack de comunicaciones"),
            ("ED-B-P2-OF", "Edificio B, Planta 2, Oficina de docencia"),
        ]);

    private static byte[] MessyExcel()
    {
        using var book = new XLWorkbook();
        var ws = book.Worksheets.Add("Resguardos");
        ws.Cell(1, 1).Value = "INSTITUTO TECNOLÓGICO — CENTRO DE CÓMPUTO";
        ws.Cell(3, 1).Value = "No."; ws.Cell(3, 2).Value = "Clave Inv."; ws.Cell(3, 3).Value = "Descripción"; ws.Cell(3, 4).Value = "No. de Serie";
        ws.Cell(4, 1).Value = "LABORATORIO DE REDES — EDIFICIO B, PLANTA 1";
        ws.Cell(5, 1).Value = 1; ws.Cell(5, 2).Value = "TEC-1"; ws.Cell(5, 3).Value = "Switch Cisco Catalyst 2960"; ws.Cell(5, 4).Value = "FOC123";
        ws.Cell(6, 1).Value = 2; ws.Cell(6, 2).Value = "TEC-2"; ws.Cell(6, 3).Value = "Proyector BenQ MS560"; ws.Cell(6, 4).Value = "PDK999";
        var other = book.Worksheets.Add("Vacía");
        other.Cell(1, 1).Value = "sin datos";
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    [Fact]
    public void Excel_se_lee_por_hoja_con_las_celdas_etiquetadas_por_su_encabezado()
    {
        var doc = SourceReader.Read(new ImportFile("inventario.xlsx", MessyExcel()));

        Assert.Equal(["Resguardos", "Vacía"], doc.Blocks.Select(b => b.Name));
        var block = doc.Blocks[0];
        Assert.True(block.RowBased);
        Assert.Contains(block.Lines, l => l.IsHeader && l.Origin == "Resguardos F3");
        var row = block.Lines.Single(l => l.Origin == "Resguardos F5");
        Assert.Equal("TEC-1", row.Cells!["Clave Inv."]);
        Assert.Contains("No. de Serie=\"FOC123\"", row.Text);
    }

    [Fact]
    public void Pdf_de_texto_da_lineas_y_pdf_escaneado_da_la_imagen_jpeg()
    {
        var jpeg = Convert.FromBase64String(TinyJpeg);
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var textPage = builder.AddPage(PageSize.Letter);
        textPage.AddText("1. Laptop Dell Latitude 3420, inventario TEC-0201, serie 4HQ7MN3.", 11, new PdfPoint(50, 700), font);
        var scannedPage = builder.AddPage(PageSize.Letter);
        scannedPage.AddJpeg(jpeg, new PdfRectangle(0, 0, 600, 780));

        var doc = SourceReader.Read(new ImportFile("oficio.pdf", builder.Build()));

        var block = Assert.Single(doc.Blocks);
        Assert.False(block.RowBased);
        Assert.Contains(block.Lines, l => l.Origin == "p1 L1" && l.Text.Contains("TEC-0201"));
        var image = Assert.Single(doc.Images);
        Assert.Equal(2, image.Page);
        Assert.Equal("image/jpeg", image.MimeType);
        Assert.Equal(jpeg, image.Content);
    }

    [Fact]
    public void Reglas_corrigen_columnas_quitan_datos_inventados_y_ubican_por_el_titulo()
    {
        var block = SourceReader.Read(new ImportFile("inventario.xlsx", MessyExcel())).Blocks[0];
        // El modelo tomó la serie como código, inventó el modelo, puso el título como hostname y dijo ROUTER.
        var asset = new ExtractedAsset
        {
            Code = "FOC123", Serial = "FOC123", Type = "ROUTER", Manufacturer = "Cisco",
            Model = "Catalyst 9300", Hostname = "LABORATORIO DE REDES",
        };

        Assert.True(ExtractionRules.Apply(asset, block, Catalog));

        Assert.Equal("Resguardos F5", asset.Origin);
        Assert.Equal("TEC-1", asset.Code);
        Assert.Equal("FOC123", asset.Serial);
        Assert.Equal("", asset.Model);
        Assert.Equal("", asset.Hostname);
        Assert.Equal("SWITCH", asset.Type);
        Assert.Equal("ED-B-P1-LAB", asset.Location);
        Assert.Contains(asset.Warnings, w => w.StartsWith("Código corregido"));
        Assert.Contains(asset.Warnings, w => w.Contains("«Catalyst 9300» no aparece"));
        Assert.Contains(asset.Warnings, w => w.StartsWith("Ubicación tomada del título"));
    }

    [Fact]
    public void En_texto_corrido_el_equipo_puede_ocupar_dos_lineas_y_la_ubicacion_sale_de_su_parrafo()
    {
        var block = SourceReader.FromText(1, """
            Entrega a la Oficina de docencia, Edificio B, planta 2:
            1. Una laptop marca Dell modelo Latitude 3420, con inventario
               TEC-0201 y serie 4HQ7MN3.
            El punto de acceso con serie F09 e inventario TEC-0204 queda en el
            Rack de comunicaciones del laboratorio de redes.
            """);
        var laptop = new ExtractedAsset { Code = "TEC-0201", Serial = "4HQ7MN3", Type = "LAPTOP", Manufacturer = "Dell", Model = "Latitude 3420" };
        var ap = new ExtractedAsset { Code = "TEC-0204", Serial = "F09", Type = "LAPTOP" };
        var ghost = new ExtractedAsset { Code = "TEC-9999", Type = "PC" };

        Assert.True(ExtractionRules.Apply(laptop, block, Catalog));
        Assert.True(ExtractionRules.Apply(ap, block, Catalog));
        Assert.False(ExtractionRules.Apply(ghost, block, Catalog)); // no está en el texto: se descarta

        Assert.Equal("p1 L3", laptop.Origin);
        Assert.Equal("Dell", laptop.Manufacturer); // venía en la línea anterior
        Assert.Equal("ED-B-P2-OF", laptop.Location);
        Assert.Equal("ED-B-P1-LAB-R1", ap.Location);
    }

    [Fact]
    public void Cola_un_trabajo_activo_por_usuario_y_solo_el_dueno_lo_ve()
    {
        var jobs = new AiImportJobs(TimeProvider.System);
        var file = new ImportFile("a.xlsx", [1]);

        var job = jobs.Enqueue(1, file);
        Assert.Throws<BusinessRuleViolationException>(() => jobs.Enqueue(1, file));
        jobs.Enqueue(2, file);

        Assert.Same(job, jobs.Get(job.Id, 1));
        Assert.Throws<KeyNotFoundException>(() => jobs.Get(job.Id, 2));
        Assert.Equal(AiJobStatus.Queued, job.Snapshot().Status);
    }

    [Fact]
    public async Task Proceso_completo_con_ia_simulada_genera_la_plantilla_con_origen_y_revisar()
    {
        await using var context = TestDb.Create();
        Seed(context);
        var ai = new FakeAi("""
            {"activos":[
              {"codigo_inventario":"TEC-1","tipo":"SWITCH","fabricante":"Cisco","modelo":"Catalyst 2960","serie":"FOC123","hostname":""},
              {"codigo_inventario":"PDK999","tipo":"PROJECTOR","fabricante":"BenQ","modelo":"MS560","serie":"PDK999","hostname":""},
              {"codigo_inventario":"TEC-404","tipo":"PC","fabricante":"HP","modelo":"","serie":"","hostname":""}
            ]}
            """);
        var job = new AiImportJob(1, new ImportFile("inventario.xlsx", MessyExcel()), DateTime.UtcNow);

        await new AiImportProcessor(ai, context, new AssetImportChecker(context)).ProcessAsync(job, CancellationToken.None);

        var snap = job.Snapshot();
        Assert.Equal(AiJobStatus.Done, snap.Status);
        Assert.Equal(2, snap.Found); // TEC-404 no está en el archivo
        Assert.Null(job.File);
        Assert.Contains("SWITCH (Switch de Red)", ai.LastSystem);

        using var book = new XLWorkbook(new MemoryStream(job.Result!.Content));
        var sheet = book.Worksheet("Activos");
        Assert.Equal("origen", sheet.Cell(1, 9).GetString());
        Assert.Equal("revisar", sheet.Cell(1, 10).GetString());
        Assert.Equal("TEC-1", sheet.Cell(2, 1).GetString());
        Assert.Equal("ED-B-P1-LAB", sheet.Cell(2, 7).GetString());
        Assert.Equal("TEC-2", sheet.Cell(3, 1).GetString());
        Assert.Contains("Código corregido", sheet.Cell(3, 10).GetString());
        // El checker de la carga masiva ya avisa lo que fallará al subir (fabricantes fuera del catálogo).
        Assert.Contains("El fabricante \"Cisco\" no existe.", sheet.Cell(2, 10).GetString());
        Assert.True(book.Worksheets.Contains("Catálogos"));

        // Y la plantilla generada se puede subir tal cual: el parser ignora origen/revisar.
        var parsed = AssetImportParser.Parse(new ImportFile("x.xlsx", job.Result.Content));
        Assert.Equal(2, parsed.Count);
        Assert.Equal("TEC-2", parsed[1][AssetImportParser.Code]);
    }

    [Fact]
    public async Task Cliente_reusa_el_mismo_esquema_en_varios_bloques_y_respeta_la_configuracion()
    {
        var handler = new StubHandler("""{"choices":[{"message":{"content":"{\"activos\":[]}"}}]}""");
        var options = Microsoft.Extensions.Options.Options.Create(new AiOptions { BaseUrl = "http://ia:8080/" });
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var client = new LocalAiClient(new HttpClient(handler), options, cache);
        var schema = AiPrompt.Schema(Catalog);

        await client.CompleteJsonAsync("s", "bloque 1", schema, CancellationToken.None);
        var second = await client.CompleteJsonAsync("s", "bloque 2", schema, CancellationToken.None);

        Assert.Equal(2, handler.Calls);
        Assert.Empty(second["activos"]!.AsArray());
        Assert.Equal("http://ia:8080/v1/chat/completions", handler.LastUri);

        var off = new LocalAiClient(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(new AiOptions()), cache);
        Assert.False(await off.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Chat_devuelve_las_herramientas_que_pidio_el_modelo_sin_tocar_el_historial()
    {
        var handler = new StubHandler("""
            {"choices":[{"message":{"role":"assistant","content":"","tool_calls":[
              {"id":"c1","type":"function","function":{"name":"buscar_activos","arguments":"{\"tipo\":\"LAPTOP\"}"}},
              {"id":"c2","type":"function","function":{"name":"buscar_incidentes","arguments":"{}"}}]}}]}
            """);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var client = new LocalAiClient(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new AiOptions { BaseUrl = "http://ia:8080/" }), cache);
        var history = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "laptops" });
        var tools = new JsonArray(new JsonObject { ["type"] = "function" });

        var reply = await client.ChatAsync(history, tools, CancellationToken.None);
        await client.ChatAsync(history, tools, CancellationToken.None); // mismo historial dos veces: no se "adopta"

        Assert.Null(reply.Content);
        Assert.Equal(["buscar_activos", "buscar_incidentes"], reply.ToolCalls.Select(c => c.Name));
        Assert.Equal("{\"tipo\":\"LAPTOP\"}", reply.ToolCalls[0].Arguments);
        Assert.Equal("c1", reply.ToolCalls[0].Id);
        Assert.Single(history);
        Assert.NotNull(reply.Message["tool_calls"]);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string LastUri { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastUri = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private static void Seed(TecnmInventarioDbContext context)
    {
        foreach (var (code, name) in Catalog.Types)
            context.AssetTypes.Add(new AssetType { Code = code, Name = name });
        context.Locations.Add(new Location { LocationId = 1, Code = "ED-B", Name = "Edificio B" });
        context.Locations.Add(new Location { LocationId = 2, Code = "ED-B-P1", Name = "Planta 1", ParentLocationId = 1 });
        context.Locations.Add(new Location { LocationId = 3, Code = "ED-B-P1-LAB", Name = "Laboratorio de redes", ParentLocationId = 2 });
        context.SaveChanges();
    }

    private sealed class FakeAi(string json) : ILocalAiClient
    {
        public string LastSystem { get; private set; } = "";
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<JsonNode> CompleteJsonAsync(string system, string user, JsonObject schema, CancellationToken ct)
        {
            LastSystem = system;
            return Task.FromResult(JsonNode.Parse(json)!);
        }

        public Task<string> TranscribeAsync(byte[] image, string mimeType, CancellationToken ct) => Task.FromResult("");

        public Task<AiChatReply> ChatAsync(JsonArray messages, JsonArray? tools, CancellationToken ct) =>
            throw new NotSupportedException("La carga masiva no conversa.");
    }

    private const string TinyJpeg =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAIAAgDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==";
}
