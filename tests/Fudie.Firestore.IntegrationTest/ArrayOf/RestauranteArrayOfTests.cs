using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para los 5 casos de ArrayOf definidos en ARRAYOF_IMPLEMENTATION_PLAN.md
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class RestauranteArrayOfTests
{
    private readonly FirestoreTestFixture _fixture;

    public RestauranteArrayOfTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region CASO 1: ArrayOf Embedded Simple

    [Fact]
    public async Task Caso1_ArrayOfEmbedded_ShouldPersistAsArrayOfMaps()
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

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Restaurante>(restauranteId);
        rawData.Should().ContainKey("Horarios");

        var horarios = ((IEnumerable<object>)rawData["Horarios"]).ToList();
        horarios.Should().HaveCount(2);

        var primerHorario = horarios[0] as Dictionary<string, object>;
        primerHorario!["Dia"].Should().Be("Lunes");
    }

    #endregion

    #region CASO 2: ArrayOf GeoPoints

    [Fact]
    public async Task Caso2_ArrayOfGeoPoints_ShouldPersistAsNativeGeoPoints()
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

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Restaurante>(restauranteId);
        rawData.Should().ContainKey("ZonasCobertura");

        var zonas = ((IEnumerable<object>)rawData["ZonasCobertura"]).ToList();
        zonas.Should().HaveCount(2);
        zonas[0].Should().BeOfType<GeoPoint>();

        var primerPunto = (GeoPoint)zonas[0];
        primerPunto.Latitude.Should().BeApproximately(40.4168, 0.0001);
        primerPunto.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    #endregion

    #region CASO 3: ArrayOf References

    [Fact]
    public async Task Caso3_ArrayOfReferences_ShouldPersistAsDocumentReferences()
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

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Restaurante>(restauranteId);
        rawData.Should().ContainKey("Categorias");

        var categorias = ((IEnumerable<object>)rawData["Categorias"]).ToList();
        categorias.Should().HaveCount(2);
        categorias[0].Should().BeOfType<DocumentReference>();

        var docRef = (DocumentReference)categorias[0];
        docRef.Id.Should().Be(cat1Id);

        // Verificar que NO se creó FK inversa en CategoriaRestaurante
        var categoriaRawData = await GetDocumentRawData<CategoriaRestaurante>(cat1Id);
        categoriaRawData.Should().NotContainKey("Restaurante",
            "ArrayOf Reference no debe crear FK inversa en la entidad referenciada");
    }

    #endregion

    #region CASO 4: ArrayOf Embedded con Reference

    [Fact]
    public async Task Caso4_ArrayOfEmbeddedWithReference_ShouldPersistWithDocumentReference()
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

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Restaurante>(restauranteId);
        rawData.Should().ContainKey("Certificaciones");

        var certificaciones = ((IEnumerable<object>)rawData["Certificaciones"]).ToList();
        certificaciones.Should().HaveCount(1);

        var primeraCert = certificaciones[0] as Dictionary<string, object>;
        primeraCert!["Nombre"].Should().Be("ISO 9001");
        primeraCert.Should().ContainKey("Certificador");
        primeraCert["Certificador"].Should().BeOfType<DocumentReference>();

        var certRef = (DocumentReference)primeraCert["Certificador"];
        certRef.Id.Should().Be(certId);
    }

    #endregion

    #region CASO 5: ArrayOf Embedded Anidado con Reference

    [Fact]
    public async Task Caso5_ArrayOfNestedEmbeddedWithReference_ShouldPersistNestedStructure()
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

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Restaurante>(restauranteId);
        rawData.Should().ContainKey("Menus");

        var menus = ((IEnumerable<object>)rawData["Menus"]).ToList();
        menus.Should().HaveCount(1);

        var primerMenu = menus[0] as Dictionary<string, object>;
        primerMenu!["Nombre"].Should().Be("Carta Principal");
        primerMenu.Should().ContainKey("Secciones");

        var secciones = ((IEnumerable<object>)primerMenu["Secciones"]).ToList();
        var primeraSeccion = secciones[0] as Dictionary<string, object>;
        primeraSeccion!["Titulo"].Should().Be("Entrantes");
        primeraSeccion.Should().ContainKey("Items");

        var items = ((IEnumerable<object>)primeraSeccion["Items"]).ToList();
        var primerItem = items[0] as Dictionary<string, object>;
        primerItem!["Descripcion"].Should().Be("Ración completa");
        primerItem.Should().ContainKey("Plato");
        primerItem["Plato"].Should().BeOfType<DocumentReference>();

        var platoRef = (DocumentReference)primerItem["Plato"];
        platoRef.Id.Should().Be(platoId);
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
