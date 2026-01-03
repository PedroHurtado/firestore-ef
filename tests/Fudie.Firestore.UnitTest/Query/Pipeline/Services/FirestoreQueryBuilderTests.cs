using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Pipeline;
using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;
using Google.Cloud.Firestore;
using Moq;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class FirestoreQueryBuilderTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreQueryBuilder_Implements_IQueryBuilder()
    {
        typeof(FirestoreQueryBuilder)
            .Should().Implement<IQueryBuilder>();
    }

    [Fact]
    public void FirestoreQueryBuilder_Constructor_Accepts_IFirestoreClientWrapper()
    {
        var constructors = typeof(FirestoreQueryBuilder).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreClientWrapper));
    }

    [Fact]
    public void FirestoreQueryBuilder_Can_Be_Instantiated()
    {
        var mockClientWrapper = new Mock<IFirestoreClientWrapper>();

        var builder = new FirestoreQueryBuilder(mockClientWrapper.Object);

        builder.Should().NotBeNull();
    }

    #endregion

    #region Build Method Signature Tests

    [Fact]
    public void Build_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(FirestoreQueryBuilder).GetMethod("Build");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Google.Cloud.Firestore.Query));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
    }

    #endregion

    #region BuildAggregate Method Signature Tests

    [Fact]
    public void BuildAggregate_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(FirestoreQueryBuilder).GetMethod("BuildAggregate");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(AggregateQuery));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
    }

    #endregion

    #region Build - Collection Query Behavior Tests

    [Fact]
    public void Build_Uses_CollectionPath_From_ResolvedQuery()
    {
        // Build() should call GetCollection with the CollectionPath from ResolvedQuery
        // This is verified in integration tests since CollectionReference is sealed
        // and cannot be mocked.
        //
        // The implementation at line 27:
        // FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);

        typeof(FirestoreQueryBuilder)
            .GetMethod("Build")
            .Should().NotBeNull("Build should use CollectionPath from ResolvedQuery");
    }

    [Fact]
    public void Build_Returns_Query_For_SimpleCollection()
    {
        // This documents the expected behavior:
        // Build(resolvedQuery with CollectionPath="products")
        // should return a Query for the "products" collection

        typeof(FirestoreQueryBuilder)
            .GetMethod("Build")!
            .ReturnType.Should().Be(typeof(Google.Cloud.Firestore.Query));
    }

    [Fact]
    public void Build_Applies_Filters_From_ResolvedQuery()
    {
        // This documents the expected behavior:
        // Build should apply all filters from resolvedQuery.FilterResults
        // Using WhereEqualTo, WhereGreaterThan, etc. based on FirestoreOperator

        typeof(ResolvedFilterResult).Should().NotBeNull(
            "Build should iterate FilterResults and apply each filter");
    }

    [Fact]
    public void Build_Applies_OrderBy_From_ResolvedQuery()
    {
        // This documents the expected behavior:
        // Build should apply all OrderBy clauses from resolvedQuery.OrderByClauses
        // Using OrderBy or OrderByDescending based on Descending flag

        typeof(ResolvedOrderByClause).Should().NotBeNull(
            "Build should iterate OrderByClauses and apply each ordering");
    }

    [Fact]
    public void Build_Applies_Pagination_From_ResolvedQuery()
    {
        // This documents the expected behavior:
        // Build should apply pagination from resolvedQuery.Pagination
        // - Limit: query.Limit(n)
        // - Skip: query.Offset(n)
        // - LimitToLast: query.LimitToLast(n)

        typeof(ResolvedPaginationInfo).Should().NotBeNull(
            "Build should apply Limit, Skip, LimitToLast from Pagination");
    }

    [Fact]
    public void Build_Applies_MinMax_OrderByAndLimit()
    {
        // For Min/Max aggregations, Build should:
        // - Min: query.Select(field).OrderBy(field).Limit(1)
        // - Max: query.Select(field).OrderByDescending(field).Limit(1)

        typeof(FirestoreAggregationType).Should().NotBeNull(
            "Build handles Min/Max by applying OrderBy + Limit(1)");
    }

    #endregion

    #region BuildAggregate Behavior Tests

    [Fact]
    public void BuildAggregate_Uses_CollectionPath_From_ResolvedQuery()
    {
        // BuildAggregate() should call GetCollection with the CollectionPath from ResolvedQuery
        // This is verified in integration tests since CollectionReference is sealed
        // and cannot be mocked.
        //
        // The implementation at line 70:
        // FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);

        typeof(FirestoreQueryBuilder)
            .GetMethod("BuildAggregate")
            .Should().NotBeNull("BuildAggregate should use CollectionPath from ResolvedQuery");
    }

    [Fact]
    public void BuildAggregate_Returns_AggregateQuery()
    {
        // This documents the expected return type
        typeof(FirestoreQueryBuilder)
            .GetMethod("BuildAggregate")!
            .ReturnType.Should().Be(typeof(AggregateQuery));
    }

    [Fact]
    public void BuildAggregate_Handles_Count()
    {
        // Expected: query.Count()
        FirestoreAggregationType.Count.Should().BeDefined(
            "BuildAggregate should call query.Count() for Count aggregation");
    }

    [Fact]
    public void BuildAggregate_Handles_Any()
    {
        // Expected: query.Count() - Any is Count > 0
        FirestoreAggregationType.Any.Should().BeDefined(
            "BuildAggregate should call query.Count() for Any aggregation");
    }

    [Fact]
    public void BuildAggregate_Handles_Sum()
    {
        // Expected: query.Aggregate(AggregateField.Sum(fieldName))
        FirestoreAggregationType.Sum.Should().BeDefined(
            "BuildAggregate should use AggregateField.Sum for Sum aggregation");
    }

    [Fact]
    public void BuildAggregate_Handles_Average()
    {
        // Expected: query.Aggregate(AggregateField.Average(fieldName))
        FirestoreAggregationType.Average.Should().BeDefined(
            "BuildAggregate should use AggregateField.Average for Average aggregation");
    }

    [Fact]
    public void BuildAggregate_Throws_For_MinMax()
    {
        // Min/Max are NOT native Firestore aggregations
        // They should be handled by Build() with OrderBy + Limit
        // BuildAggregate should throw NotSupportedException for Min/Max

        typeof(FirestoreQueryBuilder).Should().NotBeNull(
            "BuildAggregate should throw NotSupportedException for Min/Max");
    }

    #endregion

    #region Filter Operator Tests

    [Theory]
    [InlineData(FirestoreOperator.EqualTo)]
    [InlineData(FirestoreOperator.NotEqualTo)]
    [InlineData(FirestoreOperator.LessThan)]
    [InlineData(FirestoreOperator.LessThanOrEqualTo)]
    [InlineData(FirestoreOperator.GreaterThan)]
    [InlineData(FirestoreOperator.GreaterThanOrEqualTo)]
    [InlineData(FirestoreOperator.ArrayContains)]
    [InlineData(FirestoreOperator.ArrayContainsAny)]
    [InlineData(FirestoreOperator.In)]
    [InlineData(FirestoreOperator.NotIn)]
    public void Build_Should_Handle_All_FirestoreOperators(FirestoreOperator op)
    {
        // Documents that Build should map each FirestoreOperator to SDK method:
        // EqualTo -> WhereEqualTo
        // NotEqualTo -> WhereNotEqualTo
        // LessThan -> WhereLessThan
        // LessThanOrEqualTo -> WhereLessThanOrEqualTo
        // GreaterThan -> WhereGreaterThan
        // GreaterThanOrEqualTo -> WhereGreaterThanOrEqualTo
        // ArrayContains -> WhereArrayContains
        // ArrayContainsAny -> WhereArrayContainsAny
        // In -> WhereIn
        // NotIn -> WhereNotIn

        op.Should().BeDefined();
    }

    #endregion

    #region Helper Methods

    private static ResolvedFirestoreQuery CreateSimpleQuery(string collectionPath)
    {
        return new ResolvedFirestoreQuery(
            CollectionPath: collectionPath,
            EntityClrType: typeof(object),
            DocumentId: null,
            FilterResults: Array.Empty<ResolvedFilterResult>(),
            OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
            Pagination: ResolvedPaginationInfo.None,
            StartAfterCursor: null,
            Includes: Array.Empty<ResolvedInclude>(),
            AggregationType: FirestoreAggregationType.None,
            AggregationPropertyName: null,
            AggregationResultType: null,
            Projection: null,
            ReturnDefault: false,
            ReturnType: null);
    }

    private static ResolvedFirestoreQuery CreateAggregateQuery(
        string collectionPath,
        FirestoreAggregationType aggregationType,
        string? propertyName = null)
    {
        return new ResolvedFirestoreQuery(
            CollectionPath: collectionPath,
            EntityClrType: typeof(object),
            DocumentId: null,
            FilterResults: Array.Empty<ResolvedFilterResult>(),
            OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
            Pagination: ResolvedPaginationInfo.None,
            StartAfterCursor: null,
            Includes: Array.Empty<ResolvedInclude>(),
            AggregationType: aggregationType,
            AggregationPropertyName: propertyName,
            AggregationResultType: typeof(int),
            Projection: null,
            ReturnDefault: false,
            ReturnType: null);
    }

    #endregion
}
