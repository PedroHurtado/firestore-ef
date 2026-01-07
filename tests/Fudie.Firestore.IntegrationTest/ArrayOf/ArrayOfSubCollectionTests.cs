using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para ArrayOf dentro de SubCollections.
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura.
/// La deserialización con LINQ no está implementada aún.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfSubCollectionTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfSubCollectionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region ArrayOf Embedded en SubCollection

    [Fact]
    public async Task SubCollection_WithArrayOfEmbedded_ShouldPersistAsArrayOfMaps()
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

        // Act
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert - Verificar estructura en Firestore con SDK
        var rawData = await GetSubCollectionDocumentRawData<Empresa, Sucursal>(empresaId, sucursalId);
        rawData.Should().ContainKey("Horarios");

        var horarios = ((IEnumerable<object>)rawData["Horarios"]).ToList();
        horarios.Should().HaveCount(3);

        var primerHorario = horarios[0] as Dictionary<string, object>;
        primerHorario!["Dia"].Should().Be("Lunes");
        primerHorario["Apertura"].Should().Be("09:00");
        primerHorario["Cierre"].Should().Be("18:00");

        var tercerHorario = horarios[2] as Dictionary<string, object>;
        tercerHorario!["Dia"].Should().Be("Sábado");
        tercerHorario["Apertura"].Should().Be("10:00");
    }

    #endregion

    #region ArrayOf GeoPoint en SubCollection

    [Fact]
    public async Task SubCollection_WithArrayOfGeoPoint_ShouldPersistAsNativeGeoPoints()
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

        // Act
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert - Verificar estructura en Firestore con SDK
        var rawData = await GetSubCollectionDocumentRawData<Empresa, RutaDistribucion>(empresaId, rutaId);
        rawData.Should().ContainKey("Waypoints");

        var waypoints = ((IEnumerable<object>)rawData["Waypoints"]).ToList();
        waypoints.Should().HaveCount(3);
        waypoints[0].Should().BeOfType<GeoPoint>();

        var primerPunto = (GeoPoint)waypoints[0];
        primerPunto.Latitude.Should().BeApproximately(-12.0464, 0.0001);
        primerPunto.Longitude.Should().BeApproximately(-77.0428, 0.0001);

        var tercerPunto = (GeoPoint)waypoints[2];
        tercerPunto.Latitude.Should().BeApproximately(-11.8800, 0.0001);
        tercerPunto.Longitude.Should().BeApproximately(-77.1000, 0.0001);
    }

    #endregion

    #region Multiple SubCollections con ArrayOf

    [Fact]
    public async Task MultipleSubCollections_WithBothArrayOfTypes_ShouldPersistCorrectly()
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

        // Act
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert - Verificar Sucursal A con 1 horario
        var sucursalARaw = await GetSubCollectionDocumentRawData<Empresa, Sucursal>(empresaId, sucursalAId);
        sucursalARaw["Nombre"].Should().Be("Sucursal A");
        var horariosA = ((IEnumerable<object>)sucursalARaw["Horarios"]).ToList();
        horariosA.Should().HaveCount(1);

        // Assert - Verificar Sucursal B con 2 horarios
        var sucursalBRaw = await GetSubCollectionDocumentRawData<Empresa, Sucursal>(empresaId, sucursalBId);
        sucursalBRaw["Nombre"].Should().Be("Sucursal B");
        var horariosB = ((IEnumerable<object>)sucursalBRaw["Horarios"]).ToList();
        horariosB.Should().HaveCount(2);

        // Assert - Verificar Ruta con GeoPoints
        var rutaRaw = await GetSubCollectionDocumentRawData<Empresa, RutaDistribucion>(empresaId, rutaId);
        rutaRaw["Codigo"].Should().Be("RUTA-A");
        var waypoints = ((IEnumerable<object>)rutaRaw["Waypoints"]).ToList();
        waypoints.Should().HaveCount(2);
        waypoints[0].Should().BeOfType<GeoPoint>();
    }

    #endregion

    #region Helpers

    private async Task<Dictionary<string, object>> GetSubCollectionDocumentRawData<TParent, TChild>(
        string parentId, string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var parentCollection = GetCollectionName<TParent>();
        var childCollection = GetCollectionName<TChild>();

        // Path: {ParentCollection}/{parentId}/{ChildCollection}/{documentId}
        var docSnapshot = await firestoreDb
            .Collection(parentCollection)
            .Document(parentId)
            .Collection(childCollection)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue($"El documento {documentId} debe existir en {childCollection}");
        return docSnapshot.ToDictionary();
    }

#pragma warning disable EF1001
    private static string GetCollectionName<T>()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001

    #endregion
}
