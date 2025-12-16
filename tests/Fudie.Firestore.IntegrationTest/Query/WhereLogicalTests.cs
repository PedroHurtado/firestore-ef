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

    #region ID + Filters (Multi-tenancy)

    [Fact]
    public async Task Where_IdAndTenantId_ReturnsSecureMatch()
    {
        // Arrange - Multi-tenancy scenario: Id + TenantId for secure access
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var targetId = FirestoreTestFixture.GenerateId("tenant");
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";

        var entities = new[]
        {
            new QueryTestEntity { Id = targetId, Name = "Shared-Resource", TenantId = tenantA },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tenant"), Name = "Other-Resource", TenantId = tenantA },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("tenant"), Name = "Different-Tenant", TenantId = tenantB }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Query by Id AND TenantId (secure multi-tenant access)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Id == targetId && e.TenantId == tenantA)
            .ToListAsync();

        // Assert - Should return exactly one entity matching both Id AND TenantId
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(targetId);
        results[0].TenantId.Should().Be(tenantA);
        results[0].Name.Should().Be("Shared-Resource");
    }

    [Fact]
    public async Task Where_IdAndTenantId_WrongTenant_ReturnsEmpty()
    {
        // Arrange - Security test: requesting wrong tenant should return nothing
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var targetId = FirestoreTestFixture.GenerateId("sec");
        var correctTenant = $"correct-{Guid.NewGuid():N}";
        var wrongTenant = $"wrong-{Guid.NewGuid():N}";

        var entity = new QueryTestEntity
        {
            Id = targetId,
            Name = "Secure-Resource",
            TenantId = correctTenant
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Try to access with wrong TenantId
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Id == targetId && e.TenantId == wrongTenant)
            .ToListAsync();

        // Assert - Should return empty (secure access denied)
        results.Should().BeEmpty();
    }

    #endregion

    #region AND + OR Nested (Optional Filters)

    [Fact]
    public async Task Where_AndWithNestedOr_OptionalFilter_WithValue()
    {
        // Arrange - Optional filter pattern: A && (param == null || x.Field == param)
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrefix = $"OptFilter-{Guid.NewGuid():N}";
        var targetName = $"{uniquePrefix}-Target";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = targetName, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = $"{uniquePrefix}-Other", IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = targetName, IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter with optional name filter (name has value)
        string? nameFilter = targetName;
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.IsActive == true && (nameFilter == null || e.Name == nameFilter))
            .ToListAsync();

        // Assert - Should return only active entity with target name
        results.Should().HaveCount(1);
        results[0].Name.Should().Be(targetName);
        results[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Where_AndWithNestedOr_OptionalFilter_WithNull()
    {
        // Arrange - When optional param is null, the OR condition (null || ...) is always true
        // so only the AND condition matters
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"OptNull-{Guid.NewGuid():N}";
        var nameA = $"OptNullA-{Guid.NewGuid():N}";
        var nameB = $"OptNullB-{Guid.NewGuid():N}";
        var nameC = $"OptNullC-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = nameA, TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = nameB, TenantId = uniqueTenant, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("opt"), Name = nameC, TenantId = uniqueTenant, IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter with null optional filter (should return all active with this tenant)
        string? nameFilter = null;
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.IsActive == true && (nameFilter == null || e.Name == nameFilter))
            .ToListAsync();

        // Assert - Should return all active entities (2 of 3)
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task Where_AndWithNestedOr_ComplexCondition()
    {
        // Arrange - Complex: A && (B || C) where B and C are different fields
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueTenant = $"Complex-{Guid.NewGuid():N}";
        var uniquePrice = 77777.77m + new Random().Next(1, 1000);
        var entities = new[]
        {
            // Matches: TenantId=unique AND (Price>50000 OR IsActive)
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("cplx"), Name = "Match-HighPrice", TenantId = uniqueTenant, Price = uniquePrice, IsActive = false },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("cplx"), Name = "Match-Active", TenantId = uniqueTenant, Price = 100m, IsActive = true },
            // Does not match: TenantId=unique but neither condition
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("cplx"), Name = "NoMatch", TenantId = uniqueTenant, Price = 100m, IsActive = false },
            // Does not match: wrong tenant
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("cplx"), Name = "WrongTenant", TenantId = "other", Price = uniquePrice, IsActive = true }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - TenantId == unique && (Price > 50000 || IsActive)
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.TenantId == uniqueTenant && (e.Price > 50000m || e.IsActive == true))
            .ToListAsync();

        // Assert - Should return 2 entities
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Match-HighPrice");
        results.Should().Contain(e => e.Name == "Match-Active");
    }

    #endregion
}
