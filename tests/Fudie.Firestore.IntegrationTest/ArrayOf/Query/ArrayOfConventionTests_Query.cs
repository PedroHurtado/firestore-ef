using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para verificar que ArrayOfConvention auto-detecta propiedades List&lt;T&gt;.
/// DESERIALIZACIÓN con LINQ.
/// Patrón: Guardar SIN configuración explícita → Leer con LINQ → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfConventionTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfConventionTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_ArrayOfEmbedded_Query()
    {
        // Arrange
        var oficinaId = FirestoreTestFixture.GenerateId("oficina");
        using var context = _fixture.CreateContext<ArrayOfConventionTestDbContext>();

        var oficina = new Oficina
        {
            Id = oficinaId,
            Nombre = "Oficina Central",
            Direcciones =
            [
                new DireccionOficina { Calle = "Gran Vía 123", Ciudad = "Madrid", CodigoPostal = "28013" },
                new DireccionOficina { Calle = "Diagonal 456", Ciudad = "Barcelona", CodigoPostal = "08029" }
            ],
            Ubicaciones = []
        };

        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfConventionTestDbContext>();
        var result = await readContext.Oficinas
            .FirstOrDefaultAsync(o => o.Id == oficinaId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Oficina Central");
        result.Direcciones.Should().HaveCount(2);
        result.Direcciones[0].Calle.Should().Be("Gran Vía 123");
        result.Direcciones[0].Ciudad.Should().Be("Madrid");
        result.Direcciones[1].Calle.Should().Be("Diagonal 456");
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_ArrayOfGeoPoint_Query()
    {
        // Arrange
        var oficinaId = FirestoreTestFixture.GenerateId("oficina");
        using var context = _fixture.CreateContext<ArrayOfConventionTestDbContext>();

        var oficina = new Oficina
        {
            Id = oficinaId,
            Nombre = "Oficina Regional",
            Direcciones = [],
            Ubicaciones =
            [
                new PuntoGeo { Latitude = 40.4168, Longitude = -3.7038 },
                new PuntoGeo { Latitude = 41.3851, Longitude = 2.1734 }
            ]
        };

        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfConventionTestDbContext>();
        var result = await readContext.Oficinas
            .FirstOrDefaultAsync(o => o.Id == oficinaId);

        // Assert
        result.Should().NotBeNull();
        result!.Ubicaciones.Should().HaveCount(2);
        result.Ubicaciones[0].Latitude.Should().BeApproximately(40.4168, 0.0001);
        result.Ubicaciones[0].Longitude.Should().BeApproximately(-3.7038, 0.0001);
        result.Ubicaciones[1].Latitude.Should().BeApproximately(41.3851, 0.0001);
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_BothTypesInSameEntity_Query()
    {
        // Arrange
        var oficinaId = FirestoreTestFixture.GenerateId("oficina");
        using var context = _fixture.CreateContext<ArrayOfConventionTestDbContext>();

        var oficina = new Oficina
        {
            Id = oficinaId,
            Nombre = "Oficina Mixta",
            Direcciones =
            [
                new DireccionOficina { Calle = "Calle Mayor 1", Ciudad = "Valencia", CodigoPostal = "46001" }
            ],
            Ubicaciones =
            [
                new PuntoGeo { Latitude = 39.4699, Longitude = -0.3763 }
            ]
        };

        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfConventionTestDbContext>();
        var result = await readContext.Oficinas
            .FirstOrDefaultAsync(o => o.Id == oficinaId);

        // Assert
        result.Should().NotBeNull();

        // Direcciones
        result!.Direcciones.Should().HaveCount(1);
        result.Direcciones[0].Ciudad.Should().Be("Valencia");

        // Ubicaciones
        result.Ubicaciones.Should().HaveCount(1);
        result.Ubicaciones[0].Latitude.Should().BeApproximately(39.4699, 0.0001);
    }
}
