using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Min partial class.
/// </summary>
public class FirestoreQueryExpression_MinTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_MinTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public int Quantity { get; set; }
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

    [Fact]
    public void TranslateMin_WithIntSelector_SetsAggregation()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;
        var request = new TranslateMinRequest(source, selector, typeof(int));

        var result = FirestoreQueryExpression.TranslateMin(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.AggregationType.Should().Be(FirestoreAggregationType.Min);
        ast.AggregationPropertyName.Should().Be("Quantity");
        ast.AggregationResultType.Should().Be(typeof(int));
    }

    [Fact]
    public void TranslateMin_WithNullSelector_ReturnsNull()
    {
        var source = CreateShapedQuery();
        var request = new TranslateMinRequest(source, null, typeof(int));

        var result = FirestoreQueryExpression.TranslateMin(request);

        result.Should().BeNull();
    }

    [Fact]
    public void TranslateMin_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;
        var request = new TranslateMinRequest(source, selector, typeof(decimal));

        var result = FirestoreQueryExpression.TranslateMin(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
    }

    [Fact]
    public void TranslateMinRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;

        var request = new TranslateMinRequest(source, selector, typeof(int));

        request.Source.Should().Be(source);
        request.Selector.Should().Be(selector);
        request.ResultType.Should().Be(typeof(int));
    }
}
