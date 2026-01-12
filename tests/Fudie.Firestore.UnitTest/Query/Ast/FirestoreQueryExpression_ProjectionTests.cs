using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Projection partial class (Slice).
/// Tests the static TranslateSelect method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_ProjectionTests
{
    private readonly Mock<IEntityType> _entityTypeMock;
    private readonly IFirestoreCollectionManager _collectionManager;

    public FirestoreQueryExpression_ProjectionTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));

        var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
        collectionManagerMock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns((Type t) => t.Name.ToLower() + "s");
        _collectionManager = collectionManagerMock.Object;
    }

    private TranslateSelectRequest CreateRequest(ShapedQueryExpression source, LambdaExpression selector)
        => new TranslateSelectRequest(source, selector, _collectionManager);

    #region Test Entities

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public Address Address { get; set; } = default!;
        public List<Order> Orders { get; set; } = default!;
    }

    private class Address
    {
        public string City { get; set; } = default!;
    }

    private class Order
    {
        public string OrderId { get; set; } = default!;
        public decimal Total { get; set; }
    }

    private record TestRecord(string Id, string Name);

    #endregion

    private ShapedQueryExpression CreateShapedQuery()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var shaperExpression = new StructuralTypeShaperExpression(
            _entityTypeMock.Object,
            new ProjectionBindingExpression(queryExpression, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);
        return new ShapedQueryExpression(queryExpression, shaperExpression);
    }

    #region TranslateSelect - Identity Projection

    [Fact]
    public void TranslateSelect_IdentityProjection_DoesNotSetProjection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, TestEntity>> selector = e => e;
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.HasProjection.Should().BeFalse();
        ast.Projection.Should().BeNull();
    }

    [Fact]
    public void TranslateSelect_TypeConversion_DoesNotSetProjection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, object>> selector = e => e;
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.HasProjection.Should().BeFalse();
    }

    #endregion

    #region TranslateSelect - Single Field

    [Fact]
    public void TranslateSelect_SingleField_SetsProjection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Name;
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.HasProjection.Should().BeTrue();
        ast.Projection!.ResultType.Should().Be(ProjectionResultType.SingleField);
        ast.Projection.Fields.Should().HaveCount(1);
        ast.Projection.Fields![0].FieldPath.Should().Be("Name");
    }

    [Fact]
    public void TranslateSelect_NestedField_SetsCorrectPath()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Address.City;
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Projection!.Fields![0].FieldPath.Should().Be("Address.City");
    }

    #endregion

    #region TranslateSelect - Anonymous Type

    [Fact]
    public void TranslateSelect_AnonymousType_SetsProjection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, object>> selector = e => new { e.Id, e.Name };
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.HasProjection.Should().BeTrue();
        ast.Projection!.ResultType.Should().Be(ProjectionResultType.AnonymousType);
        ast.Projection.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void TranslateSelect_AnonymousTypeWithAlias_PreservesAliases()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, object>> selector = e => new { ProductId = e.Id, ProductName = e.Name };
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Projection!.Fields![0].ResultName.Should().Be("ProductId");
        ast.Projection.Fields[1].ResultName.Should().Be("ProductName");
    }

    #endregion

    #region TranslateSelect - Record

    [Fact]
    public void TranslateSelect_Record_SetsProjection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, TestRecord>> selector = e => new TestRecord(e.Id, e.Name);
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Projection!.ResultType.Should().Be(ProjectionResultType.Record);
        ast.Projection.ClrType.Should().Be(typeof(TestRecord));
    }

    #endregion

    #region TranslateSelect - Subcollections

    [Fact]
    public void TranslateSelect_WithSubcollection_IncludesSubcollection()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, object>> selector = e => new { e.Name, e.Orders };
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Projection!.Subcollections.Should().HaveCount(1);
        ast.Projection.Subcollections[0].NavigationName.Should().Be("Orders");
    }

    [Fact]
    public void TranslateSelect_SubcollectionWithOperations_ExtractsOperations()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, object>> selector = e => new
        {
            e.Name,
            TopOrders = e.Orders.OrderByDescending(o => o.Total).Take(3)
        };
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.Projection!.Subcollections.Should().HaveCount(1);
        ast.Projection.Subcollections[0].ResultName.Should().Be("TopOrders");
        ast.Projection.Subcollections[0].OrderByClauses.Should().HaveCount(1);
        ast.Projection.Subcollections[0].Limit.Should().Be(3);
    }

    #endregion

    #region TranslateSelect - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateSelect_ReturnsUpdatedShapedQueryExpression()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Name;
        var request = CreateRequest(source, selector);

        var result = FirestoreQueryExpression.TranslateSelect(request);

        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
    }

    #endregion

    #region TranslateSelect - Unsupported Cases

    [Fact]
    public void TranslateSelect_BinaryExpression_ThrowsNotSupportedException()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price * 1.21m;
        var request = CreateRequest(source, selector);

        Action act = () => FirestoreQueryExpression.TranslateSelect(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Binary*");
    }

    [Fact]
    public void TranslateSelect_MethodCall_ThrowsNotSupportedException()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Name.ToUpper();
        var request = CreateRequest(source, selector);

        Action act = () => FirestoreQueryExpression.TranslateSelect(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Method*");
    }

    #endregion

    #region TranslateSelectRequest Record

    [Fact]
    public void TranslateSelectRequest_IsRecord_WithCorrectProperties()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Name;

        var request = CreateRequest(source, selector);

        request.Source.Should().Be(source);
        request.Selector.Should().Be(selector);
    }

    [Fact]
    public void TranslateSelectRequest_SupportsDeconstruction()
    {
        var source = CreateShapedQuery();
        Expression<Func<TestEntity, string>> selector = e => e.Name;
        var request = CreateRequest(source, selector);

        var (src, sel, cm) = request;

        src.Should().Be(source);
        sel.Should().Be(selector);
        cm.Should().Be(_collectionManager);
    }

    #endregion
}
