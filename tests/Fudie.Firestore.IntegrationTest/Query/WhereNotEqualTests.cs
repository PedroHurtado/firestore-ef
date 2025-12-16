using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where not equal operator (!=).
/// Each test uses a single != filter with unique values per type.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereNotEqualTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereNotEqualTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Where_NotEqual_String_ExcludesMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var excludeName = $"Exclude-{Guid.NewGuid():N}";
        var includeName = $"Include-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = excludeName },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = includeName }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name != excludeName)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == includeName);
        results.Should().NotContain(e => e.Name == excludeName);
    }

    [Fact]
    public async Task Where_NotEqual_Int_ExcludesMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var excludeQuantity = new Random().Next(100000, 999999);
        var includeQuantity = excludeQuantity + 1;
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "IntNotEq1", Quantity = excludeQuantity },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "IntNotEq2", Quantity = includeQuantity }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity != excludeQuantity)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Quantity == includeQuantity);
        results.Should().NotContain(e => e.Quantity == excludeQuantity);
    }

    [Fact]
    public async Task Where_NotEqual_Decimal_ExcludesMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var excludePrice = 99999.99m + new Random().Next(1, 1000);
        var includePrice = excludePrice + 100m;
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "DecNotEq1", Price = excludePrice },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "DecNotEq2", Price = includePrice }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price != excludePrice)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Price == includePrice);
        results.Should().NotContain(e => e.Price == excludePrice);
    }

    [Fact]
    public async Task Where_NotEqual_Bool_ExcludesMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var activeName = $"Active-{Guid.NewGuid():N}";
        var inactiveName = $"Inactive-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = activeName, IsActive = true },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = inactiveName, IsActive = false }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.IsActive != true)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == inactiveName);
        results.Should().NotContain(e => e.Name == activeName);
    }

    [Fact]
    public async Task Where_NotEqual_Enum_ExcludesMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var electronicsName = $"Electronics-{Guid.NewGuid():N}";
        var clothingName = $"Clothing-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = electronicsName, Category = Category.Electronics },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = clothingName, Category = Category.Clothing }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Category != Category.Electronics)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == clothingName);
        results.Should().NotContain(e => e.Name == electronicsName);
    }
}
