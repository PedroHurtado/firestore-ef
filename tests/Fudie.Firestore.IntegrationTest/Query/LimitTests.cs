using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Limit operators.
/// Ciclo 22: Take (Limit)
/// Ciclo 23: First / FirstOrDefault
/// Ciclo 24: Single / SingleOrDefault
/// Ciclo 25: Skip (documentar ineficiencia)
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class LimitTests
{
    private readonly FirestoreTestFixture _fixture;

    public LimitTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 22: Take (Limit)

    [Fact]
    public async Task Take_ReturnsLimitedResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Take-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-4", Quantity = 40, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-5", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .Take(3)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Quantity.Should().Be(10);
        results[1].Quantity.Should().Be(20);
        results[2].Quantity.Should().Be(30);
    }

    [Fact]
    public async Task Take_WithLargerLimitThanResults_ReturnsAllResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"TakeLarge-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-1", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-2", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .Take(100)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Take_One_ReturnsSingleResult()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"TakeOne-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "First", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Second", Quantity = 20, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .Take(1)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("First");
    }

    #endregion

    #region Ciclo 23: First / FirstOrDefault

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsFirstEntity()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"First-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Second", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "First", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Third", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var result = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("First");
        result.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatches_ReturnsNull()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"NonExistent-{Guid.NewGuid():N}";

        // Act
        var result = await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .FirstOrDefaultAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithPredicate_ReturnsMatchingEntity()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"FirstPred-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Low", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "High", Quantity = 100, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Mid", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var result = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .FirstOrDefaultAsync(e => e.Quantity > 50);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("High");
    }

    #endregion

    #region Ciclo 24: Single / SingleOrDefault

    [Fact]
    public async Task SingleOrDefaultAsync_WithOneMatch_ReturnsEntity()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Single-{Guid.NewGuid():N}";
        var entity = new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("limit"),
            Name = "OnlyOne",
            TenantId = uniqueTenant
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var result = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .SingleOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("OnlyOne");
    }

    [Fact]
    public async Task SingleOrDefaultAsync_WithNoMatches_ReturnsNull()
    {
        // Arrange
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var nonExistentTenant = $"SingleNone-{Guid.NewGuid():N}";

        // Act
        var result = await readContext.QueryTestEntities
            .Where(e => e.TenantId == nonExistentTenant)
            .SingleOrDefaultAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SingleOrDefaultAsync_WithMultipleMatches_ThrowsException()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SingleMulti-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "First", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Second", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act & Assert
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var act = async () => await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .SingleOrDefaultAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Ciclo 25: Skip (documentar ineficiencia)

    /// <summary>
    /// Skip in Firestore is inefficient because Firestore doesn't support offset-based pagination.
    /// The implementation reads all documents up to the skip point and discards them.
    /// For efficient pagination, use cursor-based pagination with StartAfter/StartAt.
    /// </summary>
    [Fact]
    public async Task Skip_ReturnsResultsAfterSkippedCount()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Skip-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-4", Quantity = 40, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-5", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .Skip(2)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Quantity.Should().Be(30);
        results[1].Quantity.Should().Be(40);
        results[2].Quantity.Should().Be(50);
    }

    [Fact]
    public async Task Skip_WithTake_ReturnsPaginatedResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"SkipTake-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-4", Quantity = 40, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("limit"), Name = "Entity-5", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Get page 2 (items 3-4) with page size 2
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .Skip(2)
            .Take(2)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].Quantity.Should().Be(30);
        results[1].Quantity.Should().Be(40);
    }

    #endregion
}
