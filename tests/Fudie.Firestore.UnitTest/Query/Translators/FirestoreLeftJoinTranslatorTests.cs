using System.Linq.Expressions;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreLeftJoinTranslator.
/// Translates LeftJoin expressions to IncludeInfo by extracting navigation information.
/// EF Core generates OuterKeySelector as: Property(entity, "ForeignKeyName")
/// </summary>
public class FirestoreLeftJoinTranslatorTests
{
    private readonly FirestoreLeftJoinTranslator _translator;
    private readonly Mock<IFirestoreCollectionManager> _collectionManagerMock;

    public FirestoreLeftJoinTranslatorTests()
    {
        _collectionManagerMock = new Mock<IFirestoreCollectionManager>();
        _collectionManagerMock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns((Type t) => t.Name.ToLower() + "s");
        _translator = new FirestoreLeftJoinTranslator(_collectionManagerMock.Object);
    }

    #region Test Entities

    private class Cliente
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public string? VendedorId { get; set; }
        public List<Pedido> Pedidos { get; set; } = default!;
        public Vendedor Vendedor { get; set; } = default!;
    }

    private class Pedido
    {
        public string Id { get; set; } = default!;
        public decimal Total { get; set; }
        public string? ClienteId { get; set; }
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
            ("Pedidos", true, typeof(Pedido), "ClienteId"));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // EF Core generates: Property(c, "ClienteId") for collection navigations
        var outerKeySelector = CreatePropertyExpression<Cliente>("ClienteId");

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
            ("Vendedor", false, typeof(Vendedor), "VendedorId"));
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        // EF Core generates: Property(c, "VendedorId") for reference navigations
        var outerKeySelector = CreatePropertyExpression<Cliente>("VendedorId");

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

    #region Fallback - No Match

    [Fact]
    public void Translate_WithNonPropertyExpression_ReturnsNull()
    {
        // Arrange - outerKeySelector is not a Property expression
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Pedidos", true, typeof(Pedido), "ClienteId"));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // outerKeySelector that is NOT a Property expression (e.g., parameter itself)
        var parameter = Expression.Parameter(typeof(Cliente), "c");
        var lambda = Expression.Lambda<Func<Cliente, object>>(
            Expression.Convert(parameter, typeof(object)),
            parameter);

        // Act
        var result = _translator.Translate(
            lambda,
            outerEntityType,
            innerEntityType);

        // Assert - Returns null because it's not a Property expression
        result.Should().BeNull();
    }

    [Fact]
    public void Translate_WithUnknownFkProperty_ReturnsNull()
    {
        // Arrange - FK property doesn't match any navigation
        var outerEntityType = CreateEntityTypeMock<Cliente>(
            ("Pedidos", true, typeof(Pedido), "ClienteId"));
        var innerEntityType = CreateEntityTypeMock<Pedido>();

        // Property with unknown FK name
        var outerKeySelector = CreatePropertyExpression<Cliente>("UnknownId");

        // Act
        var result = _translator.Translate(
            outerKeySelector,
            outerEntityType,
            innerEntityType);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region No Navigation Found

    [Fact]
    public void Translate_WithNoMatchingNavigation_ReturnsNull()
    {
        // Arrange - No navigation from Cliente to Vendedor configured
        var outerEntityType = CreateEntityTypeMock<Cliente>(); // No navigations
        var innerEntityType = CreateEntityTypeMock<Vendedor>();

        var outerKeySelector = CreatePropertyExpression<Cliente>("VendedorId");

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

        // Property that doesn't match any FK
        var outerKeySelector = CreatePropertyExpression<Cliente>("Nombre");

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

    /// <summary>
    /// Creates a Property(entity, "propertyName") expression like EF Core generates.
    /// </summary>
    private static LambdaExpression CreatePropertyExpression<T>(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(T), "e");

        // EF.Property<object>(e, "propertyName")
        var propertyMethod = typeof(EF).GetMethod(
            nameof(EF.Property),
            BindingFlags.Static | BindingFlags.Public)!
            .MakeGenericMethod(typeof(object));

        var propertyCall = Expression.Call(
            propertyMethod,
            parameter,
            Expression.Constant(propertyName));

        return Expression.Lambda<Func<T, object>>(propertyCall, parameter);
    }

    private static IEntityType CreateEntityTypeMock<T>(
        params (string Name, bool IsCollection, Type TargetType, string FkPropertyName)[] navigations)
    {
        var entityTypeMock = new Mock<IEntityType>();
        entityTypeMock.Setup(e => e.ClrType).Returns(typeof(T));

        var navMocks = new List<INavigation>();

        foreach (var (name, isCollection, targetType, fkPropertyName) in navigations)
        {
            var navMock = new Mock<INavigation>();
            navMock.Setup(n => n.Name).Returns(name);
            navMock.Setup(n => n.IsCollection).Returns(isCollection);

            var targetEntityMock = new Mock<IEntityType>();
            targetEntityMock.Setup(e => e.ClrType).Returns(targetType);
            navMock.Setup(n => n.TargetEntityType).Returns(targetEntityMock.Object);

            // Mock FK property
            var fkPropertyMock = new Mock<IProperty>();
            fkPropertyMock.Setup(p => p.Name).Returns(fkPropertyName);

            var fkMock = new Mock<IForeignKey>();
            fkMock.Setup(fk => fk.Properties).Returns(new[] { fkPropertyMock.Object });

            navMock.Setup(n => n.ForeignKey).Returns(fkMock.Object);

            navMocks.Add(navMock.Object);
        }

        entityTypeMock.Setup(e => e.GetNavigations()).Returns(navMocks);
        entityTypeMock.Setup(e => e.FindNavigation(It.IsAny<string>()))
            .Returns((string name) => navMocks.FirstOrDefault(n => n.Name == name));

        return entityTypeMock.Object;
    }

    private static IEntityType CreateEntityTypeMock<T>()
    {
        var entityTypeMock = new Mock<IEntityType>();
        entityTypeMock.Setup(e => e.ClrType).Returns(typeof(T));
        entityTypeMock.Setup(e => e.GetNavigations()).Returns(new List<INavigation>());
        entityTypeMock.Setup(e => e.FindNavigation(It.IsAny<string>())).Returns((INavigation?)null);
        return entityTypeMock.Object;
    }

    #endregion
}
