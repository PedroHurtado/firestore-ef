using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where equality operator (==).
/// Each test uses a single == filter with unique values per type.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereEqualTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereEqualTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Where_Equal_String_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var targetName = $"StringTest-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = targetName },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = targetName },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "Other" }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Name == targetName)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Name.Should().Be(targetName));
    }

    [Fact]
    public async Task Where_Equal_Int_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueQuantity = new Random().Next(100000, 999999);
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "IntTest1", Quantity = uniqueQuantity },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "IntTest2", Quantity = uniqueQuantity },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "IntTest3", Quantity = 0 }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity == uniqueQuantity)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Quantity.Should().Be(uniqueQuantity));
    }

    [Fact]
    public async Task Where_Equal_Decimal_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniquePrice = 12345.67m + new Random().Next(1, 1000);
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "DecTest1", Price = uniquePrice },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "DecTest2", Price = uniquePrice },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("qry"), Name = "DecTest3", Price = 0m }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price == uniquePrice)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Price.Should().Be(uniquePrice));
    }

    [Fact]
    public async Task Where_Equal_Bool_True_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"BoolTrueTest-{Guid.NewGuid():N}";
        var entity = new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("qry"),
            Name = uniqueName,
            IsActive = true
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.IsActive == true)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == uniqueName);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task Where_Equal_Bool_False_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"BoolFalseTest-{Guid.NewGuid():N}";
        var entity = new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("qry"),
            Name = uniqueName,
            IsActive = false
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.IsActive == false)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == uniqueName);
        results.Should().AllSatisfy(e => e.IsActive.Should().BeFalse());
    }

    [Fact]
    public async Task Where_Equal_Enum_ReturnsMatches()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var uniqueName = $"EnumTest-{Guid.NewGuid():N}";
        var entity = new QueryTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("qry"),
            Name = uniqueName,
            Category = Category.Electronics
        };

        context.QueryTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Category == Category.Electronics)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Name == uniqueName);
        results.Should().AllSatisfy(e => e.Category.Should().Be(Category.Electronics));
    }
}
