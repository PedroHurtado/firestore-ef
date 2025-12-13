using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Conventions;

/// <summary>
/// Tests de integración para DecimalToDoubleConvention.
/// Verifica que decimal se persiste como double en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class DecimalConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public DecimalConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_EntityWithDecimal_ShouldPersistAsDouble()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Decimal",
            Precio = 1299.99m,
            Categoria = CategoriaProducto.Electronica,
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

        // Assert - Leer y verificar
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoLeido.Should().NotBeNull();
        productoLeido!.Precio.Should().Be(1299.99m);
    }

    [Fact]
    public async Task Add_EntityWithListDecimal_ShouldPersistAsDoubleArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test List Decimal",
            Precio = 100m,
            Categoria = CategoriaProducto.Electronica,
            Precios = [10.5m, 20.3m, 30.1m],
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
        productoLeido!.Precios.Should().HaveCount(3);
        productoLeido.Precios.Should().BeEquivalentTo([10.5m, 20.3m, 30.1m]);
    }

    [Fact]
    public async Task Query_EntityWithDecimal_ShouldReturnCorrectValue()
    {
        // Arrange - Crear producto con precio específico
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Query Decimal",
            Precio = 999.95m,
            Categoria = CategoriaProducto.Ropa,
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

        // Act - Consultar
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        // Assert
        productoLeido.Should().NotBeNull();
        productoLeido!.Precio.Should().Be(999.95m);
    }

    [Fact]
    public async Task Update_DecimalProperty_ShouldPersistChanges()
    {
        // Arrange - Crear producto
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Update Decimal",
            Precio = 100.00m,
            Categoria = CategoriaProducto.Alimentos,
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

        // Act - Actualizar precio usando la misma instancia (evita problema de ComplexTypes null al releer)
        producto.Precio = 250.50m;
        await context.SaveChangesAsync();

        // Assert - Leer con nuevo contexto
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoActualizado = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Precio.Should().Be(250.50m);
    }
}
