using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Projections;

/// <summary>
/// Integration tests for Select (projection) operators with Arrays/Lists.
/// Covers:
/// - List of primitives (string, int, double, decimal)
/// - List of enums
/// - List of GeoLocation
/// - List of ComplexType
/// - Projections extracting specific fields from list elements
/// </summary>
/// <remarks>
/// SKIP: Pendiente implementar ValueObjectList() y ReferenceList() para soportar
/// List&lt;ComplexType&gt; y List&lt;GeoLocation&gt; en el modelo de EF Core.
/// Ver CONFIGURATION.md para la sintaxis propuesta.
/// </remarks>
[Collection(nameof(FirestoreTestCollection))]
public class SelectArrayTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectArrayTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region List of Primitives

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfStrings_ReturnsStringArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrstr");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con etiquetas",
            Precio = 100m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Test",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            Etiquetas = ["rojo", "grande", "premium"]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Etiquetas)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var etiquetas = results[0];
        etiquetas.Should().HaveCount(3);
        etiquetas.Should().Contain("rojo");
        etiquetas.Should().Contain("grande");
        etiquetas.Should().Contain("premium");
    }

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfInts_ReturnsIntArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrint");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con cantidades",
            Precio = 200m,
            Categoria = CategoriaProducto.Ropa,
            Ubicacion = new GeoLocation(41.3851, 2.1734),
            Direccion = new Direccion
            {
                Calle = "Calle Barcelona",
                Ciudad = "Barcelona",
                CodigoPostal = "08001",
                Coordenadas = new Coordenadas { Altitud = 12, Posicion = new GeoLocation(41.39, 2.17) }
            },
            Cantidades = [10, 25, 50, 100]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Cantidades)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var cantidades = results[0];
        cantidades.Should().HaveCount(4);
        cantidades.Should().BeEquivalentTo([10, 25, 50, 100]);
    }

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfDoubles_ReturnsDoubleArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrdbl");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con pesos",
            Precio = 300m,
            Categoria = CategoriaProducto.Alimentos,
            Ubicacion = new GeoLocation(37.3891, -5.9845),
            Direccion = new Direccion
            {
                Calle = "Avenida Sevilla",
                Ciudad = "Sevilla",
                CodigoPostal = "41001",
                Coordenadas = new Coordenadas { Altitud = 7, Posicion = new GeoLocation(37.39, -5.98) }
            },
            Pesos = [1.5, 2.75, 3.25, 4.0]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Pesos)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var pesos = results[0];
        pesos.Should().HaveCount(4);
        pesos.Should().BeEquivalentTo([1.5, 2.75, 3.25, 4.0]);
    }

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfDecimals_ReturnsDecimalArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrdec");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con precios",
            Precio = 400m,
            Categoria = CategoriaProducto.Hogar,
            Ubicacion = new GeoLocation(39.4699, -0.3763),
            Direccion = new Direccion
            {
                Calle = "Calle Valencia",
                Ciudad = "Valencia",
                CodigoPostal = "46001",
                Coordenadas = new Coordenadas { Altitud = 15, Posicion = new GeoLocation(39.47, -0.38) }
            },
            Precios = [19.99m, 29.99m, 49.99m, 99.99m]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Precios)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var precios = results[0];
        precios.Should().HaveCount(4);
        precios.Should().BeEquivalentTo([19.99m, 29.99m, 49.99m, 99.99m]);
    }

    #endregion

    #region List of Enums

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfEnums_ReturnsEnumArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrenum");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto multi-categoria",
            Precio = 500m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(43.2630, -2.9350),
            Direccion = new Direccion
            {
                Calle = "Gran Via Bilbao",
                Ciudad = "Bilbao",
                CodigoPostal = "48001",
                Coordenadas = new Coordenadas { Altitud = 19, Posicion = new GeoLocation(43.26, -2.94) }
            },
            Tags = [CategoriaProducto.Electronica, CategoriaProducto.Hogar, CategoriaProducto.Ropa]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Tags)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var tags = results[0];
        tags.Should().HaveCount(3);
        tags.Should().Contain(CategoriaProducto.Electronica);
        tags.Should().Contain(CategoriaProducto.Hogar);
        tags.Should().Contain(CategoriaProducto.Ropa);
    }

    #endregion

    #region List of GeoLocation

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfGeoLocations_ReturnsGeoLocationArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrgeo");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con ubicaciones",
            Precio = 600m,
            Categoria = CategoriaProducto.Alimentos,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Principal",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            Ubicaciones =
            [
                new GeoLocation(40.4168, -3.7038),   // Madrid
                new GeoLocation(41.3851, 2.1734),    // Barcelona
                new GeoLocation(37.3891, -5.9845)    // Sevilla
            ]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.Ubicaciones)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var ubicaciones = results[0];
        ubicaciones.Should().HaveCount(3);
        ubicaciones.Should().Contain(u => u.Latitude == 40.4168 && u.Longitude == -3.7038);
        ubicaciones.Should().Contain(u => u.Latitude == 41.3851 && u.Longitude == 2.1734);
        ubicaciones.Should().Contain(u => u.Latitude == 37.3891 && u.Longitude == -5.9845);
    }

    #endregion

    #region List of ComplexType

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_ListOfComplexTypes_ReturnsComplexTypeArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrct");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con direcciones de entrega",
            Precio = 700m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Principal",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            DireccionesEntrega =
            [
                new Direccion
                {
                    Calle = "Gran Via 123",
                    Ciudad = "Madrid",
                    CodigoPostal = "28013",
                    Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.71) }
                },
                new Direccion
                {
                    Calle = "Paseo de Gracia 50",
                    Ciudad = "Barcelona",
                    CodigoPostal = "08007",
                    Coordenadas = new Coordenadas { Altitud = 12, Posicion = new GeoLocation(41.39, 2.17) }
                }
            ]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .Select(p => p.DireccionesEntrega)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var direcciones = results[0];
        direcciones.Should().HaveCount(2);
        direcciones.Should().Contain(d => d.Ciudad == "Madrid" && d.Calle == "Gran Via 123");
        direcciones.Should().Contain(d => d.Ciudad == "Barcelona" && d.Calle == "Paseo de Gracia 50");
    }

    #endregion

    #region Projections with Arrays in Anonymous Types

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_AnonymousTypeWithMultipleArrays_ReturnsAllArrays()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrmulti");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto completo",
            Precio = 800m,
            Categoria = CategoriaProducto.Hogar,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Test",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            Etiquetas = ["tag1", "tag2"],
            Cantidades = [5, 10],
            Precios = [9.99m, 19.99m],
            Tags = [CategoriaProducto.Electronica, CategoriaProducto.Ropa]
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
                p.Etiquetas,
                p.Cantidades,
                p.Precios,
                p.Tags
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Producto completo");
        result.Etiquetas.Should().BeEquivalentTo(["tag1", "tag2"]);
        result.Cantidades.Should().BeEquivalentTo([5, 10]);
        result.Precios.Should().BeEquivalentTo([9.99m, 19.99m]);
        result.Tags.Should().BeEquivalentTo([CategoriaProducto.Electronica, CategoriaProducto.Ropa]);
    }

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_AnonymousTypeWithGeoLocationArray_ReturnsGeoLocations()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrgeoano");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto geo",
            Precio = 900m,
            Categoria = CategoriaProducto.Alimentos,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Test",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            Ubicaciones =
            [
                new GeoLocation(40.0, -3.0),
                new GeoLocation(41.0, 2.0)
            ]
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
                p.Ubicaciones
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Producto geo");
        result.Ubicaciones.Should().HaveCount(2);
        result.Ubicaciones.Should().Contain(u => u.Latitude == 40.0);
        result.Ubicaciones.Should().Contain(u => u.Latitude == 41.0);
    }

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_AnonymousTypeWithComplexTypeArray_ReturnsComplexTypes()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrctano");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto con entregas",
            Precio = 1000m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Principal",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            DireccionesEntrega =
            [
                new Direccion
                {
                    Calle = "Entrega 1",
                    Ciudad = "Sevilla",
                    CodigoPostal = "41001",
                    Coordenadas = new Coordenadas { Altitud = 7, Posicion = new GeoLocation(37.39, -5.98) }
                },
                new Direccion
                {
                    Calle = "Entrega 2",
                    Ciudad = "Valencia",
                    CodigoPostal = "46001",
                    Coordenadas = new Coordenadas { Altitud = 15, Posicion = new GeoLocation(39.47, -0.38) }
                }
            ]
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
                p.DireccionesEntrega
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Producto con entregas");
        result.DireccionesEntrega.Should().HaveCount(2);
        result.DireccionesEntrega.Should().Contain(d => d.Ciudad == "Sevilla");
        result.DireccionesEntrega.Should().Contain(d => d.Ciudad == "Valencia");
    }

    #endregion

    #region Complete Entity with Arrays

    [Fact(Skip = "Pendiente: implementar ValueObjectList() para List<ComplexType> y List<GeoLocation>")]
    public async Task Select_CompleteEntityWithAllArrayTypes_ReturnsAllData()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueId = FirestoreTestFixture.GenerateId("arrcomplete");
        var producto = new ProductoCompleto
        {
            Id = uniqueId,
            Nombre = "Producto super completo",
            Precio = 1500m,
            Categoria = CategoriaProducto.Electronica,
            Ubicacion = new GeoLocation(40.4168, -3.7038),
            Direccion = new Direccion
            {
                Calle = "Calle Principal",
                Ciudad = "Madrid",
                CodigoPostal = "28001",
                Coordenadas = new Coordenadas { Altitud = 650, Posicion = new GeoLocation(40.42, -3.70) }
            },
            // All array types populated
            Etiquetas = ["premium", "nuevo"],
            Cantidades = [100, 200, 300],
            Pesos = [1.5, 2.5],
            Precios = [99.99m, 199.99m],
            Tags = [CategoriaProducto.Electronica, CategoriaProducto.Hogar],
            Ubicaciones =
            [
                new GeoLocation(40.0, -3.0),
                new GeoLocation(41.0, 2.0),
                new GeoLocation(37.0, -6.0)
            ],
            DireccionesEntrega =
            [
                new Direccion
                {
                    Calle = "Entrega Madrid",
                    Ciudad = "Madrid",
                    CodigoPostal = "28002",
                    Coordenadas = new Coordenadas { Altitud = 660, Posicion = new GeoLocation(40.43, -3.71) }
                }
            ]
        };

        context.ProductosCompletos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.ProductosCompletos
            .Where(p => p.Id == uniqueId)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];

        result.Nombre.Should().Be("Producto super completo");
        result.Precio.Should().Be(1500m);

        // Primitives arrays
        result.Etiquetas.Should().BeEquivalentTo(["premium", "nuevo"]);
        result.Cantidades.Should().BeEquivalentTo([100, 200, 300]);
        result.Pesos.Should().BeEquivalentTo([1.5, 2.5]);
        result.Precios.Should().BeEquivalentTo([99.99m, 199.99m]);

        // Enum array
        result.Tags.Should().BeEquivalentTo([CategoriaProducto.Electronica, CategoriaProducto.Hogar]);

        // GeoLocation array
        result.Ubicaciones.Should().HaveCount(3);

        // ComplexType array
        result.DireccionesEntrega.Should().HaveCount(1);
        result.DireccionesEntrega[0].Ciudad.Should().Be("Madrid");
    }

    #endregion
}
