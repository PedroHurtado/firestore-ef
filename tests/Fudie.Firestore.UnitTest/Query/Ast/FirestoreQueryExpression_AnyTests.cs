using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Any partial class.
/// Tests the static TranslateAny method.
/// Any checks if there are any elements matching the predicate.
/// </summary>
public class FirestoreQueryExpression_AnyTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_AnyTests()
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

    #region TranslateAnyRequest Record

    [Fact]
    public void TranslateAnyRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateAnyRequest(source, predicate);

        var (src, pred) = request;

        src.Should().Be(source);
        pred.Should().Be(predicate);
    }

    [Fact]
    public void TranslateAnyRequest_WithNullPredicate_HasNullPredicate()
    {
        var source = CreateShapedQuery();
        var request = new TranslateAnyRequest(source, null);

        request.Predicate.Should().BeNull();
    }

    #endregion

    #region TranslateAny - No Predicate

    [Fact]
    public void TranslateAny_WithoutPredicate_SetsIsAnyQuery()
    {
        var source = CreateShapedQuery();
        var request = new TranslateAnyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateAny(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsAnyQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateAny_WithoutPredicate_DoesNotAddFilters()
    {
        var source = CreateShapedQuery();
        var request = new TranslateAnyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateAny(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().BeEmpty();
    }

    #endregion

    #region TranslateAny - With Predicate

    [Fact]
    public void TranslateAny_WithPredicate_SetsIsAnyQuery()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateAnyRequest(source, predicate);

        var result = FirestoreQueryExpression.TranslateAny(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsAnyQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateAny_WithPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateAnyRequest(source, predicate);

        var result = FirestoreQueryExpression.TranslateAny(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
    }

    #endregion

    #region TranslateAny - Preserves Existing State

    [Fact]
    public void TranslateAny_PreservesExistingFilters()
    {
        var filter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter);
        var source = CreateShapedQuery(queryExpr);
        var request = new TranslateAnyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateAny(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
        ast.IsAnyQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateAny_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var request = new TranslateAnyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateAny(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion
}
