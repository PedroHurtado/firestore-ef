using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.IntegrationTest.Helpers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Update;

/// <summary>
/// Integration tests for partial update functionality.
/// Verifies that only modified fields are sent to Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class PartialUpdateTests
{
    private readonly FirestoreTestFixture _fixture;

    public PartialUpdateTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Simple Property Tests

    [Fact]
    public async Task Update_SimpleProperty_ShouldPersistOnlyModifiedField()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateTestDbContext>();

        var entity = new PartialUpdateEntity
        {
            Id = entityId,
            Name = "Original Name",
            Description = "Original Description",
            Price = 100.00m,
            IsActive = true
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Load and modify only Name
        using var updateContext = _fixture.CreateContext<PartialUpdateTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Name = "Updated Name";
        await updateContext.SaveChangesAsync();

        // Assert - Verify via direct Firestore read
        var rawData = await GetDocumentRawData(entityId);

        rawData["Name"].Should().Be("Updated Name");
        rawData["Description"].Should().Be("Original Description");
        rawData["IsActive"].Should().Be(true);
    }

    [Fact]
    public async Task Update_MultipleSimpleProperties_ShouldPersistAll()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateTestDbContext>();

        var entity = new PartialUpdateEntity
        {
            Id = entityId,
            Name = "Original Name",
            Description = "Original Description",
            Price = 100.00m,
            IsActive = true
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Load and modify multiple properties
        using var updateContext = _fixture.CreateContext<PartialUpdateTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Name = "Updated Name";
        loaded.Price = 200.00m;
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData(entityId);

        rawData["Name"].Should().Be("Updated Name");
        rawData["Price"].Should().Be(200.0); // decimal → double
        rawData["Description"].Should().Be("Original Description");
    }

    [Fact]
    public async Task Update_PropertyToNull_ShouldDeleteField()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateTestDbContext>();

        var entity = new PartialUpdateEntity
        {
            Id = entityId,
            Name = "Test Name",
            Description = "To be deleted",
            Price = 100.00m
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Set Description to null
        using var updateContext = _fixture.CreateContext<PartialUpdateTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Description = null;
        await updateContext.SaveChangesAsync();

        // Assert - Description field should be deleted (not present in document)
        var rawData = await GetDocumentRawData(entityId);

        rawData["Name"].Should().Be("Test Name");
        rawData.Should().NotContainKey("Description");
    }

    [Fact]
    public async Task Update_NoChanges_ShouldNotUpdateDocument()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateTestDbContext>();

        var entity = new PartialUpdateEntity
        {
            Id = entityId,
            Name = "No Change Name",
            Description = "No Change Description",
            Price = 100.00m
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Load without modifications and save
        using var updateContext = _fixture.CreateContext<PartialUpdateTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        var stateBefore = updateContext.Entry(loaded!).State;
        var changesCount = await updateContext.SaveChangesAsync();

        // Assert - No changes should be detected
        stateBefore.Should().Be(EntityState.Unchanged);
        changesCount.Should().Be(0);
    }

    #endregion

    #region ComplexType Tests

    [Fact]
    public async Task Update_ComplexTypeField_ShouldUseDotNotation()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();

        var entity = new EntityWithComplexType
        {
            Id = entityId,
            Name = "Complex Entity",
            Address = new AddressInfo
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Modify only City within Address
        using var updateContext = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Address.City = "Brooklyn";
        await updateContext.SaveChangesAsync();

        // Assert - Verify the Address was updated
        var rawData = await GetDocumentRawData<EntityWithComplexType>(entityId);

        var address = rawData["Address"] as Dictionary<string, object>;
        address.Should().NotBeNull();
        address!["Street"].Should().Be("123 Main St");
        address["City"].Should().Be("Brooklyn");
        address["ZipCode"].Should().Be("10001");
    }

    [Fact]
    public async Task Update_MultipleComplexTypeFields_ShouldUpdateAll()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();

        var entity = new EntityWithComplexType
        {
            Id = entityId,
            Name = "Complex Entity",
            Address = new AddressInfo
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Modify multiple fields within Address
        using var updateContext = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Address.Street = "456 Broadway";
        loaded.Address.City = "Manhattan";
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EntityWithComplexType>(entityId);

        var address = rawData["Address"] as Dictionary<string, object>;
        address!["Street"].Should().Be("456 Broadway");
        address["City"].Should().Be("Manhattan");
        address["ZipCode"].Should().Be("10001");
    }

    [Fact]
    public async Task Update_ComplexTypeFieldToNull_ShouldDeleteField()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();

        var entity = new EntityWithComplexType
        {
            Id = entityId,
            Name = "Complex Entity",
            Address = new AddressInfo
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Set ZipCode to null
        using var updateContext = _fixture.CreateContext<PartialUpdateComplexTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Address.ZipCode = null;
        await updateContext.SaveChangesAsync();

        // Assert - ZipCode should be deleted
        var rawData = await GetDocumentRawData<EntityWithComplexType>(entityId);

        var address = rawData["Address"] as Dictionary<string, object>;
        address!["Street"].Should().Be("123 Main St");
        address["City"].Should().Be("New York");
        address.Should().NotContainKey("ZipCode");
    }

    #endregion

    #region ArrayOf Tests

    [Fact]
    public async Task Update_ArrayOfAddElement_ShouldPersistChange()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Array Entity",
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Add new element
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Tags.Add(new TagItem { Name = "Tag2", Color = "Blue" });
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags = ((IEnumerable<object>)rawData["Tags"]).ToList();

        tags.Should().HaveCount(2);
        var tag2 = tags[1] as Dictionary<string, object>;
        tag2!["Name"].Should().Be("Tag2");
        tag2["Color"].Should().Be("Blue");
    }

    [Fact]
    public async Task Update_ArrayOfRemoveElement_ShouldPersistChange()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Array Entity",
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" },
                new TagItem { Name = "Tag2", Color = "Blue" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Remove element
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Tags.RemoveAt(1);
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags = ((IEnumerable<object>)rawData["Tags"]).ToList();

        tags.Should().HaveCount(1);
        var tag1 = tags[0] as Dictionary<string, object>;
        tag1!["Name"].Should().Be("Tag1");
    }

    [Fact]
    public async Task Update_ArrayOfModifyElement_ShouldPersistChange()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Array Entity",
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Modify element
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Tags[0].Color = "Green";
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags = ((IEnumerable<object>)rawData["Tags"]).ToList();

        tags.Should().HaveCount(1);
        var tag1 = tags[0] as Dictionary<string, object>;
        tag1!["Name"].Should().Be("Tag1");
        tag1["Color"].Should().Be("Green");
    }

    [Fact]
    public async Task Update_ArrayOfNoChanges_ShouldNotTriggerUpdate()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Array Entity",
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Load without modifications
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        var stateBefore = updateContext.Entry(loaded!).State;
        var changesCount = await updateContext.SaveChangesAsync();

        // Assert
        stateBefore.Should().Be(EntityState.Unchanged);
        changesCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_ArrayOfMultipleSaveChanges_ShouldTrackChangesCorrectly()
    {
        // Arrange - Create entity with 3 elements
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Multi-Save Entity",
            Tags =
            [
                new TagItem { Name = "TagA", Color = "Red" },
                new TagItem { Name = "TagB", Color = "Blue" },
                new TagItem { Name = "TagC", Color = "Green" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act 1 - First modification: change TagA color
        using var updateContext1 = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded1 = await updateContext1.Entities.FindAsync(entityId);

        loaded1!.Tags.First(t => t.Name == "TagA").Color = "Yellow";
        await updateContext1.SaveChangesAsync();

        // Verify after first save
        // Note: ArrayUnion adds modified elements to the end, so order may change
        var rawData1 = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags1 = ((IEnumerable<object>)rawData1["Tags"])
            .Cast<Dictionary<string, object>>()
            .ToList();
        tags1.Should().HaveCount(3);
        tags1.Should().Contain(t => (string)t["Name"] == "TagA" && (string)t["Color"] == "Yellow");

        // Act 2 - Second modification: change TagB color
        // This tests that the shadow property was updated after first SaveChanges
        using var updateContext2 = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded2 = await updateContext2.Entities.FindAsync(entityId);

        loaded2!.Tags.First(t => t.Name == "TagB").Color = "Purple";
        await updateContext2.SaveChangesAsync();

        // Verify after second save
        var rawData2 = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags2 = ((IEnumerable<object>)rawData2["Tags"])
            .Cast<Dictionary<string, object>>()
            .ToList();
        tags2.Should().HaveCount(3);
        tags2.Should().Contain(t => (string)t["Name"] == "TagA" && (string)t["Color"] == "Yellow");
        tags2.Should().Contain(t => (string)t["Name"] == "TagB" && (string)t["Color"] == "Purple");
        tags2.Should().Contain(t => (string)t["Name"] == "TagC" && (string)t["Color"] == "Green");
    }

    [Fact]
    public async Task Update_ArrayOfRemoveAllElements_ShouldDeleteField()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Array Entity",
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" },
                new TagItem { Name = "Tag2", Color = "Blue" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Remove all elements
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Tags.Clear();
        await updateContext.SaveChangesAsync();

        // Assert - Tags field should be deleted (not present or empty)
        var rawData = await GetDocumentRawData<EntityWithArrayOf>(entityId);

        // When all elements are removed, FieldValue.Delete is used, so field should not exist
        rawData.Should().NotContainKey("Tags");
    }

    [Fact]
    public async Task Update_ArrayOfConsecutiveModifications_SameContext_ShouldWorkCorrectly()
    {
        // Arrange - Create entity
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();

        var entity = new EntityWithArrayOf
        {
            Id = entityId,
            Name = "Consecutive Entity",
            Tags =
            [
                new TagItem { Name = "TagA", Color = "Red" },
                new TagItem { Name = "TagB", Color = "Blue" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Multiple modifications in same context with multiple SaveChanges
        using var updateContext = _fixture.CreateContext<PartialUpdateArrayTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        // First modification
        loaded!.Tags.First(t => t.Name == "TagA").Color = "Yellow";
        await updateContext.SaveChangesAsync();

        // Second modification on same tracked entity
        loaded.Tags.First(t => t.Name == "TagB").Color = "Purple";
        await updateContext.SaveChangesAsync();

        // Assert - Both changes should be persisted
        // Note: ArrayUnion adds modified elements to the end, so order may change
        var rawData = await GetDocumentRawData<EntityWithArrayOf>(entityId);
        var tags = ((IEnumerable<object>)rawData["Tags"])
            .Cast<Dictionary<string, object>>()
            .ToList();

        tags.Should().HaveCount(2);
        tags.Should().Contain(t => (string)t["Name"] == "TagA" && (string)t["Color"] == "Yellow");
        tags.Should().Contain(t => (string)t["Name"] == "TagB" && (string)t["Color"] == "Purple");
    }

    #endregion

    #region Mixed Changes Tests

    [Fact]
    public async Task Update_MixedChanges_ShouldPersistAll()
    {
        // Arrange
        var entityId = FirestoreTestFixture.GenerateId("partial");
        using var context = _fixture.CreateContext<PartialUpdateMixedTestDbContext>();

        var entity = new EntityWithMixedTypes
        {
            Id = entityId,
            Name = "Mixed Entity",
            Description = "Original Description",
            Address = new AddressInfo
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            },
            Tags =
            [
                new TagItem { Name = "Tag1", Color = "Red" }
            ]
        };

        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        // Act - Modify simple property, complex property field, and array
        using var updateContext = _fixture.CreateContext<PartialUpdateMixedTestDbContext>();
        var loaded = await updateContext.Entities.FindAsync(entityId);

        loaded!.Name = "Updated Name";
        loaded.Description = null;  // Delete field
        loaded.Address.City = "Brooklyn";
        loaded.Tags.Add(new TagItem { Name = "Tag2", Color = "Blue" });
        await updateContext.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EntityWithMixedTypes>(entityId);

        rawData["Name"].Should().Be("Updated Name");
        rawData.Should().NotContainKey("Description");

        var address = rawData["Address"] as Dictionary<string, object>;
        address!["City"].Should().Be("Brooklyn");
        address["Street"].Should().Be("123 Main St");

        var tags = ((IEnumerable<object>)rawData["Tags"]).ToList();
        tags.Should().HaveCount(2);
    }

    #endregion

    #region Helpers

    private async Task<Dictionary<string, object>> GetDocumentRawData(string documentId)
    {
        return await GetDocumentRawData<PartialUpdateEntity>(documentId);
    }

    private async Task<Dictionary<string, object>> GetDocumentRawData<T>(string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var collectionName = GetCollectionName<T>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue($"Document {documentId} should exist");
        return docSnapshot.ToDictionary();
    }

#pragma warning disable EF1001
    private static string GetCollectionName<T>()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001

    #endregion
}

// ============================================================================
// TEST ENTITIES AND DBCONTEXTS
// ============================================================================

#region Test Entities

/// <summary>
/// Entity for simple property partial update tests.
/// </summary>
public class PartialUpdateEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Address ComplexType for partial update tests.
/// </summary>
public class AddressInfo
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
}

/// <summary>
/// Entity with ComplexType for partial update tests.
/// </summary>
public class EntityWithComplexType
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public AddressInfo Address { get; set; } = new();
}

/// <summary>
/// Tag item for ArrayOf tests.
/// </summary>
public class TagItem
{
    public string Name { get; set; } = default!;
    public string Color { get; set; } = default!;
}

/// <summary>
/// Entity with ArrayOf for partial update tests.
/// </summary>
public class EntityWithArrayOf
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public List<TagItem> Tags { get; set; } = [];
}

