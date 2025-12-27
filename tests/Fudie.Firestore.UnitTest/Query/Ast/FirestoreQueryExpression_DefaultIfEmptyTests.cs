using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_DefaultIfEmpty partial class.
/// Tests the static TranslateDefaultIfEmpty method.
/// DefaultIfEmpty returns a collection with a single default value if the source is empty.
/// </summary>
public class FirestoreQueryExpression_DefaultIfEmptyTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_DefaultIfEmptyTests()
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

    #region TranslateDefaultIfEmptyRequest Record

    [Fact]
    public void TranslateDefaultIfEmptyRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        var defaultValue = Expression.Constant(new TestEntity { Id = "default", Name = "Default" });
        var request = new TranslateDefaultIfEmptyRequest(source, defaultValue);

        var (src, defVal) = request;

        src.Should().Be(source);
        defVal.Should().Be(defaultValue);
    }

    [Fact]
    public void TranslateDefaultIfEmptyRequest_WithNullDefaultValue_HasNullDefaultValue()
    {
        var source = CreateShapedQuery();
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        request.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void TranslateDefaultIfEmptyRequest_WithDefaultValue_HasDefaultValue()
    {
        var source = CreateShapedQuery();
        var defaultValue = Expression.Constant(new TestEntity { Id = "default", Name = "Default" });
        var request = new TranslateDefaultIfEmptyRequest(source, defaultValue);

        request.DefaultValue.Should().Be(defaultValue);
    }

    #endregion

    #region TranslateDefaultIfEmpty - Stores DefaultValue

    [Fact]
    public void TranslateDefaultIfEmpty_WithNullDefaultValue_StoresNull()
    {
        var source = CreateShapedQuery();
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.DefaultValueExpression.Should().BeNull();
    }

    [Fact]
    public void TranslateDefaultIfEmpty_WithDefaultValue_StoresDefaultValue()
    {
        var source = CreateShapedQuery();
        var defaultValue = Expression.Constant(new TestEntity { Id = "default", Name = "Default" });
        var request = new TranslateDefaultIfEmptyRequest(source, defaultValue);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.DefaultValueExpression.Should().Be(defaultValue);
    }

    [Fact]
    public void TranslateDefaultIfEmpty_SetsHasDefaultIfEmpty()
    {
        var source = CreateShapedQuery();
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.HasDefaultIfEmpty.Should().BeTrue();
    }

    #endregion

    #region TranslateDefaultIfEmpty - Preserves Shaper

    [Fact]
    public void TranslateDefaultIfEmpty_PreservesShaperExpression()
    {
        var source = CreateShapedQuery();
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        result!.ShaperExpression.Should().Be(source.ShaperExpression);
    }

    #endregion

    #region TranslateDefaultIfEmpty - Preserves Existing Query State

    [Fact]
    public void TranslateDefaultIfEmpty_PreservesExistingFilters()
    {
        var filter = new FirestoreWhereClause("Status", FirestoreOperator.EqualTo, Expression.Constant("active"));
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter);
        var source = CreateShapedQuery(queryExpr);
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Filters.Should().HaveCount(1);
        ast.HasDefaultIfEmpty.Should().BeTrue();
    }

    [Fact]
    public void TranslateDefaultIfEmpty_PreservesExistingLimit()
    {
        var queryExpr = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithLimit(10);
        var source = CreateShapedQuery(queryExpr);
        var request = new TranslateDefaultIfEmptyRequest(source, null);

        var result = FirestoreQueryExpression.TranslateDefaultIfEmpty(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Limit.Should().Be(10);
        ast.HasDefaultIfEmpty.Should().BeTrue();
    }

    #endregion
}
