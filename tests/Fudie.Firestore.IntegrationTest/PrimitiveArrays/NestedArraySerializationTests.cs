using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.PrimitiveArrays;

/// <summary>
/// Tests de integración para verificar que List&lt;List&lt;T&gt;&gt; (arrays anidados)
/// se persisten correctamente en Firestore.
/// Firestore soporta arrays de arrays.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class NestedArraySerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public NestedArraySerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ========================================================================
    // LIST<LIST<STRING>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListString_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "StringMatrixTest",
            StringMatrix =
            [
                ["a", "b", "c"],
                ["d", "e"],
                ["f", "g", "h", "i"]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.StringMatrix.Should().HaveCount(3);
        retrieved.StringMatrix[0].Should().ContainInOrder("a", "b", "c");
        retrieved.StringMatrix[1].Should().ContainInOrder("d", "e");
        retrieved.StringMatrix[2].Should().ContainInOrder("f", "g", "h", "i");
    }

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListString_WithEmptyInnerArrays_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "EmptyInnerTest",
            StringMatrix =
            [
                ["a", "b"],
                [],  // Empty inner array
                ["c"]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.StringMatrix.Should().HaveCount(3);
        retrieved.StringMatrix[0].Should().HaveCount(2);
        retrieved.StringMatrix[1].Should().BeEmpty();
        retrieved.StringMatrix[2].Should().HaveCount(1);
    }

    // ========================================================================
    // LIST<LIST<INT>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListInt_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "NumberMatrixTest",
            NumberMatrix =
            [
                [1, 2, 3],
                [4, 5],
                [6, 7, 8, 9]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.NumberMatrix.Should().HaveCount(3);
        retrieved.NumberMatrix[0].Should().ContainInOrder(1, 2, 3);
        retrieved.NumberMatrix[1].Should().ContainInOrder(4, 5);
        retrieved.NumberMatrix[2].Should().ContainInOrder(6, 7, 8, 9);
    }

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListInt_SingleRow_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "SingleRowTest",
            NumberMatrix =
            [
                [100, 200, 300]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.NumberMatrix.Should().HaveCount(1);
        retrieved.NumberMatrix[0].Should().ContainInOrder(100, 200, 300);
    }

    // ========================================================================
    // LIST<LIST<LONG>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListLong_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "LongMatrixTest",
            LongMatrix =
            [
                [100L, 200L, 300L],
                [400L, 500L]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.LongMatrix.Should().HaveCount(2);
        retrieved.LongMatrix[0].Should().ContainInOrder(100L, 200L, 300L);
        retrieved.LongMatrix[1].Should().ContainInOrder(400L, 500L);
    }

    // ========================================================================
    // LIST<LIST<DOUBLE>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListDouble_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "DoubleMatrixTest",
            DoubleMatrix =
            [
                [1.5, 2.5, 3.5],
                [4.5, 5.5]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.DoubleMatrix.Should().HaveCount(2);
        retrieved.DoubleMatrix[0].Should().ContainInOrder(1.5, 2.5, 3.5);
        retrieved.DoubleMatrix[1].Should().ContainInOrder(4.5, 5.5);
    }

    // ========================================================================
    // LIST<LIST<DECIMAL>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListDecimal_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "DecimalMatrixTest",
            DecimalMatrix =
            [
                [10.99m, 20.99m],
                [30.99m, 40.99m, 50.99m]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.DecimalMatrix.Should().HaveCount(2);
        retrieved.DecimalMatrix[0].Should().ContainInOrder(10.99m, 20.99m);
        retrieved.DecimalMatrix[1].Should().ContainInOrder(30.99m, 40.99m, 50.99m);
    }

    // ========================================================================
    // LIST<LIST<BOOL>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListBool_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "BoolMatrixTest",
            BoolMatrix =
            [
                [true, false, true],
                [false, false]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.BoolMatrix.Should().HaveCount(2);
        retrieved.BoolMatrix[0].Should().ContainInOrder(true, false, true);
        retrieved.BoolMatrix[1].Should().ContainInOrder(false, false);
    }

    // ========================================================================
    // LIST<LIST<DATETIME>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListDateTime_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var date1 = new DateTime(2024, 1, 1, 0, 0, 0);
        var date2 = new DateTime(2024, 6, 15, 0, 0, 0);
        var date3 = new DateTime(2024, 12, 31, 0, 0, 0);

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "DateTimeMatrixTest",
            DateTimeMatrix =
            [
                [date1, date2],
                [date3]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.DateTimeMatrix.Should().HaveCount(2);
        retrieved.DateTimeMatrix[0].Should().ContainInOrder(date1, date2);
        retrieved.DateTimeMatrix[1].Should().ContainInOrder(date3);
    }

    // ========================================================================
    // LIST<LIST<ENUM>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListEnum_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "EnumMatrixTest",
            EnumMatrix =
            [
                [Priority.Low, Priority.High],
                [Priority.Critical, Priority.Medium, Priority.Low]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.EnumMatrix.Should().HaveCount(2);
        retrieved.EnumMatrix[0].Should().ContainInOrder(Priority.Low, Priority.High);
        retrieved.EnumMatrix[1].Should().ContainInOrder(Priority.Critical, Priority.Medium, Priority.Low);
    }

    // ========================================================================
    // LIST<LIST<GUID>>
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task ListListGuid_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "GuidMatrixTest",
            GuidMatrix =
            [
                [guid1, guid2],
                [guid3]
            ]
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.GuidMatrix.Should().HaveCount(2);
        retrieved.GuidMatrix[0].Should().ContainInOrder(guid1, guid2);
        retrieved.GuidMatrix[1].Should().ContainInOrder(guid3);
    }

    // ========================================================================
    // EMPTY NESTED ARRAYS
    // ========================================================================

    [Fact(Skip = "Firestore does not support nested arrays")]
    public async Task EmptyNestedArrays_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("nested");
        using var context = _fixture.CreateContext<NestedArrayTestDbContext>();

        var entity = new NestedArrayEntity
        {
            Id = id,
            Name = "EmptyNestedTest"
            // Both matrices default to empty []
        };

        context.NestedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<NestedArrayTestDbContext>();
        var retrieved = await readContext.NestedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.StringMatrix.Should().BeEmpty();
        retrieved.NumberMatrix.Should().BeEmpty();
    }
}
