using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para ArrayOf dentro de SubCollections - DESERIALIZACIÓN con LINQ.
/// Patrón: Guardar con EF Core → Leer con LINQ (Include para SubCollections) → Verificar estructura.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfSubCollectionTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfSubCollectionTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region ArrayOf Embedded en SubCollection

    [Fact]
    public async Task SubCollection_WithArrayOfEmbedded_ShouldDeserializeAsListOfObjects()
    {
        // Arrange
        var empresaId = FirestoreTestFixture.GenerateId("empresa");
        var sucursalId = FirestoreTestFixture.GenerateId("sucursal");
        using var context = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();

        var empresa = new Empresa
        {
            Id = empresaId,
            RazonSocial = "Empresa Test S.A.",
            Ruc = "20123456789",
            Sucursales =
            [
                new Sucursal
                {
                    Id = sucursalId,
                    Nombre = "Sucursal Central",
                    Direccion = "Av. Principal 123",
                    Horarios =
                    [
                        new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" },
                        new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" },
                        new HorarioAtencion { Dia = "Sábado", Apertura = "10:00", Cierre = "14:00" }
                    ]
                }
            ],
            Rutas = []
        };

        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para SubCollection
        using var readContext = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();
        var result = await readContext.Empresas
            .Include(e => e.Sucursales)
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        // Assert
        result.Should().NotBeNull();
        result!.RazonSocial.Should().Be("Empresa Test S.A.");
        result.Sucursales.Should().HaveCount(1);
        result.Sucursales[0].Nombre.Should().Be("Sucursal Central");
        result.Sucursales[0].Horarios.Should().HaveCount(3);
        result.Sucursales[0].Horarios[0].Dia.Should().Be("Lunes");
        result.Sucursales[0].Horarios[0].Apertura.Should().Be("09:00");
        result.Sucursales[0].Horarios[2].Dia.Should().Be("Sábado");
        result.Sucursales[0].Horarios[2].Apertura.Should().Be("10:00");
    }

    #endregion

    #region ArrayOf GeoPoint en SubCollection

    [Fact]
    public async Task SubCollection_WithArrayOfGeoPoint_ShouldDeserializeAsListOfCoordinates()
    {
        // Arrange
        var empresaId = FirestoreTestFixture.GenerateId("empresa");
        var rutaId = FirestoreTestFixture.GenerateId("ruta");
        using var context = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();

        var empresa = new Empresa
        {
            Id = empresaId,
            RazonSocial = "Logística Express S.A.",
            Ruc = "20111222333",
            Sucursales = [],
            Rutas =
            [
                new RutaDistribucion
                {
                    Id = rutaId,
                    Codigo = "RUTA-001",
                    Descripcion = "Ruta Centro-Norte",
                    Waypoints =
                    [
                        new PuntoGeo { Latitude = -12.0464, Longitude = -77.0428 },
                        new PuntoGeo { Latitude = -11.9500, Longitude = -77.0700 },
                        new PuntoGeo { Latitude = -11.8800, Longitude = -77.1000 }
                    ]
                }
            ]
        };

        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para SubCollection
        using var readContext = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();
        var result = await readContext.Empresas
            .Include(e => e.Rutas)
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        // Assert
        result.Should().NotBeNull();
        result!.Rutas.Should().HaveCount(1);
        result.Rutas[0].Codigo.Should().Be("RUTA-001");
        result.Rutas[0].Waypoints.Should().HaveCount(3);
        result.Rutas[0].Waypoints[0].Latitude.Should().BeApproximately(-12.0464, 0.0001);
        result.Rutas[0].Waypoints[0].Longitude.Should().BeApproximately(-77.0428, 0.0001);
        result.Rutas[0].Waypoints[2].Latitude.Should().BeApproximately(-11.8800, 0.0001);
    }

    #endregion

    #region Multiple SubCollections con ArrayOf

    [Fact]
    public async Task MultipleSubCollections_WithBothArrayOfTypes_ShouldDeserializeCorrectly()
    {
        // Arrange
        var empresaId = FirestoreTestFixture.GenerateId("empresa");
        var sucursalAId = FirestoreTestFixture.GenerateId("sucursal");
        var sucursalBId = FirestoreTestFixture.GenerateId("sucursal");
        var rutaId = FirestoreTestFixture.GenerateId("ruta");
        using var context = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();

        var empresa = new Empresa
        {
            Id = empresaId,
            RazonSocial = "Empresa Completa S.A.",
            Ruc = "20777888999",
            Sucursales =
            [
                new Sucursal
                {
                    Id = sucursalAId,
                    Nombre = "Sucursal A",
                    Direccion = "Av. A 100",
                    Horarios =
                    [
                        new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" }
                    ]
                },
                new Sucursal
                {
                    Id = sucursalBId,
                    Nombre = "Sucursal B",
                    Direccion = "Av. B 200",
                    Horarios =
                    [
                        new HorarioAtencion { Dia = "Martes", Apertura = "10:00", Cierre = "19:00" },
                        new HorarioAtencion { Dia = "Miércoles", Apertura = "10:00", Cierre = "19:00" }
                    ]
                }
            ],
            Rutas =
            [
                new RutaDistribucion
                {
                    Id = rutaId,
                    Codigo = "RUTA-A",
                    Descripcion = "Ruta para Sucursal A",
                    Waypoints =
                    [
                        new PuntoGeo { Latitude = -12.0500, Longitude = -77.0400 },
                        new PuntoGeo { Latitude = -12.0600, Longitude = -77.0500 }
                    ]
                }
            ]
        };

        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para ambas SubCollections
        using var readContext = _fixture.CreateContext<ArrayOfSubCollectionTestDbContext>();
        var result = await readContext.Empresas
            .Include(e => e.Sucursales)
            .Include(e => e.Rutas)
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        // Assert - Verificar empresa
        result.Should().NotBeNull();
        result!.RazonSocial.Should().Be("Empresa Completa S.A.");

        // Assert - Verificar Sucursales
        result.Sucursales.Should().HaveCount(2);

        var sucursalA = result.Sucursales.First(s => s.Id == sucursalAId);
        sucursalA.Nombre.Should().Be("Sucursal A");
        sucursalA.Horarios.Should().HaveCount(1);

        var sucursalB = result.Sucursales.First(s => s.Id == sucursalBId);
        sucursalB.Nombre.Should().Be("Sucursal B");
        sucursalB.Horarios.Should().HaveCount(2);

        // Assert - Verificar Rutas
        result.Rutas.Should().HaveCount(1);
        result.Rutas[0].Codigo.Should().Be("RUTA-A");
        result.Rutas[0].Waypoints.Should().HaveCount(2);
    }

    #endregion
}
