using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Conventions;

/// <summary>
/// Tests de integración para GeoPointConvention.
/// Verifica que records con Latitude/Longitude se persisten como GeoPoint nativo en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class GeoPointConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public GeoPointConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_EntityWithGeoPoint_ShouldPersist()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test GeoPoint",
            Precio = 100m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038), // Madrid
            Direccion = new Direccion
            {
                Calle = "Gran Vía",
                Ciudad = "Madrid",
                CodigoPostal = "28013",
                Coordenadas = new Coordenadas
                {
                    Altitud = 650,
                    Posicion = new GeoLocation(40.4200, -3.7025)
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
        productoLeido!.Ubicacion.Should().NotBeNull();
        productoLeido.Ubicacion.Latitude.Should().BeApproximately(40.4168, 0.0001);
        productoLeido.Ubicacion.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    [Fact]
    public async Task Add_EntityWithNestedGeoPoint_ShouldPersist()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Nested GeoPoint",
            Precio = 200m,
            Categoria = CategoriaProducto.Ropa,
            Ubicacion = new GeoLocation(41.3851, 2.1734), // Barcelona
            Direccion = new Direccion
            {
                Calle = "La Rambla",
                Ciudad = "Barcelona",
                CodigoPostal = "08002",
                Coordenadas = new Coordenadas
                {
                    Altitud = 12,
                    Posicion = new GeoLocation(41.3800, 2.1700) // GeoPoint anidado
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
        productoLeido!.Direccion.Should().NotBeNull();
        productoLeido.Direccion.Coordenadas.Should().NotBeNull();
        productoLeido.Direccion.Coordenadas.Posicion.Should().NotBeNull();
        productoLeido.Direccion.Coordenadas.Posicion.Latitude.Should().BeApproximately(41.3800, 0.0001);
        productoLeido.Direccion.Coordenadas.Posicion.Longitude.Should().BeApproximately(2.1700, 0.0001);
        productoLeido.Direccion.Coordenadas.Altitud.Should().Be(12);
    }

    [Fact]
    public async Task Query_EntityWithGeoPoint_ShouldReturnCoordinates()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Query GeoPoint",
            Precio = 150m,
            Categoria = CategoriaProducto.Alimentos,
            Ubicacion = new GeoLocation(37.3891, -5.9845), // Sevilla
            Direccion = new Direccion
            {
                Calle = "Avenida de la Constitución",
                Ciudad = "Sevilla",
                CodigoPostal = "41001",
                Coordenadas = new Coordenadas
                {
                    Altitud = 7,
                    Posicion = new GeoLocation(37.3886, -5.9823)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        // Assert - Verificar ambos GeoPoints
        productoLeido.Should().NotBeNull();

        // GeoPoint directo
        productoLeido!.Ubicacion.Latitude.Should().BeApproximately(37.3891, 0.0001);
        productoLeido.Ubicacion.Longitude.Should().BeApproximately(-5.9845, 0.0001);

        // GeoPoint anidado
        productoLeido.Direccion.Coordenadas.Posicion.Latitude.Should().BeApproximately(37.3886, 0.0001);
        productoLeido.Direccion.Coordenadas.Posicion.Longitude.Should().BeApproximately(-5.9823, 0.0001);
    }

    [Fact]
    public async Task Update_GeoPointProperty_ShouldPersistChanges()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Update GeoPoint",
            Precio = 300m,
            Categoria = CategoriaProducto.Hogar,
            Ubicacion = new GeoLocation(39.4699, -0.3763), // Valencia
            Direccion = new Direccion
            {
                Calle = "Plaza del Ayuntamiento",
                Ciudad = "Valencia",
                CodigoPostal = "46002",
                Coordenadas = new Coordenadas
                {
                    Altitud = 15,
                    Posicion = new GeoLocation(39.4700, -0.3760)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Actualizar ubicación usando la misma instancia
        producto.Ubicacion = new GeoLocation(43.2630, -2.9350); // Bilbao
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoActualizado = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Ubicacion.Latitude.Should().BeApproximately(43.2630, 0.0001);
        productoActualizado.Ubicacion.Longitude.Should().BeApproximately(-2.9350, 0.0001);
    }
}
