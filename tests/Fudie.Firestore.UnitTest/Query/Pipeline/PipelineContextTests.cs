using System.Collections.Immutable;

namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineContextTests
{
    #region Record Definition Tests

    [Fact]
    public void PipelineContext_Is_Record()
    {
        typeof(PipelineContext).IsClass.Should().BeTrue();
        typeof(PipelineContext).GetMethod("<Clone>$").Should().NotBeNull();
    }

    [Fact]
    public void PipelineContext_Has_Required_Ast_Property()
    {
        var property = typeof(PipelineContext).GetProperty("Ast");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(FirestoreQueryExpression));
    }

    [Fact]
    public void PipelineContext_Has_Required_QueryContext_Property()
    {
        var property = typeof(PipelineContext).GetProperty("QueryContext");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(IFirestoreQueryContext));
    }

    [Fact]
    public void PipelineContext_Has_Required_IsTracking_Property()
    {
        var property = typeof(PipelineContext).GetProperty("IsTracking");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void PipelineContext_Has_Required_ResultType_Property()
    {
        var property = typeof(PipelineContext).GetProperty("ResultType");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Type));
    }

    [Fact]
    public void PipelineContext_Has_Required_Kind_Property()
    {
        var property = typeof(PipelineContext).GetProperty("Kind");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(QueryKind));
    }

    [Fact]
    public void PipelineContext_Has_Optional_EntityType_Property()
    {
        var property = typeof(PipelineContext).GetProperty("EntityType");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Type));
    }

    [Fact]
    public void PipelineContext_Has_Metadata_Property_With_ImmutableDictionary()
    {
        var property = typeof(PipelineContext).GetProperty("Metadata");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(ImmutableDictionary<string, object>));
    }

    [Fact]
    public void PipelineContext_Has_Optional_ResolvedQuery_Property()
    {
        var property = typeof(PipelineContext).GetProperty("ResolvedQuery");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(ResolvedFirestoreQuery));
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void PipelineContext_Can_Be_Modified_With_With_Expression()
    {
        // This test verifies the record supports 'with' expressions
        var method = typeof(PipelineContext).GetMethod("<Clone>$");
        method.Should().NotBeNull("PipelineContext should be a record with 'with' support");
    }

    #endregion
}
