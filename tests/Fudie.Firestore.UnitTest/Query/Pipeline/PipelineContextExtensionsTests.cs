using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using FluentAssertions;
using Moq;
using System.Collections.Immutable;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineContextExtensionsTests
{
    #region Class Structure Tests

    [Fact]
    public void PipelineContextExtensions_Is_Static_Class()
    {
        typeof(PipelineContextExtensions).IsAbstract.Should().BeTrue();
        typeof(PipelineContextExtensions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void WithMetadata_Method_Exists()
    {
        var method = typeof(PipelineContextExtensions).GetMethod("WithMetadata");

        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
    }

    [Fact]
    public void GetMetadata_Method_Exists()
    {
        var method = typeof(PipelineContextExtensions).GetMethod("GetMetadata");

        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
    }

    #endregion

    #region WithMetadata Tests

    [Fact]
    public void WithMetadata_Adds_Value_To_Context()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("TestKey");

        var result = context.WithMetadata(key, "TestValue");

        result.Metadata.Should().ContainKey("TestKey");
        result.Metadata["TestKey"].Should().Be("TestValue");
    }

    [Fact]
    public void WithMetadata_Returns_New_Context()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("TestKey");

        var result = context.WithMetadata(key, "TestValue");

        result.Should().NotBeSameAs(context);
    }

    [Fact]
    public void WithMetadata_Preserves_Original_Context()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("TestKey");

        context.WithMetadata(key, "TestValue");

        context.Metadata.Should().NotContainKey("TestKey");
    }

    [Fact]
    public void WithMetadata_Can_Add_Multiple_Values()
    {
        var context = CreateEmptyContext();
        var key1 = new MetadataKey<string>("Key1");
        var key2 = new MetadataKey<int>("Key2");

        var result = context
            .WithMetadata(key1, "Value1")
            .WithMetadata(key2, 42);

        result.Metadata.Should().HaveCount(2);
        result.Metadata["Key1"].Should().Be("Value1");
        result.Metadata["Key2"].Should().Be(42);
    }

    [Fact]
    public void WithMetadata_Overwrites_Existing_Value()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("TestKey");

        var result = context
            .WithMetadata(key, "FirstValue")
            .WithMetadata(key, "SecondValue");

        result.Metadata["TestKey"].Should().Be("SecondValue");
    }

    [Fact]
    public void WithMetadata_Works_With_Int_Values()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<int>("Counter");

        var result = context.WithMetadata(key, 123);

        result.Metadata["Counter"].Should().Be(123);
    }

    [Fact]
    public void WithMetadata_Works_With_Bool_Values()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<bool>("IsEnabled");

        var result = context.WithMetadata(key, true);

        result.Metadata["IsEnabled"].Should().Be(true);
    }

    #endregion

    #region GetMetadata Tests

    [Fact]
    public void GetMetadata_Returns_Value_When_Key_Exists()
    {
        var key = new MetadataKey<string>("TestKey");
        var context = CreateEmptyContext().WithMetadata(key, "TestValue");

        var result = context.GetMetadata(key);

        result.Should().Be("TestValue");
    }

    [Fact]
    public void GetMetadata_Returns_Default_When_Key_Not_Found()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("NonExistent");

        var result = context.GetMetadata(key);

        result.Should().BeNull();
    }

    [Fact]
    public void GetMetadata_Returns_Default_Int_When_Key_Not_Found()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<int>("NonExistent");

        var result = context.GetMetadata(key);

        result.Should().Be(0);
    }

    [Fact]
    public void GetMetadata_Returns_Default_Bool_When_Key_Not_Found()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<bool>("NonExistent");

        var result = context.GetMetadata(key);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetMetadata_Works_With_Nullable_Types()
    {
        var key = new MetadataKey<int?>("NullableInt");
        var context = CreateEmptyContext().WithMetadata(key, 42);

        var result = context.GetMetadata(key);

        result.Should().Be(42);
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void WithMetadata_And_GetMetadata_Roundtrip_Works()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<string>("RoundtripKey");

        var newContext = context.WithMetadata(key, "RoundtripValue");
        var result = newContext.GetMetadata(key);

        result.Should().Be("RoundtripValue");
    }

    [Fact]
    public void WithMetadata_And_GetMetadata_Roundtrip_Works_For_Complex_Types()
    {
        var context = CreateEmptyContext();
        var key = new MetadataKey<int[]>("ArrayKey");
        var array = new[] { 1, 2, 3 };

        var newContext = context.WithMetadata(key, array);
        var result = newContext.GetMetadata(key);

        result.Should().BeEquivalentTo(array);
    }

    #endregion

    #region Helper Methods

    private static PipelineContext CreateEmptyContext()
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        return new PipelineContext
        {
            Ast = null!,
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = QueryKind.Entity,
            Metadata = ImmutableDictionary<string, object>.Empty
        };
    }

    #endregion
}
