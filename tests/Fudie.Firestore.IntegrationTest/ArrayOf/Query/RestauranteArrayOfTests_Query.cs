using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para los 5 casos de ArrayOf - DESERIALIZACIÓN con LINQ.
/// Patrón: Guardar con EF Core → Leer con LINQ (Include para References) → Verificar estructura.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class RestauranteArrayOfTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public RestauranteArrayOfTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region CASO 1: ArrayOf Embedded Simple

    [Fact]
    public async Task Caso1_ArrayOfEmbedded_ShouldDeserializeAsListOfObjects()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        using var context = _fixture.CreateContext<RestauranteTestDbContext>();

        var restaurante = new Restaurante
        {
            Id = restauranteId,
            Nombre = "La Tasca",
            Horarios =
            [
                new Horario { Dia = "Lunes", Apertura = TimeSpan.FromHours(9), Cierre = TimeSpan.FromHours(22) },
                new Horario { Dia = "Martes", Apertura = TimeSpan.FromHours(9), Cierre = TimeSpan.FromHours(22) }
            ]
        };

        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<RestauranteTestDbContext>();
        var result = await readContext.Restaurantes
            .FirstOrDefaultAsync(r => r.Id == restauranteId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("La Tasca");
        result.Horarios.Should().HaveCount(2);
        result.Horarios[0].Dia.Should().Be("Lunes");
        result.Horarios[0].Apertura.Should().Be(TimeSpan.FromHours(9));
        result.Horarios[1].Dia.Should().Be("Martes");
    }

    #endregion

    #region CASO 2: ArrayOf GeoPoints

    [Fact]
    public async Task Caso2_ArrayOfGeoPoints_ShouldDeserializeAsListOfCoordinates()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        using var context = _fixture.CreateContext<RestauranteTestDbContext>();

        var restaurante = new Restaurante
        {
            Id = restauranteId,
            Nombre = "La Tasca",
            ZonasCobertura =
            [
                new Coordenada { Latitude = 40.4168, Longitude = -3.7038 },
                new Coordenada { Latitude = 40.4200, Longitude = -3.7100 }
            ]
        };

        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<RestauranteTestDbContext>();
        var result = await readContext.Restaurantes
            .FirstOrDefaultAsync(r => r.Id == restauranteId);

        // Assert
        result.Should().NotBeNull();
        result!.ZonasCobertura.Should().HaveCount(2);
        result.ZonasCobertura[0].Latitude.Should().BeApproximately(40.4168, 0.0001);
        result.ZonasCobertura[0].Longitude.Should().BeApproximately(-3.7038, 0.0001);
        result.ZonasCobertura[1].Latitude.Should().BeApproximately(40.4200, 0.0001);
    }

    #endregion

    #region CASO 3: ArrayOf References

    [Fact]
    public async Task Caso3_ArrayOfReferences_ShouldDeserializeWithInclude()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        var cat1Id = FirestoreTestFixture.GenerateId("cat");
        var cat2Id = FirestoreTestFixture.GenerateId("cat");
        using var context = _fixture.CreateContext<RestauranteTestDbContext>();

        var categoria1 = new CategoriaRestaurante { Id = cat1Id, Nombre = "Italiana" };
        var categoria2 = new CategoriaRestaurante { Id = cat2Id, Nombre = "Mediterránea" };
        context.Categorias.AddRange(categoria1, categoria2);

        var restaurante = new Restaurante
        {
            Id = restauranteId,
            Nombre = "La Tasca",
            Categorias = [categoria1, categoria2]
        };

        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include
        using var readContext = _fixture.CreateContext<RestauranteTestDbContext>();
        var result = await readContext.Restaurantes
            .Include(r => r.Categorias)
            .FirstOrDefaultAsync(r => r.Id == restauranteId);

        // Assert
        result.Should().NotBeNull();
        result!.Categorias.Should().HaveCount(2);
        result.Categorias[0].Id.Should().Be(cat1Id);
        result.Categorias[0].Nombre.Should().Be("Italiana");
        result.Categorias[1].Id.Should().Be(cat2Id);
        result.Categorias[1].Nombre.Should().Be("Mediterránea");
    }

    #endregion

    #region CASO 4: ArrayOf Embedded con Reference

    [Fact]
    public async Task Caso4_ArrayOfEmbeddedWithReference_ShouldDeserializeWithNestedInclude()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        var certId = FirestoreTestFixture.GenerateId("cert");
        using var context = _fixture.CreateContext<RestauranteTestDbContext>();

        var certificador = new Certificador { Id = certId, Nombre = "Bureau Veritas", Pais = "Francia" };
        context.Certificadores.Add(certificador);

        var restaurante = new Restaurante
        {
            Id = restauranteId,
            Nombre = "La Tasca",
            Certificaciones =
            [
                new Certificacion
                {
                    Nombre = "ISO 9001",
                    FechaObtencion = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    Certificador = certificador
                }
            ]
        };

        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para la referencia dentro del embedded
        using var readContext = _fixture.CreateContext<RestauranteTestDbContext>();
        var result = await readContext.Restaurantes
            .Include(r => r.Certificaciones)
            .ThenInclude(c => c.Certificador)
            .FirstOrDefaultAsync(r => r.Id == restauranteId);

        // Assert
        result.Should().NotBeNull();
        result!.Certificaciones.Should().HaveCount(1);
        result.Certificaciones[0].Nombre.Should().Be("ISO 9001");
        result.Certificaciones[0].Certificador.Should().NotBeNull();
        result.Certificaciones[0].Certificador!.Id.Should().Be(certId);
        result.Certificaciones[0].Certificador!.Nombre.Should().Be("Bureau Veritas");
        result.Certificaciones[0].Certificador!.Pais.Should().Be("Francia");
    }

    #endregion

    #region CASO 5: ArrayOf Embedded Anidado con Reference

    [Fact]
    public async Task Caso5_ArrayOfNestedEmbeddedWithReference_ShouldDeserializeNestedStructure()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        var platoId = FirestoreTestFixture.GenerateId("plato");
        using var context = _fixture.CreateContext<RestauranteTestDbContext>();

        var plato = new Plato { Id = platoId, Nombre = "Patatas Bravas", Precio = 8.50m };
        context.Platos.Add(plato);

        var restaurante = new Restaurante
        {
            Id = restauranteId,
            Nombre = "La Tasca",
            Menus =
            [
                new Menu
                {
                    Nombre = "Carta Principal",
                    Secciones =
                    [
                        new SeccionMenu
                        {
                            Titulo = "Entrantes",
                            Items =
                            [
                                new ItemMenu
                                {
                                    Descripcion = "Ración completa",
                                    Plato = plato
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include anidado
        using var readContext = _fixture.CreateContext<RestauranteTestDbContext>();
        var result = await readContext.Restaurantes
            .Include(r => r.Menus)
            .ThenInclude(m => m.Secciones)
            .ThenInclude(s => s.Items)
            .ThenInclude(i => i.Plato)
            .FirstOrDefaultAsync(r => r.Id == restauranteId);

        // Assert
        result.Should().NotBeNull();
        result!.Menus.Should().HaveCount(1);
        result.Menus[0].Nombre.Should().Be("Carta Principal");
        result.Menus[0].Secciones.Should().HaveCount(1);
        result.Menus[0].Secciones[0].Titulo.Should().Be("Entrantes");
        result.Menus[0].Secciones[0].Items.Should().HaveCount(1);
        result.Menus[0].Secciones[0].Items[0].Descripcion.Should().Be("Ración completa");
        result.Menus[0].Secciones[0].Items[0].Plato.Should().NotBeNull();
        result.Menus[0].Secciones[0].Items[0].Plato!.Id.Should().Be(platoId);
        result.Menus[0].Secciones[0].Items[0].Plato!.Nombre.Should().Be("Patatas Bravas");
        result.Menus[0].Secciones[0].Items[0].Plato!.Precio.Should().Be(8.50m);
    }

    #endregion
}
