using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreLeftJoinTranslator.
/// Translates LeftJoin expressions to IncludeInfo by extracting navigation information.
/// </summary>
public class FirestoreLeftJoinTranslatorTests
{
    private readonly FirestoreLeftJoinTranslator _translator;

    public FirestoreLeftJoinTranslatorTests()
    {
        _translator = new FirestoreLeftJoinTranslator();
    }

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

    #region Extract from OuterKeySelector - Collection Navigation

    [Fact]
    public void Translate_WithCollectionNavigation_ReturnsIncludeInfo()
    {
        // Arrange
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Pedidos", true, typeof(Pedido)));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // outerKeySelector: c => c.Pedidos
        Expression<Func<Cliente, object>> outerKeySelector = c => c.Pedidos;

        // Act
        var result = _translator.Translate(
            outerKeySelector,
            outerEntityType,
            innerEntityType);

        // Assert
        result.Should().NotBeNull();
        result!.NavigationName.Should().Be("Pedidos");
        result.IsCollection.Should().BeTrue();
    }

    [Fact]
    public void Translate_WithReferenceNavigation_ReturnsIncludeInfo()
    {
        // Arrange
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Vendedor", false, typeof(Vendedor)));
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        // outerKeySelector: c => c.Vendedor
        Expression<Func<Cliente, object>> outerKeySelector = c => c.Vendedor;

        // Act
        var result = _translator.Translate(
            outerKeySelector,
            outerEntityType,
            innerEntityType);

        // Assert
        result.Should().NotBeNull();
        result!.NavigationName.Should().Be("Vendedor");
        result.IsCollection.Should().BeFalse();
    }

    #endregion

    #region Fallback - Detect from EntityTypes

    [Fact]
    public void Translate_WithNonMemberExpression_FallsBackToEntityTypeDetection()
    {
        // Arrange - outerKeySelector is not a simple member expression
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Pedidos", true, typeof(Pedido)));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // outerKeySelector that is NOT a member expression (e.g., parameter itself)
        var parameter = Expression.Parameter(typeof(Cliente), "c");
        var lambda = Expression.Lambda<Func<Cliente, object>>(
            Expression.Convert(parameter, typeof(object)),
            parameter);

        // Act
        var result = _translator.Translate(
            lambda,
            outerEntityType,
            innerEntityType);

        // Assert - Should fallback to matching by target entity type
        result.Should().NotBeNull();
        result!.NavigationName.Should().Be("Pedidos");
        result.IsCollection.Should().BeTrue();
    }

    [Fact]
    public void Translate_WithMultipleNavigationsToSameType_ReturnsFirst()
    {
        // Arrange - Cliente has two navigations to Pedido type
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Pedidos", true, typeof(Pedido)),
            ("PedidosArchivados", true, typeof(Pedido)));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        var parameter = Expression.Parameter(typeof(Cliente), "c");
        var lambda = Expression.Lambda<Func<Cliente, object>>(
            Expression.Convert(parameter, typeof(object)),
            parameter);

        // Act
        var result = _translator.Translate(
            lambda,
            outerEntityType,
            innerEntityType);

        // Assert - Returns first matching navigation
        result.Should().NotBeNull();
        result!.NavigationName.Should().Be("Pedidos");
    }

    #endregion

    #region No Navigation Found

    [Fact]
    public void Translate_WithNoMatchingNavigation_ReturnsNull()
    {
        // Arrange - No navigation from Cliente to Vendedor configured
        var outerEntityType = CreateEntityTypeMock<Cliente>(); // No navigations
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        Expression<Func<Cliente, object>> outerKeySelector = c => c.Nombre; // Not a navigation

        // Act
        var result = _translator.Translate(
            outerKeySelector,
            outerEntityType,
            innerEntityType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Translate_WithMemberNotInEntityType_ReturnsNull()
    {
        // Arrange
        var outerEntityType = CreateEntityTypeMock<Cliente>(); // No navigations configured
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // outerKeySelector points to a member that is not a navigation
        Expression<Func<Cliente, object>> outerKeySelector = c => c.Nombre;

        // Act
        var result = _translator.Translate(
            outerKeySelector,
            outerEntityType,
            innerEntityType);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

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
}
