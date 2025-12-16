using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where IN and NOT IN operators.
/// Ciclo 10: IN (list.Contains(field))
/// Ciclo 11: NOT IN (!list.Contains(field))
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereInTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereInTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 10: IN (list.Contains(field))

    [Fact]
    public async Task Where_In_StringList_ReturnsMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"In-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = $"{uniquePrefix}-A" },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = $"{uniquePrefix}-B" },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = $"{uniquePrefix}-C" },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = $"{uniquePrefix}-D" }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Name is in list [A, C]
        var targetNames = new List<string> { $"{uniquePrefix}-A", $"{uniquePrefix}-C" };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => targetNames.Contains(e.Name))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-A");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-C");
        results.Should().NotContain(e => e.Name == $"{uniquePrefix}-B");
        results.Should().NotContain(e => e.Name == $"{uniquePrefix}-D");
    }

    [Fact]
    public async Task Where_In_IntList_ReturnsMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"InInt-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = uniqueName, Quantity = 10 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = uniqueName, Quantity = 20 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = uniqueName, Quantity = 30 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = uniqueName, Quantity = 40 }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Quantity is in list [10, 30]
        var targetQuantities = new List<int> { 10, 30 };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == uniqueName && targetQuantities.Contains(e.Quantity))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Quantity == 10);
        results.Should().Contain(e => e.Quantity == 30);
    }

    [Fact]
    public async Task Where_In_WithOtherConditions_ReturnsFilteredResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"InTenant-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = "Target-A", TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = "Target-B", TenantId = uniqueTenant, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = "Target-C", TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("in"), Name = "Other", TenantId = "other-tenant", IsActive = true }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Combine IN with other conditions
        var targetNames = new List<string> { "Target-A", "Target-B", "Target-C" };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && targetNames.Contains(e.Name) && e.IsActive == true)
            .ToListAsync();

        // Assert - Only active targets in the tenant
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Target-A");
        results.Should().Contain(e => e.Name == "Target-C");
    }

    #endregion

    #region Ciclo 11: NOT IN (!list.Contains(field))

    [Fact]
    public async Task Where_NotIn_StringList_ExcludesMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"NotIn-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = "Keep-A", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = "Exclude-B", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = "Keep-C", TenantId = uniqueTenant },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = "Exclude-D", TenantId = uniqueTenant }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where Name is NOT in exclusion list
        var excludeNames = new List<string> { "Exclude-B", "Exclude-D" };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && !excludeNames.Contains(e.Name))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Keep-A");
        results.Should().Contain(e => e.Name == "Keep-C");
        results.Should().NotContain(e => e.Name == "Exclude-B");
        results.Should().NotContain(e => e.Name == "Exclude-D");
    }

    [Fact]
    public async Task Where_NotIn_IntList_ExcludesMatchingEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"NotInInt-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = uniqueName, Quantity = 100 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = uniqueName, Quantity = 200 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = uniqueName, Quantity = 300 },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("notin"), Name = uniqueName, Quantity = 400 }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Exclude quantities 200 and 400
        var excludeQuantities = new List<int> { 200, 400 };
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == uniqueName && !excludeQuantities.Contains(e.Quantity))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Quantity == 100);
        results.Should().Contain(e => e.Quantity == 300);
    }

    #endregion
}
