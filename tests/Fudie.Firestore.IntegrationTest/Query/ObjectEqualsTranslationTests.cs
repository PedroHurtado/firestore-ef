using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ObjectEquals;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for object.Equals() translation in LINQ queries.
/// These tests verify that the provider can translate .Equals() calls
/// when used with generic type parameters (which resolve to object.Equals(object)).
///
/// Bug: When using generic methods like GetRequiredAsync{T, TId}(), the compiler
/// resolves .Equals(id) to object.Equals(object) instead of TId.Equals(TId),
/// causing translation to fail.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ObjectEqualsTranslationTests
{
    private readonly FirestoreTestFixture _fixture;

    public ObjectEqualsTranslationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ObjectEquals_WithStringId_TranslatesCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<ObjectEqualsDbContext>();
        var entityId = FirestoreTestFixture.GenerateId("str");
        var entity = new EntityWithStringId
        {
            Id = entityId,
            Name = "Test Entity String",
            Description = "For object.Equals test"
        };

        context.EntitiesWithStringId.Add(entity);
        await context.SaveChangesAsync();

        // Act - Use the generic helper that causes the problem
        using var readContext = _fixture.CreateContext<ObjectEqualsDbContext>();
        var lookup = new EntityLookupHelper(readContext);

        // This should NOT throw - the provider should translate object.Equals()
        var result = await lookup.GetRequiredAsync<EntityWithStringId, string>(entityId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(entityId);
        result.Name.Should().Be("Test Entity String");
    }

    [Fact]
    public async Task ObjectEquals_WithGuidId_TranslatesCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<ObjectEqualsDbContext>();
        var entityId = Guid.NewGuid();
        var entity = new EntityWithGuidId
        {
            Id = entityId,
            Name = "Test Entity Guid",
            Price = 99.99m
        };

        context.EntitiesWithGuidId.Add(entity);
        await context.SaveChangesAsync();

        // Act - Use the generic helper that causes the problem
        using var readContext = _fixture.CreateContext<ObjectEqualsDbContext>();
        var lookup = new EntityLookupHelper(readContext);

        // This should NOT throw - the provider should translate object.Equals()
        var result = await lookup.GetRequiredAsync<EntityWithGuidId, Guid>(entityId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(entityId);
        result.Name.Should().Be("Test Entity Guid");
    }
}
