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
    public async Task ListObject_OnlyNumbers_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed");
        using var context = _fixture.CreateContext<MixedArrayTestDbContext>();

        var entity = new MixedArrayEntity
        {
            Id = id,
            Name = "NumbersOnlyTest",
            MixedValues = [1, 2.5, 3, 4.75]
        };

        context.MixedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<MixedArrayTestDbContext>();
        var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MixedValues.Should().HaveCount(4);
    }

    [Fact]
    public async Task ListObject_Empty_ShouldPersistAndRetrieve()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("mixed");
        using var context = _fixture.CreateContext<MixedArrayTestDbContext>();

        var entity = new MixedArrayEntity
        {
            Id = id,
            Name = "EmptyMixedTest",
            MixedValues = []
        };

        context.MixedArrays.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<MixedArrayTestDbContext>();
        var retrieved = await readContext.MixedArrays.FirstOrDefaultAsync(e => e.Id == id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MixedValues.Should().BeEmpty();
    }
}
