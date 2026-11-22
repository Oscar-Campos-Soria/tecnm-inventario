using System.Text.Json.Nodes;

namespace TecnmInventario.Api.Tests.Eval.Engine;

/// <summary>Los jueces del Evaluation Engine: si juzgan mal, todo el informe miente.</summary>
public sealed class EvalScoringTests
{
    [Theory]
    [InlineData("hay 3 laptops", 3, true)]
    [InlineData("hay 13 laptops", 3, false)]
    [InlineData("el equipo ZZ-IA-03 esta disponible", 3, false)]
    [InlineData("hay tres laptops", 3, true)]
    [InlineData("no hay tickets abiertos", 0, true)]
    [InlineData("hay una impresora con falla", 1, true)]
    [InlineData("revise un listado de 4 equipos", 1, false)]
    public void Cuenta_numeros(string text, int n, bool expected) => Assert.Equal(expected, Judge.HasNumber(text, n));

    [Theory]
    [InlineData("el equipo zz-ia-06 tiene falla", "ZZ-IA-06", true)]
    [InlineData("el equipo zz-ia-16 tiene falla", "ZZ-IA-06", false)]
    [InlineData("tu reporte del monitor que parpadea sigue abierto", "ZZ IA: monitor parpadea", true)]
    [InlineData("tu reporte sigue abierto", "ZZ IA: monitor parpadea", false)]
    public void Reconoce_valores(string text, string value, bool expected) => Assert.Equal(expected, Judge.HasValue(text, value));

    [Fact]
    public void Verdad_filtra_y_cuenta()
    {
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var root = JsonNode.Parse("""
            {"items":[{"assignedToUsername":"tec","createdAt":"2026-11-20T10:00:00Z","title":"A"},
                      {"assignedToUsername":null,"createdAt":"2026-11-01T10:00:00Z","title":"B"}],"totalCount":40}
            """)!;
        Assert.Equal(40, Judge.Truth(root, JsonNode.Parse("""{"expect":"count"}""")!.AsObject(), now).Count);
        var assigned = Judge.Truth(root, JsonNode.Parse("""{"expect":"values","field":"title","where":{"assignedToUsername":"!null"}}""")!.AsObject(), now);
        Assert.Equal(["A"], assigned.Values);
        Assert.Equal(1, Judge.Truth(root, JsonNode.Parse("""{"expect":"count","where":{"createdAt":">=-7d"}}""")!.AsObject(), now).Count);
        // Sin datos: la respuesta debe decir que no hay.
        var none = new FactTruth("values", 0, [], "any");
        Assert.True(Judge.InText(none, "tu ticket aun no tiene tecnico"));
    }

    [Fact]
    public void Variantes_deterministas_y_distintas()
    {
        const string q = "¿Qué fallas ha tenido la ZZ-IA-06?";
        foreach (var kind in Variants.Kinds)
        {
            var v = Variants.Make(q, kind);
            Assert.Equal(v, Variants.Make(q, kind));
            Assert.NotEqual(Judge.Plain(q), v);
            Assert.Contains("zz-ia-06", v, StringComparison.OrdinalIgnoreCase);
        }
    }
}
