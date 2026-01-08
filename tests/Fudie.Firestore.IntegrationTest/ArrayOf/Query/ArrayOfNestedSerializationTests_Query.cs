using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para verificar deserialización de ArrayOf anidados.
/// DESERIALIZACIÓN con LINQ.
/// Patrón: Guardar con EF Core → Leer con LINQ → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfNestedSerializationTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfNestedSerializationTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deserialization_NestedThreeLevels_ShouldRestoreCorrectStructure()
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

        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfNestedTestDbContext>();
        var result = await readContext.Libros
            .FirstOrDefaultAsync(l => l.Id == libroId);

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Recetas del Mundo");

        // Nivel 1: Categorias
        result.Categorias.Should().HaveCount(2);
        result.Categorias[0].Nombre.Should().Be("Postres");

        // Nivel 2: Recetas
        result.Categorias[0].Recetas.Should().HaveCount(2);
        result.Categorias[0].Recetas[0].Nombre.Should().Be("Tiramisú");
        result.Categorias[0].Recetas[0].Instrucciones.Should().Be("Mezclar y refrigerar");

        // Nivel 3: Ingredientes
        result.Categorias[0].Recetas[0].Ingredientes.Should().HaveCount(3);
        result.Categorias[0].Recetas[0].Ingredientes[0].Nombre.Should().Be("Mascarpone");
        result.Categorias[0].Recetas[0].Ingredientes[0].Cantidad.Should().Be("500g");

        // Verificar segunda categoría
        result.Categorias[1].Nombre.Should().Be("Entrantes");
        result.Categorias[1].Recetas[0].Nombre.Should().Be("Gazpacho");
    }

    [Fact]
    public async Task Deserialization_ComplexTypeWithGeoPoints_ShouldRestoreArrayOfMapsWithGeoPoints()
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

        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ArrayOfComplexWithGeoPointTestDbContext>();
        var result = await readContext.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Logística Express");
        result.Rutas.Should().HaveCount(2);

        var rutaNorte = result.Rutas[0];
        rutaNorte.Nombre.Should().Be("Ruta Norte");
        rutaNorte.Descripcion.Should().Be("Entregas zona norte");
        rutaNorte.Puntos.Should().HaveCount(3);
        rutaNorte.Puntos[0].Latitude.Should().BeApproximately(40.4168, 0.0001);
        rutaNorte.Puntos[0].Longitude.Should().BeApproximately(-3.7038, 0.0001);

        var rutaSur = result.Rutas[1];
        rutaSur.Nombre.Should().Be("Ruta Sur");
        rutaSur.Puntos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deserialization_ComplexTypeWithReferences_ShouldRestoreArrayOfMapsWithReferences()
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

        context.Catalogos.Add(catalogo);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para las referencias anidadas
        using var readContext = _fixture.CreateContext<ArrayOfComplexWithReferencesTestDbContext>();
        var result = await readContext.Catalogos
            .Include(c => c.Secciones)
            .ThenInclude(s => s.EtiquetasDestacadas)
            .FirstOrDefaultAsync(c => c.Id == catalogoId);

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Catálogo Primavera 2024");
        result.Secciones.Should().HaveCount(2);

        var seccionTecnologia = result.Secciones[0];
        seccionTecnologia.Nombre.Should().Be("Tecnología");
        seccionTecnologia.Orden.Should().Be(1);
        seccionTecnologia.EtiquetasDestacadas.Should().HaveCount(2);
        seccionTecnologia.EtiquetasDestacadas[0].Id.Should().Be(tag1Id);
        seccionTecnologia.EtiquetasDestacadas[0].Nombre.Should().Be("Electrónica");

        var seccionOfertas = result.Secciones[1];
        seccionOfertas.EtiquetasDestacadas.Should().HaveCount(1);
        seccionOfertas.EtiquetasDestacadas[0].Id.Should().Be(tag2Id);
    }
}
