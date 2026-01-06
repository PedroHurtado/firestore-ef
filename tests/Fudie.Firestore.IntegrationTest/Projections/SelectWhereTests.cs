using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Projections;

/// <summary>
/// Integration tests for Select (projection) combined with Where, OrderBy, Take.
/// Fase 3: Combinación Where + Select
/// Ciclo 7: Where + Select campos
/// Ciclo 8: Where + Select a DTO
/// Ciclo 9: Where + OrderBy + Select
/// Ciclo 10: Where + OrderBy + Take + Select
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SelectWhereTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectWhereTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 7: Where + Select campos

    [Fact]
    public async Task Where_Select_SingleField_ReturnsFilteredProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereSelect-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Producto Activo 1",
                Quantity = 10,
                Price = 100m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Producto Inactivo",
                Quantity = 20,
                Price = 200m,
                IsActive = false,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Producto Activo 2",
                Quantity = 30,
                Price = 300m,
                IsActive = true,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.IsActive == true)
            .Select(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("Producto Activo 1");
        results.Should().Contain("Producto Activo 2");
        results.Should().NotContain("Producto Inactivo");
    }

    [Fact]
    public async Task Where_Select_AnonymousType_ReturnsFilteredProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereSelectAnon-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Barato",
                Quantity = 5,
                Price = 50m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Caro",
                Quantity = 10,
                Price = 500m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Medio",
                Quantity = 15,
                Price = 150m,
                Category = Category.Clothing,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Price > 100m)
            .Select(e => new { e.Name, e.Price })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Name == "Caro" && r.Price == 500m);
        results.Should().Contain(r => r.Name == "Medio" && r.Price == 150m);
    }

    #endregion

    #region Ciclo 8: Where + Select a DTO

    [Fact]
    public async Task Where_Select_ToDto_ReturnsFilteredMappedResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereSelectDto-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Electrónico A",
                Quantity = 10,
                Price = 299.99m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Ropa A",
                Quantity = 20,
                Price = 49.99m,
                Category = Category.Clothing,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Electrónico B",
                Quantity = 5,
                Price = 599.99m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category == Category.Electronics)
            .Select(e => new QueryTestReadDto
            {
                Id = e.Id!,
                Name = e.Name,
                Price = e.Price
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Id.Should().NotBeNullOrEmpty());
        results.Should().Contain(r => r.Name == "Electrónico A" && r.Price == 299.99m);
        results.Should().Contain(r => r.Name == "Electrónico B" && r.Price == 599.99m);
    }

    [Fact]
    public async Task Where_Select_ToRecord_ReturnsFilteredMappedResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereSelectRec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Comida A",
                Quantity = 100,
                Price = 5.99m,
                Category = Category.Food,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Hogar A",
                Quantity = 10,
                Price = 25.99m,
                Category = Category.Home,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheresel"),
                Name = "Comida B",
                Quantity = 50,
                Price = 12.99m,
                Category = Category.Food,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category == Category.Food)
            .Select(e => new QueryTestReadRecord(e.Id!, e.Name, e.Price))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Id.Should().NotBeNullOrEmpty());
        results.Should().Contain(r => r.Name == "Comida A" && r.Price == 5.99m);
        results.Should().Contain(r => r.Name == "Comida B" && r.Price == 12.99m);
    }

    #endregion

    #region Ciclo 9: Where + OrderBy + Select

    [Fact]
    public async Task Where_OrderBy_Select_ReturnsFilteredOrderedProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereOrderSelect-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Zebra",
                Quantity = 10,
                Price = 100m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Apple",
                Quantity = 20,
                Price = 200m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Mango",
                Quantity = 30,
                Price = 300m,
                IsActive = false,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Banana",
                Quantity = 40,
                Price = 400m,
                IsActive = true,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.IsActive == true)
            .OrderBy(e => e.Name)
            .Select(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Be("Apple");
        results[1].Should().Be("Banana");
        results[2].Should().Be("Zebra");
    }

    [Fact]
    public async Task Where_OrderByDescending_Select_ReturnsFilteredOrderedProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereOrderDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Cheap",
                Quantity = 10,
                Price = 10m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Expensive",
                Quantity = 5,
                Price = 1000m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereord"),
                Name = "Medium",
                Quantity = 15,
                Price = 100m,
                Category = Category.Electronics,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category == Category.Electronics)
            .OrderByDescending(e => e.Price)
            .Select(e => new { e.Name, e.Price })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Expensive");
        results[0].Price.Should().Be(1000m);
        results[1].Name.Should().Be("Medium");
        results[2].Name.Should().Be("Cheap");
    }

    #endregion

    #region Ciclo 10: Where + OrderBy + Take + Select

    [Fact]
    public async Task Where_OrderBy_Take_Select_ReturnsLimitedFilteredOrderedProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereTakeSelect-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheretake"),
                Name = "Item A",
                Quantity = 100,
                Price = 10m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheretake"),
                Name = "Item B",
                Quantity = 200,
                Price = 20m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheretake"),
                Name = "Item C",
                Quantity = 300,
                Price = 30m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheretake"),
                Name = "Item D",
                Quantity = 400,
                Price = 40m,
                IsActive = true,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("wheretake"),
                Name = "Item E",
                Quantity = 500,
                Price = 50m,
                IsActive = false,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.IsActive == true)
            .OrderBy(e => e.Price)
            .Take(2)
            .Select(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Item A");
        results[1].Should().Be("Item B");
    }

    [Fact]
    public async Task Where_OrderBy_Skip_Take_Select_ReturnsPaginatedProjection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"WhereSkipTake-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereskip"),
                Name = "Page1-A",
                Quantity = 1,
                Price = 10m,
                Category = Category.Food,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereskip"),
                Name = "Page1-B",
                Quantity = 2,
                Price = 20m,
                Category = Category.Food,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereskip"),
                Name = "Page2-A",
                Quantity = 3,
                Price = 30m,
                Category = Category.Food,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereskip"),
                Name = "Page2-B",
                Quantity = 4,
                Price = 40m,
                Category = Category.Food,
                TenantId = uniqueTenant
            },
            new QueryTestEntity
            {
                Id = FirestoreTestFixture.GenerateId("whereskip"),
                Name = "Page3-A",
                Quantity = 5,
                Price = 50m,
                Category = Category.Food,
                TenantId = uniqueTenant
            }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Get second page (skip 2, take 2)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category == Category.Food)
            .OrderBy(e => e.Price)
            .Skip(2)
            .Take(2)
            .Select(e => new { e.Name, e.Price })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Page2-A");
        results[0].Price.Should().Be(30m);
        results[1].Name.Should().Be("Page2-B");
        results[1].Price.Should().Be(40m);
    }

    #endregion
}
