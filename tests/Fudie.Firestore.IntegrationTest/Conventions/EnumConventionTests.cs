using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Conventions;

/// <summary>
/// Tests de integración para EnumToStringConvention.
/// Verifica que enum se persiste como string en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class EnumConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public EnumConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_EntityWithEnum_ShouldPersistAsString()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Enum",
            Precio = 100m,
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
        productoLeido!.Categoria.Should().Be(CategoriaProducto.Electronica);
    }

    [Fact]
    public async Task Add_EntityWithListEnum_ShouldPersistAsStringArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test List Enum",
            Precio = 100m,
            Categoria = CategoriaProducto.Ropa,
            Tags = [CategoriaProducto.Electronica, CategoriaProducto.Hogar, CategoriaProducto.Alimentos],
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
        productoLeido!.Tags.Should().HaveCount(3);
        productoLeido.Tags.Should().BeEquivalentTo([
            CategoriaProducto.Electronica,
            CategoriaProducto.Hogar,
            CategoriaProducto.Alimentos
        ]);
    }

    [Fact]
    public async Task Query_EntityWithEnum_ShouldReturnCorrectValue()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Query Enum",
            Precio = 50m,
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

        // Act - Consultar
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        // Assert
        productoLeido.Should().NotBeNull();
        productoLeido!.Categoria.Should().Be(CategoriaProducto.Alimentos);
    }

    [Fact]
    public async Task Update_EnumProperty_ShouldPersistChanges()
    {
        // Arrange - Crear producto
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Update Enum",
            Precio = 200m,
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

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Actualizar categoría usando la misma instancia
        producto.Categoria = CategoriaProducto.Hogar;
        await context.SaveChangesAsync();

        // Assert - Leer con nuevo contexto
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoActualizado = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Categoria.Should().Be(CategoriaProducto.Hogar);
    }

    [Fact]
    public async Task Query_FilterByEnumEquals_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var idHogar = FirestoreTestFixture.GenerateId("prod");
        var idElectronica = FirestoreTestFixture.GenerateId("prod");

        var productoHogar = new ProductoCompleto
        {
            Id = idHogar,
            Nombre = "Test Filter Enum Hogar",
            Precio = 200m,
            Categoria = CategoriaProducto.Hogar,
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

        var productoElectronica = new ProductoCompleto
        {
            Id = idElectronica,
            Nombre = "Test Filter Enum Electronica",
            Precio = 300m,
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

        context.ProductosCompletos.Add(productoHogar);
        context.ProductosCompletos.Add(productoElectronica);
        await context.SaveChangesAsync();

        // Act - Filtrar por categoría Hogar
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productosHogar = await readContext.ProductosCompletos
            .Where(p => p.Categoria == CategoriaProducto.Hogar)
            .ToListAsync();

        // Assert
        productosHogar.Should().Contain(p => p.Id == idHogar);
        productosHogar.Should().NotContain(p => p.Id == idElectronica);
    }

    [Fact]
    public async Task Query_FilterByEnumNotEquals_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var idRopa = FirestoreTestFixture.GenerateId("prod");
        var idAlimentos = FirestoreTestFixture.GenerateId("prod");

        var productoRopa = new ProductoCompleto
        {
            Id = idRopa,
            Nombre = "Test NotEquals Ropa",
            Precio = 150m,
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

        var productoAlimentos = new ProductoCompleto
        {
            Id = idAlimentos,
            Nombre = "Test NotEquals Alimentos",
            Precio = 50m,
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

        context.ProductosCompletos.Add(productoRopa);
        context.ProductosCompletos.Add(productoAlimentos);
        await context.SaveChangesAsync();

        // Act - Filtrar por categoría != Ropa
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productosNoRopa = await readContext.ProductosCompletos
            .Where(p => p.Categoria != CategoriaProducto.Ropa)
            .ToListAsync();

        // Assert
        productosNoRopa.Should().Contain(p => p.Id == idAlimentos);
        productosNoRopa.Should().NotContain(p => p.Id == idRopa);
    }

    [Fact]
    public async Task Query_FilterByEnumWithVariable_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Variable Enum",
            Precio = 100m,
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

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Filtrar usando variable
        var categoriaFiltro = CategoriaProducto.Electronica;
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productos = await readContext.ProductosCompletos
            .Where(p => p.Categoria == categoriaFiltro)
            .ToListAsync();

        // Assert
        productos.Should().Contain(p => p.Id == id);
    }
}
