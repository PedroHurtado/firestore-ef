using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Aggregation operators.
/// Ciclo 26: Count
/// Ciclo 27: Any
/// Ciclo 28: Sum
/// Ciclo 29: Average
/// Ciclo 30: Min (client-side)
/// Ciclo 31: Max (client-side)
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class AggregationTests
{
    private readonly FirestoreTestFixture _fixture;

    public AggregationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 26: Count

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Count-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var count = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .CountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_WithNoMatches_ReturnsZero()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"CountNone-{Guid.NewGuid():N}";

        // Act
        var count = await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .CountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task CountAsync_WithPredicate_ReturnsFilteredCount()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"CountPred-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Low", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "High-1", Quantity = 100, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "High-2", Quantity = 150, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Mid", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var count = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .CountAsync(e => e.Quantity > 50);

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region Ciclo 27: Any

    [Fact]
    public async Task AnyAsync_WithMatches_ReturnsTrue()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Any-{Guid.NewGuid():N}";
        var entity = new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("agg"),
            Name = "Existing",
            TenantId = uniqueTenant
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var exists = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .AnyAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithNoMatches_ReturnsFalse()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"AnyNone-{Guid.NewGuid():N}";

        // Act
        var exists = await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .AnyAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AnyAsync_WithPredicate_ReturnsCorrectResult()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"AnyPred-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Low", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Mid", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var hasHigh = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .AnyAsync(e => e.Quantity > 100);

        var hasMid = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .AnyAsync(e => e.Quantity >= 50);

        // Assert
        hasHigh.Should().BeFalse();
        hasMid.Should().BeTrue();
    }

    #endregion

    #region Ciclo 28: Sum

    [Fact]
    public async Task SumAsync_ReturnsCorrectSum()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Sum-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var sum = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .SumAsync(e => e.Quantity);

        // Assert
        sum.Should().Be(60);
    }

    [Fact]
    public async Task SumAsync_WithNoMatches_ReturnsZero()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"SumNone-{Guid.NewGuid():N}";

        // Act
        var sum = await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .SumAsync(e => e.Quantity);

        // Assert
        sum.Should().Be(0);
    }

    [Fact]
    public async Task SumAsync_WithDecimal_ReturnsCorrectSum()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SumDec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Price = 10.50m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Price = 20.25m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Price = 30.75m, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var sum = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .SumAsync(e => e.Price);

        // Assert
        sum.Should().Be(61.50m);
    }

    #endregion

    #region Ciclo 29: Average

    [Fact]
    public async Task AverageAsync_ReturnsCorrectAverage()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Avg-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var average = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .AverageAsync(e => e.Quantity);

        // Assert
        average.Should().Be(20);
    }

    [Fact]
    public async Task AverageAsync_WithDecimal_ReturnsCorrectAverage()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"AvgDec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Price = 10.00m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Price = 20.00m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Price = 30.00m, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var average = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .AverageAsync(e => e.Price);

        // Assert
        average.Should().Be(20.00m);
    }

    [Fact]
    public async Task AverageAsync_WithNoMatches_ThrowsException()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"AvgNone-{Guid.NewGuid():N}";

        // Act & Assert
        // Average on empty sequence throws InvalidOperationException
        var act = async () => await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .AverageAsync(e => e.Quantity);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Ciclo 30: Min (client-side)

    /// <summary>
    /// Min is evaluated client-side because Firestore doesn't support native Min aggregation.
    /// The implementation fetches matching documents and calculates Min locally.
    /// </summary>
    [Fact]
    public async Task MinAsync_ReturnsCorrectMin()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Min-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Quantity = 50, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var min = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .MinAsync(e => e.Quantity);

        // Assert
        min.Should().Be(10);
    }

    [Fact]
    public async Task MinAsync_WithDecimal_ReturnsCorrectMin()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"MinDec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Price = 50.99m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Price = 10.50m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Price = 30.75m, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var min = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .MinAsync(e => e.Price);

        // Assert
        min.Should().Be(10.50m);
    }

    [Fact]
    public async Task MinAsync_WithNoMatches_ThrowsException()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"MinNone-{Guid.NewGuid():N}";

        // Act & Assert
        // Min on empty sequence throws InvalidOperationException
        var act = async () => await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .MinAsync(e => e.Quantity);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Ciclo 31: Max (client-side)

    /// <summary>
    /// Max is evaluated client-side because Firestore doesn't support native Max aggregation.
    /// The implementation fetches matching documents and calculates Max locally.
    /// </summary>
    [Fact]
    public async Task MaxAsync_ReturnsCorrectMax()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Max-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Quantity = 50, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Quantity = 100, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var max = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .MaxAsync(e => e.Quantity);

        // Assert
        max.Should().Be(100);
    }

    [Fact]
    public async Task MaxAsync_WithDecimal_ReturnsCorrectMax()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"MaxDec-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-1", Price = 50.99m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-2", Price = 10.50m, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("agg"), Name = "Entity-3", Price = 99.99m, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var max = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .MaxAsync(e => e.Price);

        // Assert
        max.Should().Be(99.99m);
    }

    [Fact]
    public async Task MaxAsync_WithNoMatches_ThrowsException()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"MaxNone-{Guid.NewGuid():N}";

        // Act & Assert
        // Max on empty sequence throws InvalidOperationException
        var act = async () => await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .MaxAsync(e => e.Quantity);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion
}