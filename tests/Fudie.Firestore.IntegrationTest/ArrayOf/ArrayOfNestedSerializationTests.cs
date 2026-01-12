using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para verificar serialización de ArrayOf anidados.
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfNestedSerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfNestedSerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Serialization_NestedThreeLevels_ShouldStoreCorrectStructure()
    {
        // Arrange
        var libroId = FirestoreTestFixture.GenerateId("libro");
        using var context = _fixture.CreateContext<ArrayOfNestedTestDbContext>();

        var libro = new LibroCocina
        {
            Id = libroId,
            Titulo = "Recetas del Mundo",
            Categorias =
            [
                new Categoria
                {
                    Nombre = "Postres",
                    Recetas =
                    [
                        new Receta
                        {
                            Nombre = "Tiramisú",
                            Instrucciones = "Mezclar y refrigerar",
                            Ingredientes =
                            [
                                new Ingrediente { Nombre = "Mascarpone", Cantidad = "500g" },
                                new Ingrediente { Nombre = "Café", Cantidad = "200ml" },
                                new Ingrediente { Nombre = "Bizcochos", Cantidad = "300g" }
                            ]
                        },
                        new Receta
                        {
                            Nombre = "Flan",
                            Instrucciones = "Hornear al baño maría",
                            Ingredientes =
                            [
                                new Ingrediente { Nombre = "Huevos", Cantidad = "4 unidades" },
                                new Ingrediente { Nombre = "Leche", Cantidad = "500ml" }
                            ]
                        }
                    ]
                },
                new Categoria
                {
                    Nombre = "Entrantes",
                    Recetas =
                    [
                        new Receta
                        {
                            Nombre = "Gazpacho",
                            Instrucciones = "Triturar y enfriar",
                            Ingredientes =
                            [
                                new Ingrediente { Nombre = "Tomates", Cantidad = "1kg" },
                                new Ingrediente { Nombre = "Pepino", Cantidad = "1 unidad" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<LibroCocina>(libroId);
        rawData.Should().ContainKey("Categorias");

        // Nivel 1: Categorias
        var categorias = ((IEnumerable<object>)rawData["Categorias"]).ToList();
        categorias.Should().HaveCount(2);

        var postres = categorias[0] as Dictionary<string, object>;
        postres!["Nombre"].Should().Be("Postres");
        postres.Should().ContainKey("Recetas");

        // Nivel 2: Recetas
        var recetas = ((IEnumerable<object>)postres["Recetas"]).ToList();
        recetas.Should().HaveCount(2);

        var tiramisu = recetas[0] as Dictionary<string, object>;
        tiramisu!["Nombre"].Should().Be("Tiramisú");
        tiramisu["Instrucciones"].Should().Be("Mezclar y refrigerar");
        tiramisu.Should().ContainKey("Ingredientes");

        // Nivel 3: Ingredientes
        var ingredientes = ((IEnumerable<object>)tiramisu["Ingredientes"]).ToList();
        ingredientes.Should().HaveCount(3);

        var mascarpone = ingredientes[0] as Dictionary<string, object>;
        mascarpone!["Nombre"].Should().Be("Mascarpone");
        mascarpone["Cantidad"].Should().Be("500g");
    }

    [Fact]
    public async Task Serialization_ComplexTypeWithGeoPoints_ShouldStoreAsArrayOfMapsWithGeoPoints()
    {
        // Arrange
        var empresaId = FirestoreTestFixture.GenerateId("empresa");
        using var context = _fixture.CreateContext<ArrayOfComplexWithGeoPointTestDbContext>();

        var empresa = new EmpresaLogistica
        {
            Id = empresaId,
            Nombre = "Logística Express",
            Rutas =
            [
                new RutaEntrega
                {
                    Nombre = "Ruta Norte",
                    Descripcion = "Entregas zona norte",
                    Puntos =
                    [
                        new UbicacionGeo { Latitude = 40.4168, Longitude = -3.7038 },
                        new UbicacionGeo { Latitude = 40.4500, Longitude = -3.6833 },
                        new UbicacionGeo { Latitude = 40.4800, Longitude = -3.7100 }
                    ]
                },
                new RutaEntrega
                {
                    Nombre = "Ruta Sur",
                    Descripcion = "Entregas zona sur",
                    Puntos =
                    [
                        new UbicacionGeo { Latitude = 40.3800, Longitude = -3.7200 },
                        new UbicacionGeo { Latitude = 40.3500, Longitude = -3.7500 }
                    ]
                }
            ]
        };

        // Act
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EmpresaLogistica>(empresaId);
        rawData.Should().ContainKey("Rutas");

        var rutas = ((IEnumerable<object>)rawData["Rutas"]).ToList();
        rutas.Should().HaveCount(2);

        var rutaNorte = rutas[0] as Dictionary<string, object>;
        rutaNorte!["Nombre"].Should().Be("Ruta Norte");
        rutaNorte["Descripcion"].Should().Be("Entregas zona norte");
        rutaNorte.Should().ContainKey("Puntos");

        // Verificar que los puntos son GeoPoints nativos
        var puntos = ((IEnumerable<object>)rutaNorte["Puntos"]).ToList();
        puntos.Should().HaveCount(3);
        puntos[0].Should().BeOfType<GeoPoint>();

        var primerPunto = (GeoPoint)puntos[0];
        primerPunto.Latitude.Should().BeApproximately(40.4168, 0.0001);
        primerPunto.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    [Fact]
    public async Task Serialization_ComplexTypeWithReferences_ShouldStoreAsArrayOfMapsWithDocumentReferences()
    {
        // Arrange
        var catalogoId = FirestoreTestFixture.GenerateId("catalogo");
        var tag1Id = FirestoreTestFixture.GenerateId("tag");
        var tag2Id = FirestoreTestFixture.GenerateId("tag");
        var tag3Id = FirestoreTestFixture.GenerateId("tag");

        using var context = _fixture.CreateContext<ArrayOfComplexWithReferencesTestDbContext>();

        // Crear etiquetas primero
        var tagElectronica = new Etiqueta { Id = tag1Id, Nombre = "Electrónica" };
        var tagOferta = new Etiqueta { Id = tag2Id, Nombre = "Oferta" };
        var tagNuevo = new Etiqueta { Id = tag3Id, Nombre = "Nuevo" };
        context.Etiquetas.AddRange(tagElectronica, tagOferta, tagNuevo);

        var catalogo = new Catalogo
        {
            Id = catalogoId,
            Titulo = "Catálogo Primavera 2024",
            Secciones =
            [
                new Seccion
                {
                    Nombre = "Tecnología",
                    Orden = 1,
                    EtiquetasDestacadas = [tagElectronica, tagNuevo]
                },
                new Seccion
                {
                    Nombre = "Ofertas Especiales",
                    Orden = 2,
                    EtiquetasDestacadas = [tagOferta]
                }
            ]
        };

        // Act
        context.Catalogos.Add(catalogo);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<Catalogo>(catalogoId);
        rawData.Should().ContainKey("Secciones");

        var secciones = ((IEnumerable<object>)rawData["Secciones"]).ToList();
        secciones.Should().HaveCount(2);

        var seccionTecnologia = secciones[0] as Dictionary<string, object>;
        seccionTecnologia!["Nombre"].Should().Be("Tecnología");
        seccionTecnologia["Orden"].Should().Be(1L);
        seccionTecnologia.Should().ContainKey("EtiquetasDestacadas");

        // Verificar que las etiquetas son DocumentReferences
        var etiquetas = ((IEnumerable<object>)seccionTecnologia["EtiquetasDestacadas"]).ToList();
        etiquetas.Should().HaveCount(2);
        etiquetas[0].Should().BeOfType<DocumentReference>();

        var primeraRef = (DocumentReference)etiquetas[0];
        primeraRef.Id.Should().Be(tag1Id);
        primeraRef.Path.Should().Contain("Etiquetas");
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
