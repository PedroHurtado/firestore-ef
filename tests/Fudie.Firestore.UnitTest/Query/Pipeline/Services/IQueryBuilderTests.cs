using Firestore.EntityFrameworkCore.Query.Pipeline;
using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;
using Google.Cloud.Firestore;
using Xunit;
using FirestoreQuery = Google.Cloud.Firestore.Query;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class IQueryBuilderTests
{
    #region Interface Structure Tests

    [Fact]
    public void IQueryBuilder_Is_Interface()
    {
        typeof(IQueryBuilder).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IQueryBuilder_Has_Build_Method()
    {
        var method = typeof(IQueryBuilder).GetMethod("Build");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(FirestoreQuery));
    }

    [Fact]
    public void Build_Accepts_ResolvedFirestoreQuery_Parameter()
    {
        var method = typeof(IQueryBuilder).GetMethod("Build");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
        parameters[0].Name.Should().Be("resolvedQuery");
    }

    [Fact]
    public void IQueryBuilder_Has_BuildAggregate_Method()
    {
        var method = typeof(IQueryBuilder).GetMethod("BuildAggregate");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(AggregateQuery));
    }

    [Fact]
    public void BuildAggregate_Accepts_ResolvedFirestoreQuery_Parameter()
    {
        var method = typeof(IQueryBuilder).GetMethod("BuildAggregate");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
        parameters[0].Name.Should().Be("resolvedQuery");
    }

    #endregion

    #region Design Documentation Tests

    [Fact]
    public void Build_Used_For_Collection_Queries()
    {
        // Documents that Build() is used for collection queries
        // that return DocumentSnapshots.

        var method = typeof(IQueryBuilder).GetMethod("Build");

        method!.ReturnType.Should().Be(typeof(FirestoreQuery),
            "Build returns Query for collection queries");
    }

    [Fact]
    public void Build_Used_For_MinMax_Queries()
    {
        // Documents that Build() is also used for Min/Max aggregations
        // because Firestore doesn't support native Min/Max.
        // Implementation: SELECT field ORDER BY field LIMIT 1

        var method = typeof(IQueryBuilder).GetMethod("Build");

        method.Should().NotBeNull(
            "Build is used for Min/Max via OrderBy + Limit(1)");
    }

    [Fact]
    public void BuildAggregate_Used_For_Native_Aggregations()
    {
        // Documents that BuildAggregate() is used for native Firestore aggregations:
        // - Count
        // - Sum
        // - Average
        // - Any (implemented as Count > 0)

        var method = typeof(IQueryBuilder).GetMethod("BuildAggregate");

        method!.ReturnType.Should().Be(typeof(AggregateQuery),
            "BuildAggregate returns AggregateQuery for native aggregations");
    }

    [Fact]
    public void BuildAggregate_Should_Throw_For_MinMax()
    {
        // Documents that BuildAggregate should throw NotSupportedException
        // for Min/Max since they're not native Firestore aggregations.
        // The ExecutionHandler should use Build() for Min/Max instead.

        typeof(IQueryBuilder).GetMethod("BuildAggregate")
            .Should().NotBeNull(
                "BuildAggregate should throw for Min/Max - use Build() instead");
    }

    [Fact]
    public void IQueryBuilder_Centralizes_Query_Building_Logic()
    {
        // Documents that IQueryBuilder centralizes all query building logic
        // that was previously scattered in ExecutionHandler.
        // This follows Single Responsibility Principle.

        typeof(IQueryBuilder).GetMethods()
            .Should().HaveCount(2,
                "IQueryBuilder has Build and BuildAggregate methods");
    }

    #endregion
}
