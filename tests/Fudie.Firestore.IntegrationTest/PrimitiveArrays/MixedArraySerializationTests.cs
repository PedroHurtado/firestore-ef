using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.PrimitiveArrays;

/// <summary>
/// Tests de integración para verificar que List&lt;object&gt; (arrays mixtos)
/// se persisten correctamente en Firestore.
/// Firestore permite arrays con elementos de distintos tipos.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class MixedArraySerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public MixedArraySerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListObject_MixedTypes_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed");
        using var context = _fixture.CreateContext<MixedArrayTestDbContext>();

        var entity = new MixedArrayEntity
        {
            Id = id,
            Name = "MixedTest",
            MixedValues = ["string-value", 42, 3.14, true, "another-string"]
        };

        context.MixedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<MixedArrayTestDbContext>();
        var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MixedValues.Should().HaveCount(5);
        // Note: Firestore stores numbers as double, so we check types accordingly
        retrieved.MixedValues[0].Should().Be("string-value");
        retrieved.MixedValues[3].Should().Be(true);
        retrieved.MixedValues[4].Should().Be("another-string");
    }

    [Fact]
    public async Task ListObject_OnlyStrings_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed");
        using var context = _fixture.CreateContext<MixedArrayTestDbContext>();

        var entity = new MixedArrayEntity
        {
            Id = id,
            Name = "StringsOnlyTest",
            MixedValues = ["a", "b", "c"]
        };

        context.MixedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<MixedArrayTestDbContext>();
        var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MixedValues.Should().HaveCount(3);
        retrieved.MixedValues.Should().AllBeOfType<string>();
    }

    [Fact]
    public async Task ListObject_AllSupportedPrimitives_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed");
        using var context = _fixture.CreateContext<MixedArrayTestDbContext>();

        var testString = "test-string";
        var testInt = 42;
        var testLong = 9876543210L;
        var testDouble = 3.14159;
        var testDecimal = 123.45m;
        var testGuid = Guid.NewGuid();
        var testEnum = Priority.High;
        var testBool = true;

        var entity = new MixedArrayEntity
        {
            Id = id,
            Name = "AllPrimitivesTest",
            MixedValues =
            [
                testString,
                testInt,
                testLong,
                testDouble,
                testDecimal,
                testGuid,
                testEnum,
                testBool
            ]
        };

        context.MixedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<MixedArrayTestDbContext>();
        var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MixedValues.Should().HaveCount(8);

        // String
        retrieved.MixedValues[0].Should().Be(testString);

        // Int (Firestore returns as long or double)
        Convert.ToInt32(retrieved.MixedValues[1]).Should().Be(testInt);

        // Long (Firestore returns as long)
        Convert.ToInt64(retrieved.MixedValues[2]).Should().Be(testLong);

        // Double
        Convert.ToDouble(retrieved.MixedValues[3]).Should().BeApproximately(testDouble, 0.00001);

        // Decimal (Firestore stores as double)
        Convert.ToDecimal(retrieved.MixedValues[4]).Should().Be(testDecimal);

        // Guid (Firestore stores as string)
        retrieved.MixedValues[5].Should().Be(testGuid.ToString());

        // Enum (Firestore stores as string)
        retrieved.MixedValues[6].Should().Be(testEnum.ToString());

        // Bool
        retrieved.MixedValues[7].Should().Be(testBool);
    }

    // ========================================================================
    // LIST<OBJECT> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListObject_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed-ct");
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = new MixedArrayEntity
            {
                Id = id,
                Name = "MixedChangeTrackingTest",
                MixedValues = ["original", 42, true]
            };
            context.MixedArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = await context.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.MixedValues[0] = "modified";
            entity.MixedValues[1] = 999;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.MixedValues.Should().HaveCount(3);
            retrieved.MixedValues[0].Should().Be("modified");
            Convert.ToInt32(retrieved.MixedValues[1]).Should().Be(999);
            retrieved.MixedValues[2].Should().Be(true);
        }
    }

    [Fact]
    public async Task ListObject_AddElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed-ct");
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = new MixedArrayEntity
            {
                Id = id,
                Name = "MixedAddTest",
                MixedValues = ["a", "b"]
            };
            context.MixedArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Add new elements
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = await context.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.MixedValues.Add(123);
            entity.MixedValues.Add(true);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.MixedValues.Should().HaveCount(4);
            retrieved.MixedValues[0].Should().Be("a");
            retrieved.MixedValues[1].Should().Be("b");
            Convert.ToInt32(retrieved.MixedValues[2]).Should().Be(123);
            retrieved.MixedValues[3].Should().Be(true);
        }
    }

    [Fact]
    public async Task ListObject_RemoveElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed-ct");
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = new MixedArrayEntity
            {
                Id = id,
                Name = "MixedRemoveTest",
                MixedValues = ["first", "second", 42, true]
            };
            context.MixedArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Remove elements
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = await context.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.MixedValues.Remove("second");
            entity.MixedValues.RemoveAt(entity.MixedValues.Count - 1);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.MixedValues.Should().HaveCount(2);
            retrieved.MixedValues[0].Should().Be("first");
            Convert.ToInt32(retrieved.MixedValues[1]).Should().Be(42);
        }
    }

    [Fact]
    public async Task ListObject_ClearAndReplace_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed-ct");
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = new MixedArrayEntity
            {
                Id = id,
                Name = "MixedClearTest",
                MixedValues = ["old1", "old2", 100]
            };
            context.MixedArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Clear and add new elements
        using (var context = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var entity = await context.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.MixedValues.Clear();
            entity.MixedValues.Add("new1");
            entity.MixedValues.Add(999);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<MixedArrayTestDbContext>())
        {
            var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.MixedValues.Should().HaveCount(2);
            retrieved.MixedValues[0].Should().Be("new1");
            Convert.ToInt32(retrieved.MixedValues[1]).Should().Be(999);
        }
    }
}
