using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreProjectionTranslator.
/// Tests the Translator component that coordinates projection extraction.
/// </summary>
public class FirestoreProjectionTranslatorTests
{
    private readonly FirestoreProjectionTranslator _translator;

    public FirestoreProjectionTranslatorTests()
    {
        var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
        collectionManagerMock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns((Type t) => t.Name.ToLower() + "s");
        _translator = new FirestoreProjectionTranslator(
            collectionManagerMock.Object,
            null,
            Array.Empty<IncludeInfo>());
    }

    #region Test Entities

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public Address Address { get; set; } = default!;
        public List<Order> Orders { get; set; } = default!;
    }

    private class Address
    {
        public string City { get; set; } = default!;
        public string Street { get; set; } = default!;
    }

    private class Order
    {
        public string OrderId { get; set; } = default!;
        public decimal Total { get; set; }
        public string Status { get; set; } = default!;
    }

    private record TestRecord(string Id, string Name);

    private class TestDto
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    #endregion

    #region Identity Projection (e => e) - No Projection Needed

    [Fact]
    public void Translate_IdentityProjection_ReturnsNull()
    {
        // e => e (no projection needed)
        Expression<Func<TestEntity, TestEntity>> selector = e => e;

        var result = _translator.Translate(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_TypeConversionProjection_ReturnsNull()
    {
        // e => (object)e (no projection needed)
        Expression<Func<TestEntity, object>> selector = e => e;

        var result = _translator.Translate(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_NullSelector_ReturnsNull()
    {
        var result = _translator.Translate(null!);

        result.Should().BeNull();
    }

    #endregion

    #region Single Field Projection (e => e.Name)

    [Fact]
    public void Translate_SingleField_ReturnsCorrectProjection()
    {
        Expression<Func<TestEntity, string>> selector = e => e.Name;

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.ResultType.Should().Be(ProjectionResultType.SingleField);
        result.ClrType.Should().Be(typeof(string));
        result.Fields.Should().HaveCount(1);
        result.Fields![0].FieldPath.Should().Be("Name");
        result.Fields[0].ResultName.Should().Be("Name");
    }

    [Fact]
    public void Translate_NestedField_ReturnsCorrectPath()
    {
        Expression<Func<TestEntity, string>> selector = e => e.Address.City;

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.ResultType.Should().Be(ProjectionResultType.SingleField);
        result.Fields.Should().HaveCount(1);
        result.Fields![0].FieldPath.Should().Be("Address.City");
    }

    #endregion

    #region Anonymous Type Projection (e => new { e.Id, e.Name })

    [Fact]
    public void Translate_AnonymousType_ReturnsCorrectProjection()
    {
        Expression<Func<TestEntity, object>> selector = e => new { e.Id, e.Name };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.ResultType.Should().Be(ProjectionResultType.AnonymousType);
        result.Fields.Should().HaveCount(2);
        result.Fields![0].ResultName.Should().Be("Id");
        result.Fields[1].ResultName.Should().Be("Name");
    }

    [Fact]
    public void Translate_AnonymousType_WithAlias_ReturnsCorrectNames()
    {
        Expression<Func<TestEntity, object>> selector = e => new { ProductId = e.Id, ProductName = e.Name };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.Fields.Should().HaveCount(2);
        result.Fields![0].FieldPath.Should().Be("Id");
        result.Fields[0].ResultName.Should().Be("ProductId");
        result.Fields[1].FieldPath.Should().Be("Name");
        result.Fields[1].ResultName.Should().Be("ProductName");
    }

    #endregion

    #region DTO Class Projection (e => new Dto { Id = e.Id })

    [Fact]
    public void Translate_DtoClass_ReturnsCorrectProjection()
    {
        Expression<Func<TestEntity, TestDto>> selector = e => new TestDto { Id = e.Id, Name = e.Name };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.ResultType.Should().Be(ProjectionResultType.DtoClass);
        result.ClrType.Should().Be(typeof(TestDto));
        result.Fields.Should().HaveCount(2);
    }

    #endregion

    #region Record Projection (e => new Record(e.Id, e.Name))

    [Fact]
    public void Translate_Record_ReturnsCorrectProjection()
    {
        Expression<Func<TestEntity, TestRecord>> selector = e => new TestRecord(e.Id, e.Name);

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.ResultType.Should().Be(ProjectionResultType.Record);
        result.ClrType.Should().Be(typeof(TestRecord));
        result.Fields.Should().HaveCount(2);
    }

    #endregion

    #region Subcollection Projections

    [Fact]
    public void Translate_WithSubcollection_ExtractsSubcollection()
    {
        Expression<Func<TestEntity, object>> selector = e => new { e.Name, e.Orders };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.Fields.Should().HaveCount(1);
        result.Fields![0].FieldPath.Should().Be("Name");
        result.Subcollections.Should().HaveCount(1);
        result.Subcollections[0].NavigationName.Should().Be("Orders");
    }

    [Fact]
    public void Translate_SubcollectionWithFilter_ExtractsFilter()
    {
        Expression<Func<TestEntity, object>> selector = e => new
        {
            e.Name,
            ConfirmedOrders = e.Orders.Where(o => o.Status == "Confirmed")
        };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.Subcollections.Should().HaveCount(1);
        result.Subcollections[0].ResultName.Should().Be("ConfirmedOrders");
        result.Subcollections[0].Filters.Should().HaveCount(1);
        result.Subcollections[0].Filters[0].PropertyName.Should().Be("Status");
    }

    [Fact]
    public void Translate_SubcollectionWithOrderBy_ExtractsOrdering()
    {
        Expression<Func<TestEntity, object>> selector = e => new
        {
            e.Name,
            TopOrders = e.Orders.OrderByDescending(o => o.Total)
        };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.Subcollections.Should().HaveCount(1);
        result.Subcollections[0].OrderByClauses.Should().HaveCount(1);
        result.Subcollections[0].OrderByClauses[0].PropertyName.Should().Be("Total");
        result.Subcollections[0].OrderByClauses[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Translate_SubcollectionWithTake_ExtractsLimit()
    {
        Expression<Func<TestEntity, object>> selector = e => new
        {
            e.Name,
            TopOrders = e.Orders.OrderByDescending(o => o.Total).Take(3)
        };

        var result = _translator.Translate(selector);

        result.Should().NotBeNull();
        result!.Subcollections.Should().HaveCount(1);
        result.Subcollections[0].Limit.Should().Be(3);
    }

    #endregion

    #region Unsupported Cases

    [Fact]
    public void Translate_BinaryExpression_ThrowsNotSupportedException()
    {
        // e => e.Price * 1.21m
        Expression<Func<TestEntity, decimal>> selector = e => e.Price * 1.21m;

        Action act = () => _translator.Translate(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Binary*");
    }

    [Fact]
    public void Translate_ConditionalExpression_ThrowsNotSupportedException()
    {
        // e => e.Stock > 0 ? "InStock" : "OutOfStock"
        Expression<Func<TestEntity, string>> selector = e => e.Stock > 0 ? "InStock" : "OutOfStock";

        Action act = () => _translator.Translate(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Conditional*");
    }

    [Fact]
    public void Translate_MethodCallOnProperty_ThrowsNotSupportedException()
    {
        // e => e.Name.ToUpper()
        Expression<Func<TestEntity, string>> selector = e => e.Name.ToUpper();

        Action act = () => _translator.Translate(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Method*");
    }

    #endregion
}
