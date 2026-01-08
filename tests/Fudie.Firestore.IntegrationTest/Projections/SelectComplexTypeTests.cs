using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Projections;

/// <summary>
/// Integration tests for Select (projection) operators with ComplexTypes.
/// Fase 2: ComplexTypes
/// Ciclo 4: Select ComplexType completo
/// Ciclo 5: Select campo de ComplexType
/// Ciclo 6: Select múltiples campos de ComplexType
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SelectComplexTypeTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectComplexTypeTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 4: Select ComplexType completo

    [Fact]
    public async Task Select_ComplexTypeCompleto_ReturnsEntireComplexType()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("selectct");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Tienda Centro",
            Precio = 1000m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Gran Vía 123",
                Ciudad = "Madrid",
                CodigoPostal = "28013",
                Coordenadas = new Coordenadas
                {
                    Altitud = 650,
                    Posicion = new GeoLocation(40.4200, -3.7050)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Direccion)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var direccion = results[0];
        direccion.Calle.Should().Be("Gran Vía 123");
        direccion.Ciudad.Should().Be("Madrid");
        direccion.CodigoPostal.Should().Be("28013");
        direccion.Coordenadas.Altitud.Should().Be(650);
        direccion.Coordenadas.Posicion.Latitude.Should().Be(40.4200);
        direccion.Coordenadas.Posicion.Longitude.Should().Be(-3.7050);
    }

    #endregion

    #region Ciclo 5: Select campo de ComplexType

    [Fact]
    public async Task Select_FieldFromComplexType_ReturnsOnlyThatField()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniquePrefix = $"SelectCTF-{Guid.NewGuid():N}";
        var productos = new[]
        {
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("selectctf"),
                Nombre = $"{uniquePrefix}-Norte",
                Precio = 500m,
                Categoria = CategoriaProducto.Ropa,
                Ubicacion = new GeoLocation(41.3851, 2.1734),
                Direccion = new Direccion
                {
                    Calle = "Paseo de Gracia 50",
                    Ciudad = "Barcelona",
                    CodigoPostal = "08007",
                    Coordenadas = new Coordenadas
                    {
                        Altitud = 12,
                        Posicion = new GeoLocation(41.3900, 2.1700)
                    }
                }
            },
            new ProductoCompleto
            {
                Id = FirestoreTestFixture.GenerateId("selectctf"),
                Nombre = $"{uniquePrefix}-Sur",
                Precio = 750m,
                Categoria = CategoriaProducto.Alimentos,
                Ubicacion = new GeoLocation(37.3891, -5.9845),
                Direccion = new Direccion
                {
                    Calle = "Avenida de la Constitución 10",
                    Ciudad = "Sevilla",
                    CodigoPostal = "41001",
                    Coordenadas = new Coordenadas
                    {
                        Altitud = 7,
                        Posicion = new GeoLocation(37.3900, -5.9900)
                    }
                }
            }
        };

        context.ProductosCompletos.AddRange(productos);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Nombre.StartsWith(uniquePrefix))
            .OrderBy(p => p.Direccion.Ciudad)
            .Select(p => p.Direccion.Ciudad)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Barcelona");
        results[1].Should().Be("Sevilla");
    }

    #endregion

    #region Ciclo 6: Select múltiples campos de ComplexType

    [Fact]
    public async Task Select_MultipleFieldsFromComplexType_ReturnsOnlySelectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("selectctm");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Tienda Este",
            Precio = 1200m,
            Categoria = CategoriaProducto.Hogar,
            Ubicacion = new GeoLocation(39.4699, -0.3763),
            Direccion = new Direccion
            {
                Calle = "Calle Colón 25",
                Ciudad = "Valencia",
                CodigoPostal = "46004",
                Coordenadas = new Coordenadas
                {
                    Altitud = 15,
                    Posicion = new GeoLocation(39.4700, -0.3800)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => new
            {
                p.Nombre,
                p.Direccion.Ciudad,
                p.Direccion.CodigoPostal,
                Altitud = p.Direccion.Coordenadas.Altitud
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Tienda Este");
        result.Ciudad.Should().Be("Valencia");
        result.CodigoPostal.Should().Be("46004");
        result.Altitud.Should().Be(15);
    }

    [Fact]
    public async Task Select_ComplexTypeToRecord_MapsFieldsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("selectctr");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Tienda Oeste",
            Precio = 800m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(43.2630, -2.9350),
            Direccion = new Direccion
            {
                Calle = "Gran Vía 45",
                Ciudad = "Bilbao",
                CodigoPostal = "48001",
                Coordenadas = new Coordenadas
                {
                    Altitud = 19,
                    Posicion = new GeoLocation(43.2650, -2.9400)
                }
            }
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => new DireccionResumenRecord(
                p.Direccion.Ciudad,
                p.Direccion.CodigoPostal,
                p.Direccion.Coordenadas.Altitud))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Ciudad.Should().Be("Bilbao");
        result.CodigoPostal.Should().Be("48001");
        result.Altitud.Should().Be(19);
    }

    #endregion
}

/// <summary>
/// DTO record for ComplexType projection tests.
/// </summary>
public record DireccionResumenRecord(string Ciudad, string CodigoPostal, double Altitud);
