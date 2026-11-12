using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Tests;

/// <summary>RAG del asistente: el manual embebido se carga y la búsqueda trae la sección correcta.</summary>
public class ManualIndexTests
{
    private static readonly ManualIndex Manual = new();

    [Theory]
    [InlineData("¿Cómo doy de baja un equipo?", "Dar de baja un equipo")]
    [InlineData("como asigno una laptop a una persona", "Asignar un activo a una persona")]
    [InlineData("¿Cuál es la diferencia entre un ticket y un incidente?", "Diferencia entre ticket e incidente")]
    [InlineData("¿Cómo registro equipos nuevos?", "Dar de alta activos")]
    [InlineData("imprimir etiquetas con QR", "Etiquetas con código QR")]
    [InlineData("¿qué puede hacer un técnico?", "Roles y qué puede hacer cada uno")]
    [InlineData("cambiar mi contraseña", "Mi perfil y contraseña")]
    [InlineData("¿cómo muevo un equipo a otro laboratorio?", "Mover un activo de ubicación")]
    [InlineData("programar mantenimiento preventivo", "Programar un mantenimiento")]
    [InlineData("conteo físico del inventario", "Levantamiento físico (conteo de inventario)")]
    // Frases tal como las mandó el modelo en la evaluación.
    [InlineData("cómo registrar un equipo nuevo en el sistema", "Dar de alta activos")]
    [InlineData("qué puede hacer un técnico en el sistema", "Roles y qué puede hacer cada uno")]
    [InlineData("cómo dar de baja un equipo", "Dar de baja un equipo")]
    public void La_pregunta_trae_su_seccion_primero(string question, string expectedSection)
    {
        Assert.Equal(expectedSection, Manual.Search(question).First().Title);
    }

    [Fact]
    public void Desde_la_inyeccion_de_dependencias_trae_el_manual_completo()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ManualIndex>(services);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);

        var manual = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ManualIndex>(provider);

        Assert.Equal(Manual.Count, manual.Count); // antes DI elegía el constructor con una lista vacía
    }

    [Fact]
    public void El_manual_embebido_se_carga_completo_y_lo_ajeno_no_trae_nada()
    {
        Assert.True(Manual.Count >= 20, $"secciones: {Manual.Count}");
        Assert.Empty(Manual.Search("receta de pozole"));
    }

    [Fact]
    public void Parse_parte_por_secciones_y_omite_el_titulo_del_archivo()
    {
        var sections = ManualIndex.Parse("x.md", "# Título\nintro suelta\n## Uno\nprimera\nlínea\n## Dos\nsegunda").ToList();

        Assert.Equal([("Uno", "primera línea"), ("Dos", "segunda")], sections.Select(s => (s.Title, s.Text)));
    }
}
