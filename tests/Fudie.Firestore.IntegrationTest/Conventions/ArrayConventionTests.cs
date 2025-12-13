using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Conventions;

/// <summary>
/// Tests de integración para ArrayConvention.
/// Verifica que List&lt;int&gt; y List&lt;string&gt; se persisten como arrays nativos en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_EntityWithListInt_ShouldPersistAsArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test List Int",
            Precio = 100m,
            Categoria = CategoriaProducto.Electronica,
            Cantidades = [10, 20, 30, 40, 50],
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Test",
                Ciudad = "Test",
                CodigoPostal = "00000",
                Coordenadas = new Coordenadas
                {
                    Altitud = 0,
                    Posicion = new GeoLocation(0, 0)
                }
            }
        };

        // Act
        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoLeido.Should().NotBeNull();
        productoLeido!.Cantidades.Should().HaveCount(5);
        productoLeido.Cantidades.Should().BeEquivalentTo([10, 20, 30, 40, 50]);
    }

    [Fact]
    public async Task Add_EntityWithListString_ShouldPersistAsArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test List String",
            Precio = 100m,
            Categoria = CategoriaProducto.Ropa,
            Etiquetas = ["oferta", "nuevo", "destacado", "premium"],
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Test",
                Ciudad = "Test",
                CodigoPostal = "00000",
                Coordenadas = new Coordenadas
                {
                    Altitud = 0,
                    Posicion = new GeoLocation(0, 0)
                }
            }
        };

        // Act
        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoLeido.Should().NotBeNull();
        productoLeido!.Etiquetas.Should().HaveCount(4);
        productoLeido.Etiquetas.Should().BeEquivalentTo(["oferta", "nuevo", "destacado", "premium"]);
    }

    [Fact]
    public async Task Query_EntityWithArrays_ShouldReturnCorrectValues()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Query Arrays",
            Precio = 150m,
            Categoria = CategoriaProducto.Alimentos,
            Cantidades = [5, 10, 15],
            Etiquetas = ["organico", "local"],
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Test",
                Ciudad = "Test",
                CodigoPostal = "00000",
                Coordenadas = new Coordenadas
                {
                    Altitud = 0,
                    Posicion = new GeoLocation(0, 0)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        // Assert
        productoLeido.Should().NotBeNull();
        productoLeido!.Cantidades.Should().BeEquivalentTo([5, 10, 15]);
        productoLeido.Etiquetas.Should().BeEquivalentTo(["organico", "local"]);
    }

    [Fact]
    public async Task Update_ArrayProperty_ShouldPersistChanges()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Update Arrays",
            Precio = 200m,
            Categoria = CategoriaProducto.Hogar,
            Cantidades = [1, 2, 3],
            Etiquetas = ["original"],
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Test",
                Ciudad = "Test",
                CodigoPostal = "00000",
                Coordenadas = new Coordenadas
                {
                    Altitud = 0,
                    Posicion = new GeoLocation(0, 0)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Actualizar arrays usando la misma instancia
        producto.Cantidades = [100, 200, 300, 400];
        producto.Etiquetas = ["actualizado", "modificado", "nuevo"];
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoActualizado = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Cantidades.Should().BeEquivalentTo([100, 200, 300, 400]);
        productoActualizado.Etiquetas.Should().BeEquivalentTo(["actualizado", "modificado", "nuevo"]);
    }
}
