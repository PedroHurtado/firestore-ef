using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para verificar que ArrayOf se deserializa correctamente.
/// DESERIALIZACIÓN con LINQ.
/// Patrón: Guardar con EF Core → Leer con LINQ → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfSerializationTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfSerializationTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deserialization_ArrayOfEmbedded_ShouldRestoreAsListOfObjects()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Centro",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Sábado", Apertura = "10:00", Cierre = "14:00" }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfTestDbContext>();
        var result = await readContext.Tiendas
            .FirstOrDefaultAsync(t => t.Id == tiendaId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Tienda Centro");
        result.Horarios.Should().HaveCount(3);
        result.Horarios[0].Dia.Should().Be("Lunes");
        result.Horarios[0].Apertura.Should().Be("09:00");
        result.Horarios[0].Cierre.Should().Be("18:00");
        result.Horarios[2].Dia.Should().Be("Sábado");
    }

    [Fact]
    public async Task Deserialization_ArrayOfGeoPoints_ShouldRestoreAsListOfCoordinates()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfGeoPointTestDbContext>();

        var tienda = new TiendaConUbicaciones
        {
            Id = tiendaId,
            Nombre = "Tienda Multi-Sucursal",
            Ubicaciones =
            [
                new UbicacionGeo { Latitude = 40.4168, Longitude = -3.7038 },
                new UbicacionGeo { Latitude = 41.3851, Longitude = 2.1734 },
                new UbicacionGeo { Latitude = 37.3891, Longitude = -5.9845 }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfGeoPointTestDbContext>();
        var result = await readContext.Tiendas
            .FirstOrDefaultAsync(t => t.Id == tiendaId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Tienda Multi-Sucursal");
        result.Ubicaciones.Should().HaveCount(3);
        result.Ubicaciones[0].Latitude.Should().BeApproximately(40.4168, 0.0001);
        result.Ubicaciones[0].Longitude.Should().BeApproximately(-3.7038, 0.0001);
        result.Ubicaciones[1].Latitude.Should().BeApproximately(41.3851, 0.0001);
    }

    [Fact]
    public async Task Deserialization_ArrayOfEmbedded_EmptyList_ShouldRestoreEmptyList()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Sin Horarios",
            Horarios = []
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfTestDbContext>();
        var result = await readContext.Tiendas
            .FirstOrDefaultAsync(t => t.Id == tiendaId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Tienda Sin Horarios");
        result.Horarios.Should().BeEmpty();
    }

    [Fact]
    public async Task Deserialization_ArrayOfReferences_ShouldRestoreWithInclude()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        var tag1Id = FirestoreTestFixture.GenerateId("tag");
        var tag2Id = FirestoreTestFixture.GenerateId("tag");
        var tag3Id = FirestoreTestFixture.GenerateId("tag");

        using var context = _fixture.CreateContext<ArrayOfReferencesTestDbContext>();

        var tag1 = new Etiqueta { Id = tag1Id, Nombre = "Electrónica" };
        var tag2 = new Etiqueta { Id = tag2Id, Nombre = "Oferta" };
        var tag3 = new Etiqueta { Id = tag3Id, Nombre = "Nuevo" };
        context.Etiquetas.AddRange(tag1, tag2, tag3);

        var producto = new ProductoConEtiquetas
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Etiquetas = [tag1, tag2, tag3]
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include
        using var readContext = _fixture.CreateContext<ArrayOfReferencesTestDbContext>();
        var result = await readContext.Productos
            .Include(p => p.Etiquetas)
            .FirstOrDefaultAsync(p => p.Id == productoId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Laptop Gaming");
        result.Etiquetas.Should().HaveCount(3);
        result.Etiquetas[0].Id.Should().Be(tag1Id);
        result.Etiquetas[0].Nombre.Should().Be("Electrónica");
        result.Etiquetas[1].Id.Should().Be(tag2Id);
        result.Etiquetas[2].Id.Should().Be(tag3Id);
    }
}
