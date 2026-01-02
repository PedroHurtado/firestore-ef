namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineResultTests
{
    #region Base Class Tests

    [Fact]
    public void PipelineResult_Is_Abstract_Record()
    {
        typeof(PipelineResult).IsAbstract.Should().BeTrue();
        typeof(PipelineResult).GetMethod("<Clone>$").Should().NotBeNull();
    }

    [Fact]
    public void PipelineResult_Has_Context_Property()
    {
        var property = typeof(PipelineResult).GetProperty("Context");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(PipelineContext));
    }

    #endregion

    #region Streaming Variant Tests

    [Fact]
    public void PipelineResult_Streaming_Exists_As_Nested_Type()
    {
        var streamingType = typeof(PipelineResult).GetNestedType("Streaming");

        streamingType.Should().NotBeNull();
        streamingType!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PipelineResult_Streaming_Inherits_From_PipelineResult()
    {
        var streamingType = typeof(PipelineResult).GetNestedType("Streaming");

        streamingType.Should().NotBeNull();
        streamingType!.BaseType.Should().Be(typeof(PipelineResult));
    }

    [Fact]
    public void PipelineResult_Streaming_Has_Items_Property()
    {
        var streamingType = typeof(PipelineResult).GetNestedType("Streaming");
        var property = streamingType?.GetProperty("Items");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(IAsyncEnumerable<object>));
    }

    [Fact]
    public void PipelineResult_Streaming_Is_Sealed()
    {
        var streamingType = typeof(PipelineResult).GetNestedType("Streaming");

        streamingType!.IsSealed.Should().BeTrue();
    }

    #endregion

    #region Materialized Variant Tests

    [Fact]
    public void PipelineResult_Materialized_Exists_As_Nested_Type()
    {
        var materializedType = typeof(PipelineResult).GetNestedType("Materialized");

        materializedType.Should().NotBeNull();
        materializedType!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PipelineResult_Materialized_Inherits_From_PipelineResult()
    {
        var materializedType = typeof(PipelineResult).GetNestedType("Materialized");

        materializedType.Should().NotBeNull();
        materializedType!.BaseType.Should().Be(typeof(PipelineResult));
    }

    [Fact]
    public void PipelineResult_Materialized_Has_Items_Property()
    {
        var materializedType = typeof(PipelineResult).GetNestedType("Materialized");
        var property = materializedType?.GetProperty("Items");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(IReadOnlyList<object>));
    }

    [Fact]
    public void PipelineResult_Materialized_Is_Sealed()
    {
        var materializedType = typeof(PipelineResult).GetNestedType("Materialized");

        materializedType!.IsSealed.Should().BeTrue();
    }

    #endregion

    #region Scalar Variant Tests

    [Fact]
    public void PipelineResult_Scalar_Exists_As_Nested_Type()
    {
        var scalarType = typeof(PipelineResult).GetNestedType("Scalar");

        scalarType.Should().NotBeNull();
        scalarType!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PipelineResult_Scalar_Inherits_From_PipelineResult()
    {
        var scalarType = typeof(PipelineResult).GetNestedType("Scalar");

        scalarType.Should().NotBeNull();
        scalarType!.BaseType.Should().Be(typeof(PipelineResult));
    }

    [Fact]
    public void PipelineResult_Scalar_Has_Value_Property()
    {
        var scalarType = typeof(PipelineResult).GetNestedType("Scalar");
        var property = scalarType?.GetProperty("Value");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(object));
    }

    [Fact]
    public void PipelineResult_Scalar_Is_Sealed()
    {
        var scalarType = typeof(PipelineResult).GetNestedType("Scalar");

        scalarType!.IsSealed.Should().BeTrue();
    }

    #endregion

    #region Empty Variant Tests

    [Fact]
    public void PipelineResult_Empty_Exists_As_Nested_Type()
    {
        var emptyType = typeof(PipelineResult).GetNestedType("Empty");

        emptyType.Should().NotBeNull();
        emptyType!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PipelineResult_Empty_Inherits_From_PipelineResult()
    {
        var emptyType = typeof(PipelineResult).GetNestedType("Empty");

        emptyType.Should().NotBeNull();
        emptyType!.BaseType.Should().Be(typeof(PipelineResult));
    }

    [Fact]
    public void PipelineResult_Empty_Is_Sealed()
    {
        var emptyType = typeof(PipelineResult).GetNestedType("Empty");

        emptyType!.IsSealed.Should().BeTrue();
    }

    #endregion
}
