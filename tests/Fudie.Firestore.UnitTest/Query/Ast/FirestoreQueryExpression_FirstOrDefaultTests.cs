using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_FirstOrDefault partial class.
/// Tests the static TranslateFirstOrDefault method with Id optimization.
/// </summary>
public class FirestoreQueryExpression_FirstOrDefaultTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_FirstOrDefaultTests()
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

    private static LambdaExpression CreateIdPredicate(string idValue)
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Id == idValue;
        return expr;
    }

    private static LambdaExpression CreateNamePredicate(string nameValue)
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Name == nameValue;
        return expr;
    }

    #region TranslateFirstOrDefaultRequest Record

    [Fact]
    public void TranslateFirstOrDefaultRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var (src, pred, returnType, returnDefault) = request;

        src.Should().Be(source);
        pred.Should().Be(predicate);
        returnType.Should().Be(typeof(TestEntity));
        returnDefault.Should().BeTrue();
    }

    [Fact]
    public void TranslateFirstOrDefaultRequest_First_HasReturnDefaultFalse()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        request.ReturnDefault.Should().BeFalse();
    }

    [Fact]
    public void TranslateFirstOrDefaultRequest_FirstOrDefault_HasReturnDefaultTrue()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        request.ReturnDefault.Should().BeTrue();
    }

    #endregion

    #region TranslateFirstOrDefault - No Predicate

    [Fact]
    public void TranslateFirstOrDefault_WithoutPredicate_AppliesLimit1()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(1);
        ast.IsIdOnlyQuery.Should().BeFalse();
    }

    [Fact]
    public void TranslateFirstOrDefault_WithoutPredicate_StoresReturnDefault()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeTrue();
    }

    [Fact]
    public void TranslateFirstOrDefault_WithoutPredicate_StoresReturnType()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnType.Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void TranslateFirstOrDefault_First_WithoutPredicate_AppliesLimit1()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(1);
    }

    [Fact]
    public void TranslateFirstOrDefault_First_WithoutPredicate_StoresReturnDefaultFalse()
    {
        var source = CreateShapedQuery();
        var request = new TranslateFirstOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeFalse();
    }

    #endregion

    #region TranslateFirstOrDefault - Id Optimization Applied

    [Fact]
    public void TranslateFirstOrDefault_WithIdPredicate_AppliesIdOptimization()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeTrue();
        ast.IdValueExpression.Should().NotBeNull();
    }

    [Fact]
    public void TranslateFirstOrDefault_WithIdPredicate_DoesNotApplyLimit()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        // Id optimization means we use GetDocumentAsync, no need for Limit
        ast.Limit.Should().BeNull();
    }

    [Fact]
    public void TranslateFirstOrDefault_WithIdPredicate_StoresReturnDefault()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeTrue();
    }

    [Fact]
    public void TranslateFirstOrDefault_First_WithIdPredicate_AppliesIdOptimization()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeTrue();
    }

    [Fact]
    public void TranslateFirstOrDefault_First_WithIdPredicate_StoresReturnDefaultFalse()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeFalse();
    }

    #endregion

    #region TranslateFirstOrDefault - Non-Id Predicate Adds Filter

    [Fact]
    public void TranslateFirstOrDefault_WithNonIdPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Limit.Should().Be(1);
        ast.Filters.Should().HaveCount(1);
    }

    [Fact]
    public void TranslateFirstOrDefault_First_WithNonIdPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Limit.Should().Be(1);
    }

    #endregion

    #region TranslateFirstOrDefault - Existing Filters Prevent Id Optimization

    [Fact]
    public void TranslateFirstOrDefault_WithExistingFilters_DoesNotApplyIdOptimization()
    {
        var existingFilter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(existingFilter);
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Limit.Should().Be(1);
    }

    [Fact]
    public void TranslateFirstOrDefault_WithExistingOrGroups_DoesNotApplyIdOptimization()
    {
        var orClause = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var orGroup = new FirestoreOrFilterGroup(new[] { orClause });
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddOrFilterGroup(orGroup);
        var source = CreateShapedQuery(queryExpr);
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
    }

    #endregion

    #region TranslateFirstOrDefault - Preserves Shaper

    [Fact]
    public void TranslateFirstOrDefault_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateFirstOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateFirstOrDefault(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion

}
