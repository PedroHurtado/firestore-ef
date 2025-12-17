using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for String operators (workarounds).
/// Ciclo 33: StartsWith (workaround using >= and <)
///
/// Firestore doesn't have native string operators like StartsWith, EndsWith, Contains.
/// StartsWith can be simulated using range queries:
///   field >= "prefix" AND field < "prefix\uffff"
/// or by incrementing the last character of the prefix.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class StringTests
{
    private readonly FirestoreTestFixture _fixture;

    public StringTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 33: StartsWith

    [Fact]
    public async Task StartsWith_ReturnsMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"StartsWith-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Alpha-One", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Alpha-Two", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Beta-One", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Gamma-One", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Alpha-Three", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Name.StartsWith("Alpha"))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(e => e.Name.StartsWith("Alpha"));
    }

    [Fact]
    public async Task StartsWith_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"StartsWithNone-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Beta-One", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Gamma-Two", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Name.StartsWith("Alpha"))
            .ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task StartsWith_CaseSensitive_OnlyMatchesExactCase()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"StartsWithCase-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Alpha-One", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "alpha-two", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "ALPHA-Three", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - StartsWith is case-sensitive
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Name.StartsWith("Alpha"))
            .ToListAsync();

        // Assert - Only "Alpha-One" matches (case-sensitive)
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alpha-One");
    }

    [Fact]
    public async Task StartsWith_WithSpecialCharacters_WorksCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"StartsWithSpecial-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Test-123-A", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Test-123-B", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Test-456-A", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("str"), Name = "Other-123", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Name.StartsWith("Test-123"))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => e.Name.StartsWith("Test-123"));
    }

    #endregion
}
