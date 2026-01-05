using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where clauses with DateTime comparisons.
/// Verifies that Timestamp → DateTime conversion works correctly in both:
/// - Resolver (for query parameters)
/// - Deserializer (for result values)
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereDateTimeTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereDateTimeTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region DateTime Comparisons

    [Fact]
    public async Task Where_DateTime_GreaterThan_ReturnsNewerEntities()
    {
        // Arrange - Use fixed dates to avoid microsecond timing issues
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var baseDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var past = baseDate.AddDays(-10);
        var middle = baseDate;
        var future = baseDate.AddDays(10);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Past", CreatedAt = past },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Middle", CreatedAt = middle },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Future", CreatedAt = future }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by Name prefix to isolate this test's data
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt > middle)
            .ToListAsync();

        // Assert
        results.Should().ContainSingle();
        results.Should().Contain(e => e.Name == $"{testPrefix}-Future");
    }

    [Fact]
    public async Task Where_DateTime_GreaterThanOrEqual_ReturnsMatchingAndNewerEntities()
    {
        // Arrange - Use fixed dates to avoid microsecond timing issues
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var baseDate = new DateTime(2024, 2, 15, 12, 0, 0, DateTimeKind.Utc);
        var past = baseDate.AddDays(-10);
        var middle = baseDate;
        var future = baseDate.AddDays(10);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Past", CreatedAt = past },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Middle", CreatedAt = middle },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Future", CreatedAt = future }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt >= middle)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{testPrefix}-Future");
        results.Should().Contain(e => e.Name == $"{testPrefix}-Middle");
    }

    [Fact]
    public async Task Where_DateTime_LessThan_ReturnsOlderEntities()
    {
        // Arrange - Use fixed dates to avoid microsecond timing issues
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var baseDate = new DateTime(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var past = baseDate.AddDays(-10);
        var middle = baseDate;
        var future = baseDate.AddDays(10);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Past", CreatedAt = past },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Middle", CreatedAt = middle },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Future", CreatedAt = future }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt < middle)
            .ToListAsync();

        // Assert
        results.Should().ContainSingle();
        results.Should().Contain(e => e.Name == $"{testPrefix}-Past");
    }

    [Fact]
    public async Task Where_DateTime_LessThanOrEqual_ReturnsMatchingAndOlderEntities()
    {
        // Arrange - Use fixed dates to avoid microsecond timing issues
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var baseDate = new DateTime(2024, 4, 15, 12, 0, 0, DateTimeKind.Utc);
        var past = baseDate.AddDays(-10);
        var middle = baseDate;
        var future = baseDate.AddDays(10);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Past", CreatedAt = past },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Middle", CreatedAt = middle },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Future", CreatedAt = future }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt <= middle)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{testPrefix}-Past");
        results.Should().Contain(e => e.Name == $"{testPrefix}-Middle");
    }

    #endregion

    #region DateTime Range Queries

    [Fact]
    public async Task Where_DateTime_Between_ReturnsEntitiesInRange()
    {
        // Arrange - Use fixed dates to avoid microsecond timing issues
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var baseDate = new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc);
        var veryOld = baseDate.AddDays(-30);
        var old = baseDate.AddDays(-10);
        var recent = baseDate.AddDays(-5);
        var future = baseDate.AddDays(10);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-VeryOld", CreatedAt = veryOld },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Old", CreatedAt = old },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Recent", CreatedAt = recent },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Future", CreatedAt = future }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Query entities between old and baseDate (exclusive)
        var startDate = old;
        var endDate = baseDate;

        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt >= startDate && e.CreatedAt < endDate)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{testPrefix}-Old");
        results.Should().Contain(e => e.Name == $"{testPrefix}-Recent");
    }

    #endregion

    #region DateTime with OrderBy

    [Fact]
    public async Task Where_DateTime_WithOrderBy_ReturnsOrderedResults()
    {
        // Arrange
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;
        var dates = new[]
        {
            now.AddDays(-5),
            now.AddDays(-1),
            now.AddDays(-10),
            now.AddDays(-3)
        };

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = dates.Select((d, i) => new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("dt"),
            Name = $"{testPrefix}-{i}",
            CreatedAt = d
        }).ToArray();

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        var threshold = now.AddDays(-7);
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt > threshold)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        // Assert - Should return entities newer than threshold, ordered by date
        results.Should().HaveCount(3); // -5, -1, -3 days (all > -7)
        results.Should().BeInAscendingOrder(e => e.CreatedAt);
    }

    [Fact]
    public async Task Where_DateTime_WithOrderByDescending_ReturnsOrderedResults()
    {
        // Arrange
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-1", CreatedAt = now.AddDays(-5) },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-2", CreatedAt = now.AddDays(-1) },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-3", CreatedAt = now.AddDays(-3) }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        var threshold = now.AddDays(-7);
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt > threshold)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeInDescendingOrder(e => e.CreatedAt);
    }

    #endregion

    #region DateTime Equality (exact match)

    [Fact]
    public async Task Where_DateTime_Equals_ReturnsExactMatch()
    {
        // Arrange - Use a specific timestamp to ensure exact match
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var specificDate = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Before", CreatedAt = specificDate.AddSeconds(-1) },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Exact", CreatedAt = specificDate },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-After", CreatedAt = specificDate.AddSeconds(1) }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt == specificDate)
            .ToListAsync();

        // Assert
        results.Should().ContainSingle(e => e.Name == $"{testPrefix}-Exact");
    }

    #endregion

    #region DateTime with Variable Capture

    [Fact]
    public async Task Where_DateTime_WithCapturedVariable_Works()
    {
        // Arrange
        var testPrefix = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Old", CreatedAt = now.AddDays(-10) },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("dt"), Name = $"{testPrefix}-Recent", CreatedAt = now.AddDays(-1) }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Use captured variable (common pattern in real code)
        var cutoffDate = now.AddDays(-5);
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(testPrefix) && e.CreatedAt > cutoffDate)
            .ToListAsync();

        // Assert
        results.Should().ContainSingle();
        results.Should().Contain(e => e.Name == $"{testPrefix}-Recent");
    }

    #endregion
}