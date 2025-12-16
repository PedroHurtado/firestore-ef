using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where logical operators (AND, OR).
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereLogicalTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereLogicalTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region AND (&&)

    [Fact]
    public async Task Where_And_TwoConditions_ReturnsBothMatch()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"AndTest-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 100, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 100, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 50, IsActive = true }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == uniqueName && e.Quantity == 100)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e =>
        {
            e.Name.Should().Be(uniqueName);
            e.Quantity.Should().Be(100);
        });
    }

    [Fact]
    public async Task Where_And_ThreeConditions_ReturnsAllMatch()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"And3Test-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 100, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 100, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Quantity = 50, IsActive = true }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == uniqueName && e.Quantity == 100 && e.IsActive == true)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be(uniqueName);
        results[0].Quantity.Should().Be(100);
        results[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Where_And_WithComparison_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"AndCompTest-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Price = 50m },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Price = 150m },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("and"), Name = uniqueName, Price = 250m }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == uniqueName && e.Price > 100m)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Price.Should().BeGreaterThan(100m));
    }

    #endregion

    #region OR (||)

    [Fact]
    public async Task Where_Or_TwoConditions_ReturnsEitherMatch()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var name1 = $"OrTest1-{Guid.NewGuid():N}";
        var name2 = $"OrTest2-{Guid.NewGuid():N}";
        var name3 = $"OrTest3-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = name1 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = name2 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = name3 }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == name1 || e.Name == name2)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == name1);
        results.Should().Contain(e => e.Name == name2);
        results.Should().NotContain(e => e.Name == name3);
    }

    [Fact]
    public async Task Where_Or_DifferentFields_ReturnsEitherMatch()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrice = 99999.99m + new Random().Next(1, 1000);
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = "OrFields-A", Price = uniquePrice, Quantity = 100, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = "OrFields-B", Price = uniquePrice, Quantity = 50, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("or"), Name = "OrFields-C", Price = uniquePrice, Quantity = 50, IsActive = true }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by Price (first Where = AND) then apply OR on different fields (second Where)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price == uniquePrice)
            .Where(e => e.Quantity == 100 || e.IsActive == false)
            .ToListAsync();

        // Assert - Should get A (Quantity=100) and B (IsActive=false)
        results.Should().HaveCount(2);
    }

    #endregion
}
