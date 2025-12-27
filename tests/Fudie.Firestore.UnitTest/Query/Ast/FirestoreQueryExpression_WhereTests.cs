using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Where partial class.
/// Tests the static TranslateWhere method.
/// Where applies filter clauses to the query.
/// </summary>
public class FirestoreQueryExpression_WhereTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_WhereTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Quantity { get; set; }
        public string Status { get; set; } = default!;
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

    private static LambdaExpression CreateQuantityPredicate(int quantity)
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Quantity > quantity;
        return expr;
    }

    private static LambdaExpression CreateIdPredicate(string idValue)
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Id == idValue;
        return expr;
    }

    private static LambdaExpression CreateOrPredicate()
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Name == "A" || e.Name == "B";
        return expr;
    }

    private static LambdaExpression CreateAndPredicate()
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Name == "test" && e.Quantity > 10;
        return expr;
    }

    #region TranslateWhereRequest Record

    [Fact]
    public void TranslateWhereRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var (src, body) = request;

        src.Should().Be(source);
        body.Should().Be(predicate.Body);
    }

    #endregion

    #region TranslateWhere - Simple Filters

    [Fact]
    public void TranslateWhere_WithEqualityPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
        ast.Filters[0].PropertyName.Should().Be("Name");
        ast.Filters[0].Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    [Fact]
    public void TranslateWhere_WithComparisonPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateQuantityPredicate(10);
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
        ast.Filters[0].PropertyName.Should().Be("Quantity");
        ast.Filters[0].Operator.Should().Be(FirestoreOperator.GreaterThan);
    }

    #endregion

    #region TranslateWhere - Id Optimization

    [Fact]
    public void TranslateWhere_WithIdEquality_SetsIdValueExpression()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc123");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeTrue();
        ast.Filters.Should().BeEmpty();
    }

    [Fact]
    public void TranslateWhere_WithIdAfterExistingFilters_AddsAsNormalFilter()
    {
        var existingFilter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(existingFilter);
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateIdPredicate("doc123");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Filters.Should().HaveCount(2);
    }

    #endregion

    #region TranslateWhere - AND Expressions

    [Fact]
    public void TranslateWhere_WithAndExpression_AddsMultipleFilters()
    {
        var source = CreateShapedQuery();
        var predicate = CreateAndPredicate();
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(2);
    }

    #endregion

    #region TranslateWhere - OR Expressions

    [Fact]
    public void TranslateWhere_WithOrExpression_AddsOrFilterGroup()
    {
        var source = CreateShapedQuery();
        var predicate = CreateOrPredicate();
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.OrFilterGroups.Should().HaveCount(1);
        ast.OrFilterGroups[0].Clauses.Should().HaveCount(2);
    }

    [Fact]
    public void TranslateWhere_WithOrOnIdOnlyQuery_ThrowsInvalidOperationException()
    {
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithIdValueExpression(Expression.Constant("doc123"));
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateOrPredicate();
        var request = new TranslateWhereRequest(source, predicate.Body);

        var act = () => FirestoreQueryExpression.TranslateWhere(request);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OR*ID*");
    }

    #endregion

    #region TranslateWhere - Preserves Existing State

    [Fact]
    public void TranslateWhere_PreservesExistingFilters()
    {
        var existingFilter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(existingFilter);
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateNamePredicate("test");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void TranslateWhere_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion

    #region TranslateWhere - IdOnlyQuery Conversion

    [Fact]
    public void TranslateWhere_WhenIdOnlyQueryAndNewFilters_ConvertsToNormalQuery()
    {
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithIdValueExpression(Expression.Constant("doc123"));
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateNamePredicate("test");
        var request = new TranslateWhereRequest(source, predicate.Body);

        var result = FirestoreQueryExpression.TranslateWhere(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Filters.Should().HaveCount(2); // Id + Name
    }

    #endregion
}
