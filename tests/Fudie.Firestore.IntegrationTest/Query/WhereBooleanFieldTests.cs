using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where clause with boolean and enum field expressions.
/// Tests simple boolean property access without explicit comparison operators,
/// and enum field filtering with == and != operators.
///
/// These tests cover:
/// - .Where(e => e.IsActive) - implicit true comparison
/// - .Where(e => !e.IsActive) - negation (implicit false comparison)
/// - .Where(e => e.Category == Category.Electronics) - enum equality
/// - .Where(e => e.Category != Category.Electronics) - enum inequality
/// - Combined boolean expressions with other filters
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereBooleanFieldTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereBooleanFieldTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Simple Boolean Property Access

    [Fact]
    public async Task Where_BooleanProperty_ImplicitTrue_ReturnsActiveEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"BoolImplicitTrue-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Active1", IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Active2", IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Inactive", IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - This is the case that currently fails: e.IsActive (without == true)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(uniquePrefix))
            .Where(e => e.IsActive)  // <-- Simple boolean property access
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-Active1");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-Active2");
        results.Should().NotContain(e => e.Name == $"{uniquePrefix}-Inactive");
    }

    [Fact]
    public async Task Where_BooleanProperty_Negated_ReturnsInactiveEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"BoolNegated-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Active", IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Inactive1", IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Inactive2", IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Negated boolean: !e.IsActive
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(uniquePrefix))
            .Where(e => !e.IsActive)  // <-- Negated boolean property
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeFalse());
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-Inactive1");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-Inactive2");
        results.Should().NotContain(e => e.Name == $"{uniquePrefix}-Active");
    }

    #endregion

    #region Boolean Combined with Other Filters

    [Fact]
    public async Task Where_BooleanProperty_CombinedWithStringFilter_ReturnsCorrectEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"BoolCombined-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Match", IsActive = true, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Match", IsActive = false, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-Other", IsActive = true, Category = Category.Clothing }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Boolean combined with category filter
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(uniquePrefix) && e.IsActive && e.Category == Category.Electronics)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be($"{uniquePrefix}-Match");
        results.First().IsActive.Should().BeTrue();
        results.First().Category.Should().Be(Category.Electronics);
    }

    [Fact]
    public async Task Where_BooleanProperty_InLogicalOr_ReturnsCorrectEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"BoolOr-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-ActiveElectronics", IsActive = true, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-InactiveClothing", IsActive = false, Category = Category.Clothing },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = $"{uniquePrefix}-InactiveFood", IsActive = false, Category = Category.Food }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Boolean in OR condition: active OR clothing category
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name.StartsWith(uniquePrefix) && (e.IsActive || e.Category == Category.Clothing))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-ActiveElectronics");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-InactiveClothing");
        results.Should().NotContain(e => e.Name == $"{uniquePrefix}-InactiveFood");
    }

    #endregion

    #region Boolean as Single Filter

    [Fact]
    public async Task Where_OnlyBooleanProperty_ReturnsAllActiveEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OnlyBool-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity1", TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity2", TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity3", TenantId = uniqueTenant, IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - First filter by TenantId, then by boolean only
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .Where(e => e.IsActive)  // <-- Only boolean filter
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task Where_OnlyNegatedBooleanProperty_ReturnsAllInactiveEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OnlyNegBool-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity1", TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity2", TenantId = uniqueTenant, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Entity3", TenantId = uniqueTenant, IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - First filter by TenantId, then by negated boolean
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .Where(e => !e.IsActive)  // <-- Negated boolean filter
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeFalse());
    }

    #endregion

    #region Enum Field as Single Filter

    [Fact]
    public async Task Where_EnumProperty_Equal_ReturnsMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"EnumEqual-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Electronics1", TenantId = uniqueTenant, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Electronics2", TenantId = uniqueTenant, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Clothing1", TenantId = uniqueTenant, Category = Category.Clothing },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Food1", TenantId = uniqueTenant, Category = Category.Food }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by enum equality only
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .Where(e => e.Category == Category.Electronics)  // <-- Enum equality filter
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Category.Should().Be(Category.Electronics));
        results.Should().Contain(e => e.Name == "Electronics1");
        results.Should().Contain(e => e.Name == "Electronics2");
    }

    [Fact]
    public async Task Where_EnumProperty_NotEqual_ReturnsNonMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"EnumNotEqual-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Electronics1", TenantId = uniqueTenant, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Clothing1", TenantId = uniqueTenant, Category = Category.Clothing },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Food1", TenantId = uniqueTenant, Category = Category.Food },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Home1", TenantId = uniqueTenant, Category = Category.Home }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter by enum inequality only
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant)
            .Where(e => e.Category != Category.Electronics)  // <-- Enum inequality filter
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(e => e.Category.Should().NotBe(Category.Electronics));
        results.Should().Contain(e => e.Name == "Clothing1");
        results.Should().Contain(e => e.Name == "Food1");
        results.Should().Contain(e => e.Name == "Home1");
        results.Should().NotContain(e => e.Name == "Electronics1");
    }

    [Fact]
    public async Task Where_EnumProperty_Equal_InSingleWhereClause_ReturnsMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"EnumSingleWhere-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Clothing1", TenantId = uniqueTenant, Category = Category.Clothing },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Clothing2", TenantId = uniqueTenant, Category = Category.Clothing },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Food1", TenantId = uniqueTenant, Category = Category.Food }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Combined filter in single Where clause
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category == Category.Clothing)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Category.Should().Be(Category.Clothing));
    }

    [Fact]
    public async Task Where_EnumProperty_NotEqual_InSingleWhereClause_ReturnsNonMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"EnumNotEqSingle-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Food1", TenantId = uniqueTenant, Category = Category.Food },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Food2", TenantId = uniqueTenant, Category = Category.Food },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Home1", TenantId = uniqueTenant, Category = Category.Home }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Combined filter in single Where clause with inequality
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.Category != Category.Food)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("Home1");
        results.First().Category.Should().Be(Category.Home);
    }

    #endregion
}
