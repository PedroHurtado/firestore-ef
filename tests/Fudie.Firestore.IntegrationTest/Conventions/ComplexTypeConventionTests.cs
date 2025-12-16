using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Conventions;

/// <summary>
/// Tests de integración para ComplexType (Value Objects).
/// Verifica que ComplexTypes se persisten como maps anidados en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ComplexTypeConventionTests
{
    private readonly FirestoreTestFixture _fixture;

    public ComplexTypeConventionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_EntityWithComplexType_ShouldPersist()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test ComplexType",
            Precio = 100m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Calle Mayor 123",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas
                {
                    Altitud = 650,
                    Posicion = new GeoLocation(40.4168, -3.7038)
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
        productoLeido.Direccion.Calle.Should().Be("Calle Mayor 123");
        productoLeido.Direccion.Ciudad.Should().Be("Madrid");
        productoLeido.Direccion.CodigoPostal.Should().Be("28001");
    }

    [Fact]
    public async Task Add_EntityWithNestedComplexType_ShouldPersist()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Nested ComplexType",
            Precio = 200m,
            Categoria = CategoriaProducto.Ropa,
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "La Rambla 45",
                Ciudad = "Barcelona",
                CodigoPostal = "08002",
                Coordenadas = new Coordenadas
                {
                    Altitud = 12,
                    Posicion = new GeoLocation(41.3851, 2.1734)
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
        productoLeido.Direccion.Coordenadas.Altitud.Should().Be(12);
        productoLeido.Direccion.Coordenadas.Posicion.Should().NotBeNull();
        productoLeido.Direccion.Coordenadas.Posicion.Latitude.Should().BeApproximately(41.3851, 0.0001);
        productoLeido.Direccion.Coordenadas.Posicion.Longitude.Should().BeApproximately(2.1734, 0.0001);
    }

    [Fact]
    public async Task Query_EntityWithComplexType_ShouldReturnData()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Query ComplexType",
            Precio = 150m,
            Categoria = CategoriaProducto.Alimentos,
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Avenida de la Constitución 10",
                Ciudad = "Sevilla",
                CodigoPostal = "41001",
                Coordenadas = new Coordenadas
                {
                    Altitud = 7,
                    Posicion = new GeoLocation(37.3891, -5.9845)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoLeido = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        // Assert - Verificar toda la estructura del ComplexType
        productoLeido.Should().NotBeNull();
        productoLeido!.Direccion.Should().NotBeNull();
        productoLeido.Direccion.Calle.Should().Be("Avenida de la Constitución 10");
        productoLeido.Direccion.Ciudad.Should().Be("Sevilla");
        productoLeido.Direccion.CodigoPostal.Should().Be("41001");
        productoLeido.Direccion.Coordenadas.Altitud.Should().Be(7);
        productoLeido.Direccion.Coordenadas.Posicion.Latitude.Should().BeApproximately(37.3891, 0.0001);
    }

    [Fact]
    public async Task Update_ComplexTypeProperty_ShouldPersistChanges()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var id = FirestoreTestFixture.GenerateId("prod");

        var producto = new ProductoCompleto
        {
            Id = id,
            Nombre = "Test Update ComplexType",
            Precio = 300m,
            Categoria = CategoriaProducto.Hogar,
            Ubicacion = new GeoLocation(0, 0),
            Direccion = new Direccion
            {
                Calle = "Calle Original 1",
                Ciudad = "Valencia",
                CodigoPostal = "46001",
                Coordenadas = new Coordenadas
                {
                    Altitud = 15,
                    Posicion = new GeoLocation(39.4699, -0.3763)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Actualizar ComplexType completo usando la misma instancia
        producto.Direccion = new Direccion
        {
            Calle = "Calle Nueva 99",
            Ciudad = "Bilbao",
            CodigoPostal = "48001",
            Coordenadas = new Coordenadas
            {
                Altitud = 19,
                Posicion = new GeoLocation(43.2630, -2.9350)
            }
        };
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var productoActualizado = await readContext.ProductosCompletos
            .FirstOrDefaultAsync(p => p.Id == id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Direccion.Calle.Should().Be("Calle Nueva 99");
        productoActualizado.Direccion.Ciudad.Should().Be("Bilbao");
        productoActualizado.Direccion.CodigoPostal.Should().Be("48001");
        productoActualizado.Direccion.Coordenadas.Altitud.Should().Be(19);
        productoActualizado.Direccion.Coordenadas.Posicion.Latitude.Should().BeApproximately(43.2630, 0.0001);
    }

    #region Where with ComplexType Properties (Ciclo 9.2)

    [Fact]
    public async Task Where_ComplexTypeProperty_FiltersByNestedField()
    {
        // Arrange - Create entities with different cities
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniquePrefix = $"CplxWhere-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("cplx"),
                Nombre = $"{uniquePrefix}-Madrid",
                Precio = 100m,
                Categoria = CategoriaProducto.Electronica,
                Ubicacion = new GeoLocation(0, 0),
                Direccion = new Direccion
                {
                    Calle = "Calle Mayor",
                    Ciudad = uniquePrefix + "-Madrid",
                    CodigoPostal = "28001",
                    Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.4168, -3.7038) }
                }
            },
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("cplx"),
                Nombre = $"{uniquePrefix}-Barcelona",
                Precio = 200m,
                Categoria = CategoriaProducto.Ropa,
                Ubicacion = new GeoLocation(0, 0),
                Direccion = new Direccion
                {
                    Calle = "La Rambla",
                    Ciudad = uniquePrefix + "-Barcelona",
                    CodigoPostal = "08002",
                    Coordenadas = new Coordenadas { Altitud = 12, Posicion = new GeoLocation(41.3851, 2.1734) }
                }
            }
        };

        context.ProductosCompletos.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by nested property: Direccion.Ciudad
        var targetCity = uniquePrefix + "-Madrid";
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Direccion.Ciudad == targetCity)
            .ToListAsync();

        // Assert - Should return only the Madrid entity
        results.Should().HaveCount(1);
        results[0].Direccion.Ciudad.Should().Be(targetCity);
        results[0].Nombre.Should().Contain("Madrid");
    }

    [Fact]
    public async Task Where_DeepNestedProperty_FiltersByDeepField()
    {
        // Arrange - Create entities with different altitudes using unique city
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueCity = $"DeepNest-{Guid.NewGuid():N}";
        var highAltitude = 1000.0;
        var lowAltitude = 50.0;
        var entities = new[]
        {
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("deep"),
                Nombre = "High-Altitude",
                Precio = 100m,
                Categoria = CategoriaProducto.Electronica,
                Ubicacion = new GeoLocation(0, 0),
                Direccion = new Direccion
                {
                    Calle = "Mountain Road",
                    Ciudad = uniqueCity,
                    CodigoPostal = "00001",
                    Coordenadas = new Coordenadas { Altitud = highAltitude, Posicion = new GeoLocation(0, 0) }
                }
            },
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("deep"),
                Nombre = "Low-Altitude",
                Precio = 200m,
                Categoria = CategoriaProducto.Ropa,
                Ubicacion = new GeoLocation(0, 0),
                Direccion = new Direccion
                {
                    Calle = "Beach Road",
                    Ciudad = uniqueCity,
                    CodigoPostal = "00002",
                    Coordenadas = new Coordenadas { Altitud = lowAltitude, Posicion = new GeoLocation(0, 0) }
                }
            }
        };

        context.ProductosCompletos.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by deep nested property: Direccion.Coordenadas.Altitud > 500 AND Direccion.Ciudad == uniqueCity
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Direccion.Ciudad == uniqueCity && p.Direccion.Coordenadas.Altitud > 500)
            .ToListAsync();

        // Assert - Should return only the high altitude entity
        results.Should().HaveCount(1);
        results[0].Direccion.Coordenadas.Altitud.Should().Be(highAltitude);
        results[0].Nombre.Should().Be("High-Altitude");
    }

    #endregion
}
