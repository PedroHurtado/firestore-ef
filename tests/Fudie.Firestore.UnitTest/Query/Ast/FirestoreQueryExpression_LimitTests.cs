using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Limit partial class.
/// Tests the static TranslateLimit method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_LimitTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_LimitTests()
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

    #region TranslateLimitRequest Record

    [Fact]
    public void TranslateLimitRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(10);

        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        request.Source.Should().Be(source);
        request.Count.Should().Be(countExpr);
        request.IsLimitToLast.Should().BeFalse();
    }

    [Fact]
    public void TranslateLimitRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(5);
        var request = new TranslateLimitRequest(source, countExpr, true);

        var (src, count, isLimitToLast) = request;

        src.Should().Be(source);
        count.Should().Be(countExpr);
        isLimitToLast.Should().BeTrue();
    }

    #endregion

    #region TranslateLimit - Take (IsLimitToLast = false)

    [Fact]
    public void TranslateLimit_WithConstantCount_SetsLimit()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(10);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(10);
        ast.LimitExpression.Should().BeNull();
    }

    [Fact]
    public void TranslateLimit_WithDifferentConstantValues_SetsCorrectLimit()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(25);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(25);
    }

    [Fact]
    public void TranslateLimit_WithParameterizedExpression_SetsLimitExpression()
    {
        var source = CreateShapedQuery();
        // Simulate a closure-captured variable (like EF Core parameters)
        int capturedValue = 15;
        Expression<Func<int>> lambda = () => capturedValue;
        var countExpr = lambda.Body;
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        // Should set Limit from evaluated expression
        ast.Limit.Should().Be(15);
    }

    #endregion

    #region TranslateLimit - TakeLast (IsLimitToLast = true)

    [Fact]
    public void TranslateLimit_WithIsLimitToLastTrue_SetsLimitToLast()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(5);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: true);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.LimitToLast.Should().Be(5);
        ast.Limit.Should().BeNull();
    }

    [Fact]
    public void TranslateLimit_WithIsLimitToLastTrue_DifferentValues_SetsCorrectLimitToLast()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(100);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: true);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.LimitToLast.Should().Be(100);
    }

    [Fact]
    public void TranslateLimit_TakeLast_WithParameterizedExpression_SetsLimitToLast()
    {
        var source = CreateShapedQuery();
        int capturedValue = 20;
        Expression<Func<int>> lambda = () => capturedValue;
        var countExpr = lambda.Body;
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: true);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.LimitToLast.Should().Be(20);
    }

    #endregion

    #region TranslateLimit - Non-evaluable Expression

    [Fact]
    public void TranslateLimit_WithNonEvaluableExpression_SetsLimitExpression()
    {
        var source = CreateShapedQuery();
        // Parameter expression that cannot be evaluated at compile time
        var paramExpr = Expression.Parameter(typeof(int), "count");
        var request = new TranslateLimitRequest(source, paramExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().BeNull();
        ast.LimitExpression.Should().Be(paramExpr);
    }

    [Fact]
    public void TranslateLimit_TakeLast_WithNonEvaluableExpression_SetsLimitToLastExpression()
    {
        var source = CreateShapedQuery();
        var paramExpr = Expression.Parameter(typeof(int), "count");
        var request = new TranslateLimitRequest(source, paramExpr, IsLimitToLast: true);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.LimitToLast.Should().BeNull();
        ast.LimitToLastExpression.Should().Be(paramExpr);
    }

    #endregion

    #region TranslateLimit - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateLimit_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(10);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
    }

    [Fact]
    public void TranslateLimit_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var countExpr = Expression.Constant(10);
        var request = new TranslateLimitRequest(source, countExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion

    #region ExtractIntConstant - Edge Cases

    [Fact]
    public void TranslateLimit_WithConvertExpression_ExtractsCorrectValue()
    {
        var source = CreateShapedQuery();
        // long converted to int
        var longExpr = Expression.Constant(30L);
        var convertExpr = Expression.Convert(longExpr, typeof(int));
        var request = new TranslateLimitRequest(source, convertExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(30);
    }

    [Fact]
    public void TranslateLimit_WithArithmeticExpression_ExtractsCorrectValue()
    {
        var source = CreateShapedQuery();
        // 5 + 5 = 10
        var addExpr = Expression.Add(Expression.Constant(5), Expression.Constant(5));
        var request = new TranslateLimitRequest(source, addExpr, IsLimitToLast: false);

        var result = FirestoreQueryExpression.TranslateLimit(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(10);
    }

    #endregion
}
