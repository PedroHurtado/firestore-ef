using System.Collections.Immutable;

namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineMetadataTests
{
    #region MetadataKey Tests

    [Fact]
    public void MetadataKey_Is_Readonly_Record_Struct()
    {
        typeof(MetadataKey<>).IsValueType.Should().BeTrue();
        // Record structs implement IEquatable<T> for value equality
        typeof(MetadataKey<string>).GetInterfaces()
            .Should().Contain(t => t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(IEquatable<>));
    }

    [Fact]
    public void MetadataKey_Has_Name_Property()
    {
        var property = typeof(MetadataKey<string>).GetProperty("Name");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void MetadataKey_Constructor_Sets_Name()
    {
        var key = new MetadataKey<bool>("TestKey");

        key.Name.Should().Be("TestKey");
    }

    #endregion

    #region PipelineMetadataKeys Tests

    [Fact]
    public void PipelineMetadataKeys_Has_RequiresLazyLoader_Key()
    {
        var field = typeof(PipelineMetadataKeys)
            .GetField("RequiresLazyLoader", BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(MetadataKey<bool>));
    }

    [Fact]
    public void PipelineMetadataKeys_Has_CacheKey_Key()
    {
        var field = typeof(PipelineMetadataKeys)
            .GetField("CacheKey", BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(MetadataKey<string>));
    }

    [Fact]
    public void PipelineMetadataKeys_Has_TrackedEntities_Key()
    {
        var field = typeof(PipelineMetadataKeys)
            .GetField("TrackedEntities", BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(MetadataKey<HashSet<object>>));
    }

    [Fact]
    public void PipelineMetadataKeys_Has_ExecutionTime_Key()
    {
        var field = typeof(PipelineMetadataKeys)
            .GetField("ExecutionTime", BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field!.FieldType.Should().Be(typeof(MetadataKey<TimeSpan>));
    }

    #endregion

    #region PipelineContextExtensions Tests

    [Fact]
    public void WithMetadata_Returns_New_Context_With_Metadata()
    {
        // Arrange
        var originalContext = CreateContext();
        var key = new MetadataKey<string>("TestKey");

        // Act
        var newContext = originalContext.WithMetadata(key, "TestValue");

        // Assert
        newContext.Should().NotBeSameAs(originalContext);
        newContext.Metadata.Should().ContainKey("TestKey");
        newContext.Metadata["TestKey"].Should().Be("TestValue");
    }

    [Fact]
    public void WithMetadata_Preserves_Existing_Metadata()
    {
        // Arrange
        var key1 = new MetadataKey<string>("Key1");
        var key2 = new MetadataKey<int>("Key2");
        var context = CreateContext().WithMetadata(key1, "Value1");

        // Act
        var newContext = context.WithMetadata(key2, 42);

        // Assert
        newContext.Metadata.Should().HaveCount(2);
        newContext.Metadata["Key1"].Should().Be("Value1");
        newContext.Metadata["Key2"].Should().Be(42);
    }

    [Fact]
    public void GetMetadata_Returns_Value_When_Key_Exists()
    {
        // Arrange
        var key = new MetadataKey<string>("TestKey");
        var context = CreateContext().WithMetadata(key, "TestValue");

        // Act
        var value = context.GetMetadata(key);

        // Assert
        value.Should().Be("TestValue");
    }

    [Fact]
    public void GetMetadata_Returns_Default_When_Key_Does_Not_Exist()
    {
        // Arrange
        var key = new MetadataKey<string>("NonExistentKey");
        var context = CreateContext();

        // Act
        var value = context.GetMetadata(key);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetMetadata_Returns_Default_For_Value_Types_When_Key_Does_Not_Exist()
    {
        // Arrange
        var key = new MetadataKey<int>("NonExistentKey");
        var context = CreateContext();

        // Act
        var value = context.GetMetadata(key);

        // Assert
        value.Should().Be(0);
    }

    #endregion

    private static PipelineContext CreateContext()
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        return new PipelineContext
        {
            Ast = null!,
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = QueryKind.Entity
        };
    }
}
