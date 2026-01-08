using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para ArrayOf con HashSet y records.
/// Verifica que ICollection&lt;T&gt; funciona correctamente (no solo List&lt;T&gt;).
/// Todo AUTO-DETECTADO por conventions - sin configuración explícita.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class HashSetArrayOfTests
{
    private readonly FirestoreTestFixture _fixture;

    public HashSetArrayOfTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region HashSet<Record> Embedded - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordEmbedded_AutoDetected_ShouldPersistAsArrayOfMaps()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            Tags =
            [
                new Tag("electronica", "blue"),
                new Tag("gaming", "red"),
                new Tag("premium", "gold")
            ]
        };

        // Act
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<ProductoConHashSet>(productoId);
        rawData.Should().ContainKey("Tags");

        var tags = ((IEnumerable<object>)rawData["Tags"]).ToList();
        tags.Should().HaveCount(3);

        var primerTag = tags[0] as Dictionary<string, object>;
        primerTag.Should().ContainKey("Nombre");
        primerTag.Should().ContainKey("Color");
    }

    #endregion

    #region HashSet<Record> GeoPoint - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordGeoPoint_AutoDetected_ShouldPersistAsNativeGeoPoints()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            PuntosVenta =
            [
                new Ubicacion(40.4168, -3.7038),
                new Ubicacion(41.3851, 2.1734)
            ]
        };

        // Act
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<ProductoConHashSet>(productoId);
        rawData.Should().ContainKey("PuntosVenta");

        var puntos = ((IEnumerable<object>)rawData["PuntosVenta"]).ToList();
        puntos.Should().HaveCount(2);
        puntos[0].Should().BeOfType<GeoPoint>();

        var primerPunto = (GeoPoint)puntos[0];
        primerPunto.Latitude.Should().BeApproximately(40.4168, 0.0001);
    }

    #endregion

    #region HashSet<Record> Reference - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordReference_AutoDetected_ShouldPersistAsDocumentReferences()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        var prov1Id = FirestoreTestFixture.GenerateId("prov");
        var prov2Id = FirestoreTestFixture.GenerateId("prov");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var proveedor1 = new Proveedor { Id = prov1Id, Nombre = "TechSupplier", Pais = "USA" };
        var proveedor2 = new Proveedor { Id = prov2Id, Nombre = "AsiaComponents", Pais = "China" };
        context.Proveedores.AddRange(proveedor1, proveedor2);

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            Proveedores = [proveedor1, proveedor2]
        };

        // Act
        context.Productos.Add(producto);        
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<ProductoConHashSet>(productoId);
        rawData.Should().ContainKey("Proveedores");

        var proveedores = ((IEnumerable<object>)rawData["Proveedores"]).ToList();
        proveedores.Should().HaveCount(2);
        proveedores[0].Should().BeOfType<DocumentReference>();

        var docRef = (DocumentReference)proveedores[0];
        docRef.Id.Should().BeOneOf(prov1Id, prov2Id); // HashSet no garantiza orden

        // Verificar que NO se creó FK inversa en Proveedor
        var proveedorRawData = await GetDocumentRawData<Proveedor>(prov1Id);
        proveedorRawData.Should().NotContainKey("ProductoConHashSet",
            "ArrayOf Reference no debe crear FK inversa en la entidad referenciada");
    }

    #endregion

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

        docSnapshot.Exists.Should().BeTrue($"El documento {documentId} debe existir en {collectionName}");
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
