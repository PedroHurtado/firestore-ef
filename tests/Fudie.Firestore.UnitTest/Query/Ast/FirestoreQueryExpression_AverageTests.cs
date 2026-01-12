using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Average partial class.
/// </summary>
public class FirestoreQueryExpression_AverageTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_AverageTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
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

    [Fact]
    public void TranslateAverage_WithDecimalSelector_SetsAggregation()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;
        var request = new TranslateAverageRequest(source, selector, typeof(decimal));

        var result = FirestoreQueryExpression.TranslateAverage(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.AggregationType.Should().Be(FirestoreAggregationType.Average);
        ast.AggregationPropertyName.Should().Be("Price");
        ast.AggregationResultType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void TranslateAverage_WithNullSelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        var request = new TranslateAverageRequest(source, null, typeof(decimal));

        var result = FirestoreQueryExpression.TranslateAverage(request);

        result.Should().BeNull();
    }

    [Fact]
    public void TranslateAverage_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, double>> selector = e => e.Weight;
        var request = new TranslateAverageRequest(source, selector, typeof(double));

        var result = FirestoreQueryExpression.TranslateAverage(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
    }

    [Fact]
    public void TranslateAverageRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;

        var request = new TranslateAverageRequest(source, selector, typeof(decimal));

        request.Source.Should().Be(source);
        request.Selector.Should().Be(selector);
        request.ResultType.Should().Be(typeof(decimal));
    }
}
