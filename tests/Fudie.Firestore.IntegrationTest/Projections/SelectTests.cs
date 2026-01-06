using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Projections;

/// <summary>
/// Integration tests for Select (projection) operators.
/// Fase 1: Campos Simples
/// Ciclo 1: Select campo único
/// Ciclo 2: Select múltiples campos (tipo anónimo)
/// Ciclo 3: Select a DTO
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SelectTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 1: Select campo único

    [Fact]
    public async Task Select_SingleField_ReturnsOnlyThatField()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Select-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Producto A",
                Quantity = 10,
                Price = 99.99m,
                Description = "Descripción del producto A",
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Producto B",
                Quantity = 20,
                Price = 199.99m,
                Description = "Descripción del producto B",
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Producto C",
                Quantity = 30,
                Price = 299.99m,
                Description = "Descripción del producto C",
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .Select(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Be("Producto A");
        results[1].Should().Be("Producto B");
        results[2].Should().Be("Producto C");
    }

    #endregion

    #region Ciclo 2: Select múltiples campos (tipo anónimo)

    [Fact]
    public async Task Select_AnonymousType_ReturnsOnlySelectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SelectAnon-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Laptop",
                Quantity = 5,
                Price = 999.99m,
                Description = "Laptop gaming de alta gama",
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Mouse",
                Quantity = 50,
                Price = 29.99m,
                Description = "Mouse inalámbrico",
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Teclado",
                Quantity = 30,
                Price = 79.99m,
                Description = "Teclado mecánico RGB",
                Category = Category.Electronics,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Price })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);

        results[0].Name.Should().Be("Laptop");
        results[0].Price.Should().Be(999.99m);
        results[0].Id.Should().NotBeNullOrEmpty();

        results[1].Name.Should().Be("Mouse");
        results[1].Price.Should().Be(29.99m);

        results[2].Name.Should().Be("Teclado");
        results[2].Price.Should().Be(79.99m);
    }

    #endregion

    #region Ciclo 3: Select a DTO

    [Fact]
    public async Task Select_ToDto_MapsFieldsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SelectDto-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Camiseta",
                Quantity = 100,
                Price = 19.99m,
                Description = "Camiseta de algodón",
                Category = Category.Clothing,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Pantalón",
                Quantity = 50,
                Price = 49.99m,
                Description = "Pantalón vaquero",
                Category = Category.Clothing,
                IsActive = false,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .Select(e => new QueryTestReadDto
            {
                Id = e.Id!,
                Name = e.Name,
                Price = e.Price
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);

        results[0].Id.Should().NotBeNullOrEmpty();
        results[0].Name.Should().Be("Camiseta");
        results[0].Price.Should().Be(19.99m);

        results[1].Id.Should().NotBeNullOrEmpty();
        results[1].Name.Should().Be("Pantalón");
        results[1].Price.Should().Be(49.99m);
    }

    [Fact]
    public async Task Select_ToRecord_MapsFieldsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SelectRecord-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Manzana",
                Quantity = 200,
                Price = 1.50m,
                Description = "Manzana roja",
                Category = Category.Food,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("select"),
                Name = "Naranja",
                Quantity = 150,
                Price = 2.00m,
                Description = "Naranja de Valencia",
                Category = Category.Food,
                IsActive = true,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .Select(e => new QueryTestReadRecord(e.Id!, e.Name, e.Price))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);

        results[0].Id.Should().NotBeNullOrEmpty();
        results[0].Name.Should().Be("Manzana");
        results[0].Price.Should().Be(1.50m);

        results[1].Id.Should().NotBeNullOrEmpty();
        results[1].Name.Should().Be("Naranja");
        results[1].Price.Should().Be(2.00m);
    }

    #endregion
}

/// <summary>
/// DTO class for Select projection tests.
/// </summary>
public class QueryTestReadDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// DTO record for Select projection tests.
/// </summary>
public record QueryTestReadRecord(string Id, string Name, decimal Price);
