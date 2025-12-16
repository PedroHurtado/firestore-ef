using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Where comparison operators (>, >=, &lt;, &lt;=).
/// Uses Theory with InlineData to test multiple scenarios.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class WhereComparisonTests
{
    private readonly FirestoreTestFixture _fixture;

    public WhereComparisonTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region GreaterThan (>)

    [Theory]
    [InlineData(50, 100, 150)]  // threshold=100, expects only 150
    [InlineData(10, 20, 30)]
    public async Task Where_GreaterThan_Int_ReturnsValuesAboveThreshold(int low, int threshold, int high)
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-Low", Quantity = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-Mid", Quantity = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-High", Quantity = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity > threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Quantity == high);
        results.Should().NotContain(e => e.Quantity == threshold);
        results.Should().NotContain(e => e.Quantity == low);
    }

    [Theory]
    [InlineData(50.5, 100.5, 150.5)]
    [InlineData(10.99, 20.99, 30.99)]
    public async Task Where_GreaterThan_Decimal_ReturnsValuesAboveThreshold(
        double lowD, double thresholdD, double highD)
    {
        var low = (decimal)lowD;
        var threshold = (decimal)thresholdD;
        var high = (decimal)highD;

        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-Low", Price = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-Mid", Price = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gt"), Name = "GT-High", Price = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price > threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Price == high);
        results.Should().NotContain(e => e.Price == threshold);
        results.Should().NotContain(e => e.Price == low);
    }

    #endregion

    #region GreaterThanOrEqual (>=)

    [Theory]
    [InlineData(50, 100, 150)]
    [InlineData(10, 20, 30)]
    public async Task Where_GreaterThanOrEqual_Int_ReturnsValuesAtOrAboveThreshold(int low, int threshold, int high)
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-Low", Quantity = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-Mid", Quantity = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-High", Quantity = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity >= threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Quantity == high);
        results.Should().Contain(e => e.Quantity == threshold);
        results.Should().NotContain(e => e.Quantity == low);
    }

    [Theory]
    [InlineData(50.5, 100.5, 150.5)]
    [InlineData(10.99, 20.99, 30.99)]
    public async Task Where_GreaterThanOrEqual_Decimal_ReturnsValuesAtOrAboveThreshold(
        double lowD, double thresholdD, double highD)
    {
        var low = (decimal)lowD;
        var threshold = (decimal)thresholdD;
        var high = (decimal)highD;

        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-Low", Price = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-Mid", Price = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("gte"), Name = "GTE-High", Price = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price >= threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Price == high);
        results.Should().Contain(e => e.Price == threshold);
        results.Should().NotContain(e => e.Price == low);
    }

    #endregion

    #region LessThan (<)

    [Theory]
    [InlineData(50, 100, 150)]
    [InlineData(10, 20, 30)]
    public async Task Where_LessThan_Int_ReturnsValuesBelowThreshold(int low, int threshold, int high)
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-Low", Quantity = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-Mid", Quantity = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-High", Quantity = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity < threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Quantity == low);
        results.Should().NotContain(e => e.Quantity == threshold);
        results.Should().NotContain(e => e.Quantity == high);
    }

    [Theory]
    [InlineData(50.5, 100.5, 150.5)]
    [InlineData(10.99, 20.99, 30.99)]
    public async Task Where_LessThan_Decimal_ReturnsValuesBelowThreshold(
        double lowD, double thresholdD, double highD)
    {
        var low = (decimal)lowD;
        var threshold = (decimal)thresholdD;
        var high = (decimal)highD;

        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-Low", Price = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-Mid", Price = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lt"), Name = "LT-High", Price = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price < threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Price == low);
        results.Should().NotContain(e => e.Price == threshold);
        results.Should().NotContain(e => e.Price == high);
    }

    #endregion

    #region LessThanOrEqual (<=)

    [Theory]
    [InlineData(50, 100, 150)]
    [InlineData(10, 20, 30)]
    public async Task Where_LessThanOrEqual_Int_ReturnsValuesAtOrBelowThreshold(int low, int threshold, int high)
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-Low", Quantity = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-Mid", Quantity = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-High", Quantity = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Quantity <= threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Quantity == low);
        results.Should().Contain(e => e.Quantity == threshold);
        results.Should().NotContain(e => e.Quantity == high);
    }

    [Theory]
    [InlineData(50.5, 100.5, 150.5)]
    [InlineData(10.99, 20.99, 30.99)]
    public async Task Where_LessThanOrEqual_Decimal_ReturnsValuesAtOrBelowThreshold(
        double lowD, double thresholdD, double highD)
    {
        var low = (decimal)lowD;
        var threshold = (decimal)thresholdD;
        var high = (decimal)highD;

        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();
        var entities = new[]
        {
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-Low", Price = low },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-Mid", Price = threshold },
            new QueryTestEntity { Id = FirestoreTestFixture.GenerateId("lte"), Name = "LTE-High", Price = high }
        };

        context.QueryTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<QueryTestDbContext>();
        var results = await readContext.QueryTestEntities
            .Where(e => e.Price <= threshold)
            .ToListAsync();

        // Assert
        results.Should().Contain(e => e.Price == low);
        results.Should().Contain(e => e.Price == threshold);
        results.Should().NotContain(e => e.Price == high);
    }

    #endregion
}
