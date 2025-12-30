using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_LeftJoin partial class (Slice).
/// Tests the static TranslateLeftJoin method that coordinates translation.
/// </summary>
public class FirestoreQueryExpression_LeftJoinTests
{
    #region Test Entities

    private class Cliente
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public List<Pedido> Pedidos { get; set; } = default!;
        public Vendedor Vendedor { get; set; } = default!;
    }

    private class Pedido
    {
        public string Id { get; set; } = default!;
        public decimal Total { get; set; }
    }

    private class Vendedor
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    #endregion

    #region Helper Methods

    private (ShapedQueryExpression Outer, ShapedQueryExpression Inner) CreateShapedQueries(
        params (string Name, bool IsCollection, Type TargetType)[] outerNavigations)
    {
        var outerEntityType = CreateEntityTypeMock<Cliente>(outerNavigations);
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        var outerQuery = CreateShapedQuery(outerEntityType, "clientes");
        var innerQuery = CreateShapedQuery(innerEntityType, "pedidos");

        return (outerQuery, innerQuery);
    }

    private ShapedQueryExpression CreateShapedQuery(IEntityType entityType, string collectionName)
    {
        var queryExpression = new FirestoreQueryExpression(entityType, collectionName);
        var shaperExpression = new StructuralTypeShaperExpression(
            entityType,
            new ProjectionBindingExpression(queryExpression, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);
        return new ShapedQueryExpression(queryExpression, shaperExpression);
    }

    private static IEntityType CreateEntityTypeMock<T>(
        params (string Name, bool IsCollection, Type TargetType)[] navigations)
    {
        var entityTypeMock = new Mock<IEntityType>();
        entityTypeMock.Setup(e => e.ClrType).Returns(typeof(T));

        var navMocks = new List<INavigation>();

        foreach (var (name, isCollection, targetType) in navigations)
        {
            var navMock = new Mock<INavigation>();
            navMock.Setup(n => n.Name).Returns(name);
            navMock.Setup(n => n.IsCollection).Returns(isCollection);

            var targetEntityMock = new Mock<IEntityType>();
            targetEntityMock.Setup(e => e.ClrType).Returns(targetType);
            navMock.Setup(n => n.TargetEntityType).Returns(targetEntityMock.Object);

            navMocks.Add(navMock.Object);
        }

        entityTypeMock.Setup(e => e.GetNavigations()).Returns(navMocks);
        entityTypeMock.Setup(e => e.FindNavigation(It.IsAny<string>()))
            .Returns((string name) => navMocks.FirstOrDefault(n => n.Name == name));

        return entityTypeMock.Object;
    }

    #endregion

    #region TranslateLeftJoin - Collection Navigation

    [Fact]
    public void TranslateLeftJoin_WithCollectionNavigation_AddsInclude()
    {
        // Arrange
        var (outer, inner) = CreateShapedQueries(("Pedidos", true, typeof(Pedido)));

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Pedidos;
        Expression<Func<Pedido, object>> innerKeySelector = p => p.Id;
        Expression<Func<Cliente, Pedido, object>> resultSelector = (c, p) => new { c, p };

        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Act
        var result = FirestoreQueryExpression.TranslateLeftJoin(request);

        // Assert
        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.PendingIncludes.Should().HaveCount(1);
        ast.PendingIncludes[0].NavigationName.Should().Be("Pedidos");
        ast.PendingIncludes[0].IsCollection.Should().BeTrue();
    }

    #endregion

    #region TranslateLeftJoin - Reference Navigation

    [Fact]
    public void TranslateLeftJoin_WithReferenceNavigation_AddsInclude()
    {
        // Arrange
        var outerEntityType = CreateEntityTypeMock<Cliente>(("Vendedor", false, typeof(Vendedor)));
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        var outer = CreateShapedQuery(outerEntityType, "clientes");
        var inner = CreateShapedQuery(innerEntityType, "vendedores");

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Vendedor;
        Expression<Func<Vendedor, object>> innerKeySelector = v => v.Id;
        Expression<Func<Cliente, Vendedor, object>> resultSelector = (c, v) => new { c, v };

        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Act
        var result = FirestoreQueryExpression.TranslateLeftJoin(request);

        // Assert
        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.PendingIncludes.Should().HaveCount(1);
        ast.PendingIncludes[0].NavigationName.Should().Be("Vendedor");
        ast.PendingIncludes[0].IsCollection.Should().BeFalse();
    }

    #endregion

    #region TranslateLeftJoin - Fallback Detection

    [Fact]
    public void TranslateLeftJoin_WithNonMemberSelector_FallsBackToEntityTypeDetection()
    {
        // Arrange
        var (outer, inner) = CreateShapedQueries(("Pedidos", true, typeof(Pedido)));

        // Non-member expression
        var parameter = Expression.Parameter(typeof(Cliente), "c");
        var outerKeySelector = Expression.Lambda<Func<Cliente, object>>(
            Expression.Convert(parameter, typeof(object)), parameter);

        Expression<Func<Pedido, object>> innerKeySelector = p => p.Id;
        Expression<Func<Cliente, Pedido, object>> resultSelector = (c, p) => new { c, p };

        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Act
        var result = FirestoreQueryExpression.TranslateLeftJoin(request);

        // Assert
        result.Should().NotBeNull();
        var ast = (FirestoreQueryExpression)result!.QueryExpression;
        ast.PendingIncludes.Should().HaveCount(1);
        ast.PendingIncludes[0].NavigationName.Should().Be("Pedidos");
    }

    #endregion

    #region TranslateLeftJoin - No Navigation Found

    [Fact]
    public void TranslateLeftJoin_WithNoMatchingNavigation_ThrowsNotSupportedException()
    {
        // Arrange - No navigations configured
        var outerEntityType = CreateEntityTypeMock<Cliente>();
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        var outer = CreateShapedQuery(outerEntityType, "clientes");
        var inner = CreateShapedQuery(innerEntityType, "vendedores");

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Nombre;
        Expression<Func<Vendedor, object>> innerKeySelector = v => v.Id;
        Expression<Func<Cliente, Vendedor, object>> resultSelector = (c, v) => new { c, v };

        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Act & Assert
        Action act = () => FirestoreQueryExpression.TranslateLeftJoin(request);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Firestore does not support real joins*");
    }

    #endregion

    #region TranslateLeftJoin - Returns Updated ShapedQueryExpression

    [Fact]
    public void TranslateLeftJoin_ReturnsUpdatedShapedQueryExpression()
    {
        // Arrange
        var (outer, inner) = CreateShapedQueries(("Pedidos", true, typeof(Pedido)));

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Pedidos;
        Expression<Func<Pedido, object>> innerKeySelector = p => p.Id;
        Expression<Func<Cliente, Pedido, object>> resultSelector = (c, p) => new { c, p };

        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Act
        var result = FirestoreQueryExpression.TranslateLeftJoin(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ShapedQueryExpression>();
        result!.QueryExpression.Should().BeOfType<FirestoreQueryExpression>();
    }

    #endregion

    #region TranslateLeftJoinRequest Record

    [Fact]
    public void TranslateLeftJoinRequest_IsRecord_WithCorrectProperties()
    {
        // Arrange
        var (outer, inner) = CreateShapedQueries(("Pedidos", true, typeof(Pedido)));

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Pedidos;
        Expression<Func<Pedido, object>> innerKeySelector = p => p.Id;
        Expression<Func<Cliente, Pedido, object>> resultSelector = (c, p) => new { c, p };

        // Act
        var request = new TranslateLeftJoinRequest(
            outer, inner, outerKeySelector, innerKeySelector, resultSelector);

        // Assert
        request.Outer.Should().Be(outer);
        request.Inner.Should().Be(inner);
        request.OuterKeySelector.Should().Be(outerKeySelector);
        request.InnerKeySelector.Should().Be(innerKeySelector);
        request.ResultSelector.Should().Be(resultSelector);
    }

    #endregion
}
