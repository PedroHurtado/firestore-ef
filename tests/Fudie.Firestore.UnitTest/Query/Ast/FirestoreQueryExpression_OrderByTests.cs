using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_OrderBy partial class.
/// Tests the static TranslateOrderBy method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_OrderByTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_OrderByTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    private ShapedQueryExpression CreateShapedQuery()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var shaperExpression = new StructuralTypeShaperExpression(
            _entityTypeMock.Object,
            new ProjectionBindingExpression(queryExpression, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);
        return new ShapedQueryExpression(queryExpression, shaperExpression);
    }

    #region TranslateOrderBy - IsFirst = true (OrderBy)

    [Fact]
    public void TranslateOrderBy_WithIsFirstTrue_SetsOrderBy()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.OrderByClauses.Should().HaveCount(1);
        ast.OrderByClauses[0].PropertyName.Should().Be("Name");
        ast.OrderByClauses[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void TranslateOrderBy_WithIsFirstTrue_ClearsExistingOrderBys()
    {
        var source = CreateShapedQuery();
        var ast = (FirestoreQueryExpression)source.QueryExpression;
        ast.AddOrderBy(new FirestoreOrderByClause("OldProperty"));

        Expression<Func<TestEntity, decimal>> keySelector = e => e.Price;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: false, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().NotBeNull();
        var updatedAst = (FirestoreQueryExpression)result!.QueryExpression;
        updatedAst.OrderByClauses.Should().HaveCount(1);
        updatedAst.OrderByClauses[0].PropertyName.Should().Be("Price");
    }

    #endregion

    #region TranslateOrderBy - IsFirst = false (ThenBy)

    [Fact]
    public void TranslateOrderBy_WithIsFirstFalse_AddsOrderBy()
    {
        var source = CreateShapedQuery();
        var ast = (FirestoreQueryExpression)source.QueryExpression;
        ast.SetOrderBy(new FirestoreOrderByClause("Name"));

        Expression<Func<TestEntity, decimal>> keySelector = e => e.Price;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: false, IsFirst: false);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().NotBeNull();
        var updatedAst = (FirestoreQueryExpression)result!.QueryExpression;
        updatedAst.OrderByClauses.Should().HaveCount(2);
        updatedAst.OrderByClauses[0].PropertyName.Should().Be("Name");
        updatedAst.OrderByClauses[1].PropertyName.Should().Be("Price");
        updatedAst.OrderByClauses[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void TranslateOrderBy_WithIsFirstFalse_PreservesExistingOrderBys()
    {
        var source = CreateShapedQuery();
        var ast = (FirestoreQueryExpression)source.QueryExpression;
        ast.AddOrderBy(new FirestoreOrderByClause("First"));
        ast.AddOrderBy(new FirestoreOrderByClause("Second"));

        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: false);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        var updatedAst = (FirestoreQueryExpression)result!.QueryExpression;
        updatedAst.OrderByClauses.Should().HaveCount(3);
    }

    #endregion

    #region TranslateOrderBy - Ascending/Descending

    [Fact]
    public void TranslateOrderBy_WithAscendingTrue_CreatesAscendingClause()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.OrderByClauses[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void TranslateOrderBy_WithAscendingFalse_CreatesDescendingClause()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: false, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.OrderByClauses[0].Descending.Should().BeTrue();
    }

    #endregion

    #region TranslateOrderBy - Invalid Cases

    [Fact]
    public void TranslateOrderBy_WithInvalidKeySelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        // Method call expression - not supported
        Expression<Func<TestEntity, string>> keySelector = e => e.Name.ToUpper();
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().BeNull();
    }

    [Fact]
    public void TranslateOrderBy_WithConstantKeySelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var constant = Expression.Constant("constant");
        var lambda = Expression.Lambda<Func<TestEntity, string>>(constant, parameter);
        var request = new TranslateOrderByRequest(source, lambda, Ascending: true, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().BeNull();
    }

    #endregion

    #region TranslateOrderBy - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateOrderBy_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: true);

        var result = FirestoreQueryExpression.TranslateOrderBy(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
    }

    #endregion

    #region TranslateOrderByRequest Record

    [Fact]
    public void TranslateOrderByRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;

        var request = new TranslateOrderByRequest(source, keySelector, Ascending: true, IsFirst: false);

        request.Source.Should().Be(source);
        request.KeySelector.Should().Be(keySelector);
        request.Ascending.Should().BeTrue();
        request.IsFirst.Should().BeFalse();
    }

    [Fact]
    public void TranslateOrderByRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;
        var request = new TranslateOrderByRequest(source, keySelector, true, false);

        var (src, selector, ascending, isFirst) = request;

        src.Should().Be(source);
        selector.Should().Be(keySelector);
        ascending.Should().BeTrue();
        isFirst.Should().BeFalse();
    }

    #endregion
}
