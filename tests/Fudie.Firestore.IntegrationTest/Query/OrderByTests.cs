using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for OrderBy operators.
/// Ciclo 18: OrderBy (ascending)
/// Ciclo 19: OrderByDescending
/// Ciclo 20: ThenBy (secondary sort)
/// Ciclo 21: ThenByDescending
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class OrderByTests
{
    private readonly FirestoreTestFixture _fixture;

    public OrderByTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 18: OrderBy (ascending)

    [Fact]
    public async Task OrderBy_String_ReturnsEntitiesSortedAscending()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OrderBy-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Charlie", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Alice");
        results[1].Name.Should().Be("Bob");
        results[2].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task OrderBy_Integer_ReturnsEntitiesSortedAscending()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OrderByInt-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "High", Quantity = 100, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Low", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Mid", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Low");    // Quantity = 10
        results[1].Name.Should().Be("Mid");    // Quantity = 50
        results[2].Name.Should().Be("High");   // Quantity = 100
    }

    [Fact]
    public async Task OrderBy_Decimal_ReturnsEntitiesSortedAscending()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OrderByDec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Expensive", Price = 99.99m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Cheap", Price = 9.99m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Medium", Price = 49.99m, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Price)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Cheap");     // Price = 9.99
        results[1].Name.Should().Be("Medium");    // Price = 49.99
        results[2].Name.Should().Be("Expensive"); // Price = 99.99
    }

    #endregion

    #region Ciclo 19: OrderByDescending

    [Fact]
    public async Task OrderByDescending_String_ReturnsEntitiesSortedDescending()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OrderByDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Charlie", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderByDescending(e => e.Name)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Charlie");
        results[1].Name.Should().Be("Bob");
        results[2].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task OrderByDescending_Integer_ReturnsEntitiesSortedDescending()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OrderByDescInt-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Low", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "High", Quantity = 100, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Mid", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderByDescending(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("High");   // Quantity = 100
        results[1].Name.Should().Be("Mid");    // Quantity = 50
        results[2].Name.Should().Be("Low");    // Quantity = 10
    }

    #endregion

    #region Ciclo 20: ThenBy (secondary sort)

    [Fact]
    public async Task ThenBy_SecondarySortAscending_SortsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ThenBy-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 5, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Order by Name ASC, then by Quantity ASC
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(4);
        results[0].Name.Should().Be("Alice");
        results[0].Quantity.Should().Be(10);  // Alice with lower quantity first
        results[1].Name.Should().Be("Alice");
        results[1].Quantity.Should().Be(20);  // Alice with higher quantity second
        results[2].Name.Should().Be("Bob");
        results[2].Quantity.Should().Be(5);   // Bob with lower quantity first
        results[3].Name.Should().Be("Bob");
        results[3].Quantity.Should().Be(30);  // Bob with higher quantity second
    }

    [Fact]
    public async Task ThenBy_AfterOrderByDescending_SortsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ThenByAfterDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Order by Name DESC, then by Quantity ASC
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderByDescending(e => e.Name)
            .ThenBy(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Bob");
        results[0].Quantity.Should().Be(30);
        results[1].Name.Should().Be("Alice");
        results[1].Quantity.Should().Be(10);  // Alice with lower quantity first
        results[2].Name.Should().Be("Alice");
        results[2].Quantity.Should().Be(20);  // Alice with higher quantity second
    }

    #endregion

    #region Ciclo 21: ThenByDescending

    [Fact]
    public async Task ThenByDescending_SecondarySortDescending_SortsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ThenByDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 5, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Order by Name ASC, then by Quantity DESC
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .ThenByDescending(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(4);
        results[0].Name.Should().Be("Alice");
        results[0].Quantity.Should().Be(20);  // Alice with higher quantity first
        results[1].Name.Should().Be("Alice");
        results[1].Quantity.Should().Be(10);  // Alice with lower quantity second
        results[2].Name.Should().Be("Bob");
        results[2].Quantity.Should().Be(30);  // Bob with higher quantity first
        results[3].Name.Should().Be("Bob");
        results[3].Quantity.Should().Be(5);   // Bob with lower quantity second
    }

    [Fact]
    public async Task ThenByDescending_AfterOrderByDescending_SortsCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ThenByDescAfterDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Alice", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("order"), Name = "Bob", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Order by Name DESC, then by Quantity DESC
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderByDescending(e => e.Name)
            .ThenByDescending(e => e.Quantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Bob");
        results[0].Quantity.Should().Be(30);
        results[1].Name.Should().Be("Alice");
        results[1].Quantity.Should().Be(20);  // Alice with higher quantity first
        results[2].Name.Should().Be("Alice");
        results[2].Quantity.Should().Be(10);  // Alice with lower quantity second
    }

    #endregion
}