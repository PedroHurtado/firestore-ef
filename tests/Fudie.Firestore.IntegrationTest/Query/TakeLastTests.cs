using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for TakeLast operator (Firestore LimitToLast).
/// Ciclo 32: TakeLast
///
/// Note: TakeLast requires an OrderBy clause to work correctly.
/// Firestore's LimitToLast returns the last N documents based on the query's ordering.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class TakeLastTests
{
    private readonly FirestoreTestFixture _fixture;

    public TakeLastTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 32: TakeLast

    [Fact]
    public async Task TakeLast_WithOrderBy_ReturnsLastNDocuments()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"TakeLast-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-A", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-B", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-C", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-D", Quantity = 40, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-E", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Get last 2 entities ordered by Quantity ascending
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .TakeLast(2)
            .ToListAsync();

        // Assert - Should get entities with Quantity 40 and 50 (last 2 in ascending order)
        results.Should().HaveCount(2);
        results.Select(e => e.Quantity).Should().BeEquivalentTo(new[] { 40, 50 });
    }

    [Fact]
    public async Task TakeLast_WithOrderByDescending_ReturnsLastNDocuments()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"TakeLastDesc-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-A", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-B", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-C", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-D", Quantity = 40, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-E", Quantity = 50, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Get last 3 entities ordered by Quantity descending
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderByDescending(e => e.Quantity)
            .TakeLast(3)
            .ToListAsync();

        // Assert - Should get entities with Quantity 30, 20, 10 (last 3 in descending order)
        results.Should().HaveCount(3);
        results.Select(e => e.Quantity).Should().BeEquivalentTo(new[] { 30, 20, 10 });
    }

    [Fact]
    public async Task TakeLast_WithFewerDocumentsThanRequested_ReturnsAllDocuments()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"TakeLastFewer-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-A", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tl"), Name = "Entity-B", Quantity = 20, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Request last 10, but only 2 exist
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .TakeLast(10)
            .ToListAsync();

        // Assert - Should return all 2 documents
        results.Should().HaveCount(2);
    }

    #endregion
}
