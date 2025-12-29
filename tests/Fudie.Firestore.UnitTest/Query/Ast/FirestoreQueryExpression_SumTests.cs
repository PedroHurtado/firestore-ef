using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Sum partial class.
/// Tests the static TranslateSum method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_SumTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_SumTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public double Weight { get; set; }
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

    #region TranslateSum - Valid Selectors

    [Fact]
    public void TranslateSum_WithIntSelector_SetsAggregation()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;
        var request = new TranslateSumRequest(source, selector, typeof(int));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.AggregationType.Should().Be(FirestoreAggregationType.Sum);
        ast.AggregationPropertyName.Should().Be("Quantity");
        ast.AggregationResultType.Should().Be(typeof(int));
    }

    [Fact]
    public void TranslateSum_WithDecimalSelector_SetsAggregation()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;
        var request = new TranslateSumRequest(source, selector, typeof(decimal));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.AggregationType.Should().Be(FirestoreAggregationType.Sum);
        ast.AggregationPropertyName.Should().Be("Price");
        ast.AggregationResultType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void TranslateSum_WithDoubleSelector_SetsAggregation()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, double>> selector = e => e.Weight;
        var request = new TranslateSumRequest(source, selector, typeof(double));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.AggregationType.Should().Be(FirestoreAggregationType.Sum);
        ast.AggregationPropertyName.Should().Be("Weight");
    }

    #endregion

    #region TranslateSum - Invalid Cases

    [Fact]
    public void TranslateSum_WithNullSelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSumRequest(source, null, typeof(int));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().BeNull();
    }

    [Fact]
    public void TranslateSum_WithMethodCallSelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity + 1;
        var request = new TranslateSumRequest(source, selector, typeof(int));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().BeNull();
    }

    #endregion

    #region TranslateSum - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateSum_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;
        var request = new TranslateSumRequest(source, selector, typeof(int));

        var result = FirestoreQueryExpression.TranslateSum(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
    }

    #endregion

    #region TranslateSumRequest Record

    [Fact]
    public void TranslateSumRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;

        var request = new TranslateSumRequest(source, selector, typeof(int));

        request.Source.Should().Be(source);
        request.Selector.Should().Be(selector);
        request.ResultType.Should().Be(typeof(int));
    }

    #endregion
}
