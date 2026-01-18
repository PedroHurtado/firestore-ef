using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.PrimitiveArrays;

/// <summary>
/// Tests de integración para verificar que List&lt;T&gt; de tipos primitivos
/// se persisten correctamente en Firestore y se pueden consultar con ArrayContains.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class PrimitiveArraySerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public PrimitiveArraySerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ========================================================================
    // LIST<STRING>
    // ========================================================================

    [Fact]
    public async Task ListString_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "StringTest",
            Tags = ["tag1", "tag2", "tag3"]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Tags.Should().HaveCount(3);
        retrieved.Tags.Should().ContainInOrder("tag1", "tag2", "tag3");
    }

    [Fact]
    public async Task ListString_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniquePrefix = $"contains-{Guid.NewGuid():N}";
        var uniqueTag = $"tag-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-A", Tags = ["common", uniqueTag] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-B", Tags = ["other"] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-C", Tags = [uniqueTag, "another"] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniquePrefix) && e.Tags.Contains(uniqueTag))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-A");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-C");
    }

    [Fact]
    public async Task ListString_ArrayNotContains_ShouldThrowNotSupportedException()
    {
        // Arrange
        var uniqueTag = $"tag-{Guid.NewGuid():N}";
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        // Act & Assert
        // Firestore does not support 'NOT array-contains' queries
        var act = async () => await readContext.PrimitiveArrays
            .Where(e => !e.Tags.Contains(uniqueTag))
            .ToListAsync();

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Firestore does not support 'NOT array-contains'*");
    }

    [Fact]
    public async Task ListString_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniquePrefix = $"containsany-{Guid.NewGuid():N}";
        var tag1 = $"tag1-{Guid.NewGuid():N}";
        var tag2 = $"tag2-{Guid.NewGuid():N}";
        var tag3 = $"tag3-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-A", Tags = [tag1, "other"] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-B", Tags = ["different"] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniquePrefix}-C", Tags = [tag2, tag3] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Tags contains any of [tag1, tag2]
        var searchTags = new[] { tag1, tag2 };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniquePrefix) && e.Tags.Any(t => searchTags.Contains(t)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-A");
        results.Should().Contain(e => e.Name == $"{uniquePrefix}-C");
    }

    // ========================================================================
    // LIST<INT>
    // ========================================================================

    [Fact]
    public async Task ListInt_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "IntTest",
            Quantities = [10, 20, 30, 40]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Quantities.Should().HaveCount(4);
        retrieved.Quantities.Should().ContainInOrder(10, 20, 30, 40);
    }

    [Fact]
    public async Task ListInt_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"intcontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Quantities = [100, 200, 999] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Quantities = [300, 400] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Quantities = [999, 500] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Quantities.Contains(999))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListInt_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"intcontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Quantities = [100, 200, 300] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Quantities = [400, 500] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Quantities = [600, 100] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Quantities contains any of [100, 600]
        var searchValues = new[] { 100, 600 };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Quantities.Any(q => searchValues.Contains(q)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<LONG>
    // ========================================================================

    [Fact]
    public async Task ListLong_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "LongTest",
            BigNumbers = [1000000000L, 2000000000L, 9999999999L]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.BigNumbers.Should().HaveCount(3);
        retrieved.BigNumbers.Should().Contain(9999999999L);
    }

    [Fact]
    public async Task ListLong_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"longcontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", BigNumbers = [100L, 200L, 999L] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", BigNumbers = [300L, 400L] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", BigNumbers = [999L, 500L] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.BigNumbers.Contains(999L))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListLong_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"longcontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", BigNumbers = [100L, 200L, 300L] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", BigNumbers = [400L, 500L] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", BigNumbers = [600L, 100L] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where BigNumbers contains any of [100L, 600L]
        var searchValues = new[] { 100L, 600L };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.BigNumbers.Any(n => searchValues.Contains(n)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<DOUBLE>
    // ========================================================================

    [Fact]
    public async Task ListDouble_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "DoubleTest",
            Measurements = [1.5, 2.75, 3.14159]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Measurements.Should().HaveCount(3);
        retrieved.Measurements[0].Should().BeApproximately(1.5, 0.001);
        retrieved.Measurements[2].Should().BeApproximately(3.14159, 0.00001);
    }

    [Fact]
    public async Task ListDouble_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"doublecontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Measurements = [1.5, 2.5, 9.99] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Measurements = [3.5, 4.5] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Measurements = [9.99, 5.5] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Measurements.Contains(9.99))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListDouble_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"doublecontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Measurements = [1.5, 2.5, 3.5] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Measurements = [4.5, 5.5] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Measurements = [6.5, 1.5] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Measurements contains any of [1.5, 6.5]
        var searchValues = new[] { 1.5, 6.5 };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Measurements.Any(m => searchValues.Contains(m)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<DECIMAL> (se convierte a double en Firestore)
    // ========================================================================

    [Fact]
    public async Task ListDecimal_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "DecimalTest",
            Prices = [19.99m, 29.99m, 99.95m]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Prices.Should().HaveCount(3);
        retrieved.Prices[0].Should().Be(19.99m);
        retrieved.Prices[1].Should().Be(29.99m);
        retrieved.Prices[2].Should().Be(99.95m);
    }

    [Fact]
    public async Task ListDecimal_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"decimalcontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Prices = [10.50m, 20.50m, 99.99m] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Prices = [30.50m, 40.50m] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Prices = [99.99m, 50.50m] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Prices.Contains(99.99m))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListDecimal_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"decimalcontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Prices = [10.50m, 20.50m, 30.50m] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Prices = [40.50m, 50.50m] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Prices = [60.50m, 10.50m] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Prices contains any of [10.50m, 60.50m]
        var searchValues = new[] { 10.50m, 60.50m };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Prices.Any(p => searchValues.Contains(p)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<BOOL>
    // ========================================================================

    [Fact]
    public async Task ListBool_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "BoolTest",
            Flags = [true, false, true, true]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Flags.Should().HaveCount(4);
        retrieved.Flags.Should().ContainInOrder(true, false, true, true);
    }

    [Fact]
    public async Task ListBool_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"boolcontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Flags = [true, true] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Flags = [false, false] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Flags = [true, false] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Flags contains false
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Flags.Contains(false))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-B");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListBool_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"boolcontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Flags = [true, true] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Flags = [false, false] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Flags = [true, false] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Flags contains any of [false]
        // All entities should match except A which only has true values
        var searchValues = new[] { false };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Flags.Any(f => searchValues.Contains(f)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-B");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<DATETIME> (se convierte a Timestamp en Firestore)
    // ========================================================================

    [Fact]
    public async Task ListDateTime_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var dates = new[]
        {
            new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 20, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "DateTimeTest",
            EventDates = dates.ToList()
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.EventDates.Should().HaveCount(3);
        retrieved.EventDates[0].Should().Be(dates[0]);
        retrieved.EventDates[1].Should().Be(dates[1]);
        retrieved.EventDates[2].Should().Be(dates[2]);
    }

    [Fact]
    public async Task ListDateTime_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"datecontains-{Guid.NewGuid():N}";
        var targetDate = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", EventDates = [new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), targetDate] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", EventDates = [new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", EventDates = [targetDate, new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc)] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.EventDates.Contains(targetDate))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListDateTime_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"datecontainsany-{Guid.NewGuid():N}";
        var date1 = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var date3 = new DateTime(2024, 12, 15, 12, 0, 0, DateTimeKind.Utc);
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", EventDates = [date1, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", EventDates = [new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", EventDates = [date2, date3] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where EventDates contains any of [date1, date2]
        var searchValues = new[] { date1, date2 };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.EventDates.Any(d => searchValues.Contains(d)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<ENUM> (se convierte a List<string> en Firestore)
    // ========================================================================

    [Fact]
    public async Task ListEnum_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "EnumTest",
            Priorities = [Priority.Low, Priority.High, Priority.Critical]
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Priorities.Should().HaveCount(3);
        retrieved.Priorities.Should().ContainInOrder(Priority.Low, Priority.High, Priority.Critical);
    }

    [Fact]
    public async Task ListEnum_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"enumcontains-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Priorities = [Priority.Low, Priority.Medium] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Priorities = [Priority.Critical] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Priorities = [Priority.High, Priority.Critical] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Priorities contains Critical
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Priorities.Contains(Priority.Critical))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-B");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListEnum_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"enumcontainsany-{Guid.NewGuid():N}";
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", Priorities = [Priority.Low, Priority.Medium] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", Priorities = [Priority.High] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", Priorities = [Priority.Critical, Priority.Low] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where Priorities contains any of [Low, Critical]
        var searchValues = new[] { Priority.Low, Priority.Critical };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.Priorities.Any(p => searchValues.Contains(p)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // LIST<GUID> (se convierte a List<string> en Firestore)
    // ========================================================================

    [Fact]
    public async Task ListGuid_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        var guids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "GuidTest",
            ExternalIds = guids.ToList()
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ExternalIds.Should().HaveCount(3);
        retrieved.ExternalIds.Should().ContainInOrder(guids);
    }

    [Fact]
    public async Task ListGuid_ArrayContains_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"guidcontains-{Guid.NewGuid():N}";
        var targetGuid = Guid.NewGuid();
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", ExternalIds = [Guid.NewGuid(), targetGuid] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", ExternalIds = [Guid.NewGuid()] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", ExternalIds = [targetGuid, Guid.NewGuid()] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.ExternalIds.Contains(targetGuid))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    [Fact]
    public async Task ListGuid_ArrayContainsAny_ShouldFindMatches()
    {
        // Arrange
        var uniqueName = $"guidcontainsany-{Guid.NewGuid():N}";
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entities = new[]
        {
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-A", ExternalIds = [guid1, Guid.NewGuid()] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-B", ExternalIds = [Guid.NewGuid()] },
            new PrimitiveArrayEntity { Id = FirestoreTestFixture.GenerateId("prim"), Name = $"{uniqueName}-C", ExternalIds = [guid2, guid3] }
        };

        context.PrimitiveArrays.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Find entities where ExternalIds contains any of [guid1, guid2]
        var searchValues = new[] { guid1, guid2 };
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var results = await readContext.PrimitiveArrays
            .Where(e => e.Name.StartsWith(uniqueName) && e.ExternalIds.Any(g => searchValues.Contains(g)))
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == $"{uniqueName}-A");
        results.Should().Contain(e => e.Name == $"{uniqueName}-C");
    }

    // ========================================================================
    // EMPTY ARRAYS
    // ========================================================================

    [Fact]
    public async Task EmptyArrays_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("prim");
        using var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>();

        var entity = new PrimitiveArrayEntity
        {
            Id = id,
            Name = "EmptyTest"
            // All arrays default to empty []
        };

        context.PrimitiveArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>();
        var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Tags.Should().BeEmpty();
        retrieved.Quantities.Should().BeEmpty();
        retrieved.Prices.Should().BeEmpty();
        retrieved.Flags.Should().BeEmpty();
        retrieved.Priorities.Should().BeEmpty();
    }
}
