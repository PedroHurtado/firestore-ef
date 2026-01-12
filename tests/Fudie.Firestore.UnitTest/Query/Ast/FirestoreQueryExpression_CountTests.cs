using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Count partial class.
/// Tests the static TranslateCount method.
/// Count returns the number of elements matching the predicate.
/// </summary>
public class FirestoreQueryExpression_CountTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_CountTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private ShapedQueryExpression CreateShapedQuery(FirestoreQueryExpression? queryExpression = null)
    {
        queryExpression ??= new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var shaperExpression = new StructuralTypeShaperExpression(
            _entityTypeMock.Object,
            new ProjectionBindingExpression(queryExpression, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);
        return new ShapedQueryExpression(queryExpression, shaperExpression);
    }

    private static LambdaExpression CreateNamePredicate(string nameValue)
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Name == nameValue;
        return expr;
    }

    #region TranslateCountRequest Record

    [Fact]
    public void TranslateCountRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateCountRequest(source, predicate);

        var (src, pred) = request;

        src.Should().Be(source);
        pred.Should().Be(predicate);
    }

    [Fact]
    public void TranslateCountRequest_WithNullPredicate_HasNullPredicate()
    {
        var source = CreateShapedQuery();
        var request = new TranslateCountRequest(source, null);

        request.Predicate.Should().BeNull();
    }

    #endregion

    #region TranslateCount - No Predicate

    [Fact]
    public void TranslateCount_WithoutPredicate_SetsIsCountQuery()
    {
        var source = CreateShapedQuery();
        var request = new TranslateCountRequest(source, null);

        var result = FirestoreQueryExpression.TranslateCount(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsCountQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateCount_WithoutPredicate_DoesNotAddFilters()
    {
        var source = CreateShapedQuery();
        var request = new TranslateCountRequest(source, null);

        var result = FirestoreQueryExpression.TranslateCount(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().BeEmpty();
    }

    #endregion

    #region TranslateCount - With Predicate

    [Fact]
    public void TranslateCount_WithPredicate_SetsIsCountQuery()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateCountRequest(source, predicate);

        var result = FirestoreQueryExpression.TranslateCount(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsCountQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateCount_WithPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateCountRequest(source, predicate);

        var result = FirestoreQueryExpression.TranslateCount(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
    }

    #endregion

    #region TranslateCount - Preserves Existing State

    [Fact]
    public void TranslateCount_PreservesExistingFilters()
    {
        var filter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter);
        var source = CreateShapedQuery(queryExpr);
        var request = new TranslateCountRequest(source, null);

        var result = FirestoreQueryExpression.TranslateCount(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
        ast.IsCountQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateCount_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var request = new TranslateCountRequest(source, null);

        var result = FirestoreQueryExpression.TranslateCount(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion
}
