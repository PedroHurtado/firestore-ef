using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para verificar que ArrayOf se serializa correctamente en Firestore.
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfSerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfSerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Serialization_ArrayOfEmbedded_ShouldStoreAsArrayOfMaps()
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

        // Act
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<TiendaConHorarios>(tiendaId);
        rawData.Should().ContainKey("Horarios");

        var horarios = ((IEnumerable<object>)rawData["Horarios"]).ToList();
        horarios.Should().HaveCount(3);

        var primerHorario = horarios[0] as Dictionary<string, object>;
        primerHorario!["Dia"].Should().Be("Lunes");
        primerHorario["Apertura"].Should().Be("09:00");
        primerHorario["Cierre"].Should().Be("18:00");
    }

    [Fact]
    public async Task Serialization_ArrayOfGeoPoints_ShouldStoreAsArrayOfGeoPoints()
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

        // Act
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<TiendaConUbicaciones>(tiendaId);
        rawData.Should().ContainKey("Ubicaciones");

        var ubicaciones = ((IEnumerable<object>)rawData["Ubicaciones"]).ToList();
        ubicaciones.Should().HaveCount(3);
        ubicaciones[0].Should().BeOfType<GeoPoint>();

        var geoPoint = (GeoPoint)ubicaciones[0];
        geoPoint.Latitude.Should().BeApproximately(40.4168, 0.0001);
        geoPoint.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    [Fact]
    public async Task Serialization_ArrayOfEmbedded_EmptyList_ShouldNotBeStored()
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

        // Act
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert - Empty arrays should NOT be stored in Firestore (saves document size)
        var rawData = await GetDocumentRawData<TiendaConHorarios>(tiendaId);
        rawData.Should().NotContainKey("Horarios", "empty arrays should not be stored in Firestore");
    }

    [Fact]
    public async Task Serialization_ArrayOfReferences_ShouldStoreAsArrayOfDocumentReferences()
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

        // Act
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<ProductoConEtiquetas>(productoId);
        rawData.Should().ContainKey("Etiquetas");

        var etiquetas = ((IEnumerable<object>)rawData["Etiquetas"]).ToList();
        etiquetas.Should().HaveCount(3);
        etiquetas[0].Should().BeOfType<DocumentReference>();

        var docRef = (DocumentReference)etiquetas[0];
        docRef.Id.Should().Be(tag1Id);
        docRef.Path.Should().Contain("Etiquetas");

        // Verificar que NO se creó FK inversa en Etiqueta
        var etiquetaRawData = await GetDocumentRawData<Etiqueta>(tag1Id);
        etiquetaRawData.Should().NotContainKey("ProductoConEtiquetas",
            "ArrayOf Reference no debe crear FK inversa en la entidad referenciada");
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
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001

    #endregion
}
