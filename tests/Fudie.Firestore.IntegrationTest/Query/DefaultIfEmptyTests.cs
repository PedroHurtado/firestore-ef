using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for DefaultIfEmpty operator.
/// DefaultIfEmpty returns a collection with a single default value if the source is empty.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class DefaultIfEmptyTests
{
    private readonly FirestoreTestFixture _fixture;

    public DefaultIfEmptyTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region DefaultIfEmpty - Empty Source

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithEmptySource_ReturnsCollectionWithDefaultValue()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-Empty-{Guid.NewGuid():N}";
        // No entities added

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .DefaultIfEmpty()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeNull();
    }

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithEmptySourceAndDefaultValue_ReturnsCollectionWithSpecifiedDefault()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-Default-{Guid.NewGuid():N}";
        var defaultEntity = new QueryTestEntity { Id = "default", Name = "Default Entity", Quantity = 0, TenantId = uniqueTenant };
        // No entities added to database

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .DefaultIfEmpty(defaultEntity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().NotBeNull();
        results[0]!.Name.Should().Be("Default Entity");
    }

    #endregion

    #region DefaultIfEmpty - Non-Empty Source

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithNonEmptySource_ReturnsOriginalCollection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-NonEmpty-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .DefaultIfEmpty()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Should().NotBeNull());
    }

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithNonEmptySourceAndDefaultValue_ReturnsOriginalCollection()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-NonEmpty-Default-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant }
        };
        var defaultEntity = new QueryTestEntity { Id = "default", Name = "Default Entity", Quantity = 0, TenantId = uniqueTenant };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .DefaultIfEmpty(defaultEntity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().NotContain(e => e.Name == "Default Entity");
    }

    #endregion

    #region DefaultIfEmpty - Combined with Other Operators

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithOrderBy_ReturnsOrderedCollectionOrDefault()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-OrderBy-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-C", Quantity = 30, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-A", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-B", Quantity = 20, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Name)
            .DefaultIfEmpty()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0]!.Name.Should().Be("Entity-A");
        results[1]!.Name.Should().Be("Entity-B");
        results[2]!.Name.Should().Be("Entity-C");
    }

    [Fact(Skip = "DefaultIfEmpty not yet fully implemented in Executor - pending implementation")]
    public async Task DefaultIfEmpty_WithTake_ReturnsLimitedCollectionOrDefault()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"DefaultIfEmpty-Take-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-1", Quantity = 10, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-2", Quantity = 20, TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("default"), Name = "Entity-3", Quantity = 30, TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .OrderBy(e => e.Quantity)
            .Take(2)
            .DefaultIfEmpty()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
    }

    #endregion
}
