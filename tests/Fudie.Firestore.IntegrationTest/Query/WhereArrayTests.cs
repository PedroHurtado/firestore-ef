using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where Array operators.
/// Ciclo 16: ArrayContains (array.Contains(value))
/// Ciclo 17: ArrayContainsAny (array.Any(x => list.Contains(x)))
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereArrayTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereArrayTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 16: ArrayContains (array.Contains(value))

    [Fact]
    public async Task Where_ArrayContains_ReturnsEntitiesWithValueInArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTag = $"unique-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-A", Tags = ["tag1", "tag2", uniqueTag] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-B", Tags = ["tag3", "tag4"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-C", Tags = [uniqueTag, "tag5"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-D", Tags = ["tag6"] }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Tags contains the unique tag (simple filter, no AND)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Tags.Contains(uniqueTag))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Entity-A");
        results.Should().Contain(e => e.Name == "Entity-C");
        results.Should().NotContain(e => e.Name == "Entity-B");
        results.Should().NotContain(e => e.Name == "Entity-D");
    }

    [Fact]
    public async Task Where_ArrayContains_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ArrayNoMatch-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-A", TenantId = uniqueTenant, Tags = ["tag1", "tag2"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-B", TenantId = uniqueTenant, Tags = ["tag3"] }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Tags contains a value that doesn't exist
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Tags.Contains("nonexistent"))
            .ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Where_ArrayContains_WithConstantValue_Works()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ArrayConst-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-A", TenantId = uniqueTenant, Tags = ["premium", "featured"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-B", TenantId = uniqueTenant, Tags = ["standard"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-C", TenantId = uniqueTenant, Tags = ["premium"] }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter using a constant (inline string)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Tags.Contains("premium"))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Entity-A");
        results.Should().Contain(e => e.Name == "Entity-C");
    }

    #endregion

    #region Ciclo 17: ArrayContainsAny

    [Fact]
    public async Task Where_ArrayContainsAny_ReturnsEntitiesWithAnyValueInArray()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ArrayAny-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-A", TenantId = uniqueTenant, Tags = ["red", "blue"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-B", TenantId = uniqueTenant, Tags = ["green", "yellow"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-C", TenantId = uniqueTenant, Tags = ["blue", "purple"] },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-D", TenantId = uniqueTenant, Tags = ["orange"] }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Tags contains any of ["red", "purple"]
        var searchTags = new[] { "red", "purple" };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Tags.Any(t => searchTags.Contains(t)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Entity-A");  // Has "red"
        results.Should().Contain(e => e.Name == "Entity-C");  // Has "purple"
        results.Should().NotContain(e => e.Name == "Entity-B");
        results.Should().NotContain(e => e.Name == "Entity-D");
    }

    [Fact]
    public async Task Where_ArrayContainsAny_WithEmptySearchList_ReturnsEmpty()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"ArrayEmpty-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("array"), Name = "Entity-A", TenantId = uniqueTenant, Tags = ["tag1"] }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter with empty search list - this might throw or return empty
        var searchTags = Array.Empty<string>();
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();

        // Note: Firestore requires at least 1 element for ArrayContainsAny
        // This test documents the expected behavior
        var act = async () => await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Tags.Any(t => searchTags.Contains(t)))
            .ToListAsync();

        // Firestore should throw for empty array in ArrayContainsAny
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion
}
