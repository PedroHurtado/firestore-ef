using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para verificar que ArrayOfConvention auto-detecta propiedades List&lt;T&gt;.
/// Patrón: Guardar SIN configuración explícita → Leer con SDK de Google → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_ArrayOfEmbedded()
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
            Ubicaciones = [] // Vacío para este test
        };

        // Act - Sin configuración explícita de ArrayOf
        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Assert - Verificar que se guardó como array de maps
        var rawData = await GetDocumentRawData<Oficina>(oficinaId);
        rawData.Should().ContainKey("Direcciones");

        var direcciones = ((IEnumerable<object>)rawData["Direcciones"]).ToList();
        direcciones.Should().HaveCount(2);

        var primeraDireccion = direcciones[0] as Dictionary<string, object>;
        primeraDireccion!["Calle"].Should().Be("Gran Vía 123");
        primeraDireccion["Ciudad"].Should().Be("Madrid");
        primeraDireccion["CodigoPostal"].Should().Be("28013");
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_ArrayOfGeoPoint()
    {
        // Arrange
        var oficinaId = FirestoreTestFixture.GenerateId("oficina");
        using var context = _fixture.CreateContext<ArrayOfConventionTestDbContext>();

        var oficina = new Oficina
        {
            Id = oficinaId,
            Nombre = "Oficina Regional",
            Direcciones = [], // Vacío para este test
            Ubicaciones =
            [
                new PuntoGeo { Latitude = 40.4168, Longitude = -3.7038 },
                new PuntoGeo { Latitude = 41.3851, Longitude = 2.1734 }
            ]
        };

        // Act - Sin configuración explícita de ArrayOf
        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Assert - Verificar que se guardó como array de GeoPoints nativos
        var rawData = await GetDocumentRawData<Oficina>(oficinaId);
        rawData.Should().ContainKey("Ubicaciones");

        var ubicaciones = ((IEnumerable<object>)rawData["Ubicaciones"]).ToList();
        ubicaciones.Should().HaveCount(2);
        ubicaciones[0].Should().BeOfType<GeoPoint>();

        var geoPoint = (GeoPoint)ubicaciones[0];
        geoPoint.Latitude.Should().BeApproximately(40.4168, 0.0001);
        geoPoint.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    [Fact]
    public async Task Convention_ShouldAutoDetect_BothTypesInSameEntity()
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

        // Act - Sin configuración explícita de ArrayOf
        context.Oficinas.Add(oficina);
        await context.SaveChangesAsync();

        // Assert - Verificar ambos tipos
        var rawData = await GetDocumentRawData<Oficina>(oficinaId);

        // Direcciones como maps
        rawData.Should().ContainKey("Direcciones");
        var direcciones = ((IEnumerable<object>)rawData["Direcciones"]).ToList();
        direcciones.Should().HaveCount(1);
        var direccion = direcciones[0] as Dictionary<string, object>;
        direccion!["Ciudad"].Should().Be("Valencia");

        // Ubicaciones como GeoPoints
        rawData.Should().ContainKey("Ubicaciones");
        var ubicaciones = ((IEnumerable<object>)rawData["Ubicaciones"]).ToList();
        ubicaciones.Should().HaveCount(1);
        ubicaciones[0].Should().BeOfType<GeoPoint>();
    }

    #region Helpers

    private async Task<Dictionary<string, object>> GetDocumentRawData<T>(string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var collectionName = GetCollectionName<T>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue($"El documento {documentId} debe existir");
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
