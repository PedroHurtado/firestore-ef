using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Skip partial class.
/// Tests the static TranslateSkip method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_SkipTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_SkipTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
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

    #region TranslateSkipRequest Record

    [Fact]
    public void TranslateSkipRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(5);
        var request = new TranslateSkipRequest(source, countExpr);

        var (src, count) = request;

        src.Should().Be(source);
        count.Should().Be(countExpr);
    }

    #endregion

    #region TranslateSkip - Constant Values

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(100)]
    public void TranslateSkip_WithConstantCount_SetsSkip(int value)
    {
        var source = CreateShapedQuery();
        var request = new TranslateSkipRequest(source, Expression.Constant(value));

        var result = FirestoreQueryExpression.TranslateSkip(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Skip.Should().Be(value);
        ast.SkipExpression.Should().BeNull();
    }

    #endregion

    #region TranslateSkip - Parameterized Expressions

    [Fact]
    public void TranslateSkip_WithCapturedVariable_SetsSkip()
    {
        var source = CreateShapedQuery();
        int capturedValue = 15;
        Expression<Func<int>> lambda = () => capturedValue;
        var request = new TranslateSkipRequest(source, lambda.Body);

        var result = FirestoreQueryExpression.TranslateSkip(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Skip.Should().Be(15);
    }

    [Fact]
    public void TranslateSkip_WithNonEvaluableExpression_SetsSkipExpression()
    {
        var source = CreateShapedQuery();
        var paramExpr = Expression.Parameter(typeof(int), "skip");
        var request = new TranslateSkipRequest(source, paramExpr);

        var result = FirestoreQueryExpression.TranslateSkip(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Skip.Should().BeNull();
        ast.SkipExpression.Should().Be(paramExpr);
    }

    #endregion

    #region TranslateSkip - Edge Cases

    public static IEnumerable<object[]> EdgeCaseTestData =>
        new List<object[]>
        {
            // long converted to int
            new object[] { Expression.Convert(Expression.Constant(30L), typeof(int)), 30 },
            // arithmetic: 5 + 5 = 10
            new object[] { Expression.Add(Expression.Constant(5), Expression.Constant(5)), 10 },
            // arithmetic: 10 * 3 = 30
            new object[] { Expression.Multiply(Expression.Constant(10), Expression.Constant(3)), 30 },
        };

    [Theory]
    [MemberData(nameof(EdgeCaseTestData))]
    public void TranslateSkip_WithComplexExpression_ExtractsCorrectValue(Expression countExpr, int expected)
    {
        var source = CreateShapedQuery();
        var request = new TranslateSkipRequest(source, countExpr);

        var result = FirestoreQueryExpression.TranslateSkip(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Skip.Should().Be(expected);
    }

    #endregion

    #region TranslateSkip - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateSkip_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSkipRequest(source, Expression.Constant(10));

        var result = FirestoreQueryExpression.TranslateSkip(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
        result.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion
}