/// <summary>
/// Entity with all types for mixed partial update tests.
/// </summary>
public class EntityWithMixedTypes
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public AddressInfo Address { get; set; } = new();
    public List<TagItem> Tags { get; set; } = [];
}

#endregion

#region Test DbContexts

/// <summary>
/// DbContext for simple property partial update tests.
/// </summary>
public class PartialUpdateTestDbContext(DbContextOptions<PartialUpdateTestDbContext> options) : DbContext(options)
{
    public DbSet<PartialUpdateEntity> Entities => Set<PartialUpdateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PartialUpdateEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}

/// <summary>
/// DbContext for ComplexType partial update tests.
/// </summary>
public class PartialUpdateComplexTestDbContext(DbContextOptions<PartialUpdateComplexTestDbContext> options) : DbContext(options)
{
    public DbSet<EntityWithComplexType> Entities => Set<EntityWithComplexType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithComplexType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.ComplexProperty(e => e.Address).IsRequired();
        });
    }
}

/// <summary>
/// DbContext for ArrayOf partial update tests.
/// </summary>
public class PartialUpdateArrayTestDbContext(DbContextOptions<PartialUpdateArrayTestDbContext> options) : DbContext(options)
{
    public DbSet<EntityWithArrayOf> Entities => Set<EntityWithArrayOf>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithArrayOf>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.ArrayOf(e => e.Tags);
        });
    }
}

/// <summary>
/// DbContext for mixed partial update tests.
/// </summary>
public class PartialUpdateMixedTestDbContext(DbContextOptions<PartialUpdateMixedTestDbContext> options) : DbContext(options)
{
    public DbSet<EntityWithMixedTypes> Entities => Set<EntityWithMixedTypes>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithMixedTypes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.ComplexProperty(e => e.Address).IsRequired();
            entity.ArrayOf(e => e.Tags);
        });
    }
}

#endregion
