using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_SingleOrDefault partial class.
/// Tests the static TranslateSingleOrDefault method.
/// SingleOrDefault uses Limit 2 to detect duplicates (EF Core throws if more than one result).
/// </summary>
public class FirestoreQueryExpression_SingleOrDefaultTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_SingleOrDefaultTests()
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

    #region TranslateSingleOrDefaultRequest Record

    [Fact]
    public void TranslateSingleOrDefaultRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateSingleOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var (src, pred, returnType, returnDefault) = request;

        src.Should().Be(source);
        pred.Should().Be(predicate);
        returnType.Should().Be(typeof(TestEntity));
        returnDefault.Should().BeTrue();
    }

    [Fact]
    public void TranslateSingleOrDefaultRequest_Single_HasReturnDefaultFalse()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        request.ReturnDefault.Should().BeFalse();
    }

    [Fact]
    public void TranslateSingleOrDefaultRequest_SingleOrDefault_HasReturnDefaultTrue()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        request.ReturnDefault.Should().BeTrue();
    }

    #endregion

    #region TranslateSingleOrDefault - No Predicate

    [Fact]
    public void TranslateSingleOrDefault_WithoutPredicate_AppliesLimit2()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        // SingleOrDefault uses Limit 2 to detect duplicates
        ast.Limit.Should().Be(2);
    }

    [Fact]
    public void TranslateSingleOrDefault_WithoutPredicate_StoresReturnDefault()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeTrue();
    }

    [Fact]
    public void TranslateSingleOrDefault_WithoutPredicate_StoresReturnType()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnType.Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void TranslateSingleOrDefault_Single_WithoutPredicate_AppliesLimit2()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(2);
    }

    [Fact]
    public void TranslateSingleOrDefault_Single_WithoutPredicate_StoresReturnDefaultFalse()
    {
        var source = CreateShapedQuery();
        var request = new TranslateSingleOrDefaultRequest(source, null, typeof(TestEntity), ReturnDefault: false);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.ReturnDefault.Should().BeFalse();
    }

    #endregion

    #region TranslateSingleOrDefault - With Predicate (No Id Optimization)

    [Fact]
    public void TranslateSingleOrDefault_WithIdPredicate_DoesNotApplyIdOptimization()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateSingleOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        // SingleOrDefault cannot use Id optimization because it needs to detect duplicates
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Limit.Should().Be(2);
    }

    [Fact]
    public void TranslateSingleOrDefault_WithIdPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateSingleOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
    }

    [Fact]
    public void TranslateSingleOrDefault_WithNonIdPredicate_AddsFilter()
    {
        var source = CreateShapedQuery();
        var predicate = CreateNamePredicate("test");
        var request = new TranslateSingleOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.IsIdOnlyQuery.Should().BeFalse();
        ast.Limit.Should().Be(2);
        ast.Filters.Should().HaveCount(1);
    }

    #endregion

    #region TranslateSingleOrDefault - Preserves Shaper

    [Fact]
    public void TranslateSingleOrDefault_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var predicate = CreateIdPredicate("doc-123");
        var request = new TranslateSingleOrDefaultRequest(source, predicate, typeof(TestEntity), ReturnDefault: true);

        var result = FirestoreQueryExpression.TranslateSingleOrDefault(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion
}
