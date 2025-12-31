using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System.Linq.Expressions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for IncludeExtractionVisitor.
/// Tests the visitor pattern to extract Include/ThenInclude from expression trees.
/// </summary>
public class IncludeExtractionVisitorTests
{
    #region Test Entities

    private class Cliente
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public List<Pedido> Pedidos { get; set; } = new();
    }

    private class Pedido
    {
        public string Id { get; set; } = default!;
        public DateTime FechaPedido { get; set; }
        public List<LineaPedido> Lineas { get; set; } = new();
    }

    private class LineaPedido
    {
        public string Id { get; set; } = default!;
        public string ProductoId { get; set; } = default!;
        public int Cantidad { get; set; }
        public Producto? Producto { get; set; }  // Reference navigation
    }

    private class Producto
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public decimal Precio { get; set; }
    }

    /// <summary>
    /// ValueObject (ComplexType) that contains a Reference navigation.
    /// Example: Pedido.Direccion.Vendedor where Direccion is a ComplexType and Vendedor is a Reference.
    /// </summary>
    private class DireccionEntrega
    {
        public string Calle { get; set; } = default!;
        public string Ciudad { get; set; } = default!;
        public Vendedor? Vendedor { get; set; }  // Reference inside ComplexType
    }

    private class Vendedor
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    /// <summary>
    /// Mock class that simulates MaterializeCollectionNavigationExpression.
    /// EF Core uses this internal class to wrap collection navigations.
    /// The key property is Subquery which contains the nested IncludeExpression for ThenInclude.
    /// </summary>
    private class MaterializeCollectionNavigationExpression : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type { get; }
        public Expression Subquery { get; }
        public INavigationBase Navigation { get; }

        public MaterializeCollectionNavigationExpression(Expression subquery, INavigationBase navigation, Type type)
        {
            Subquery = subquery;
            Navigation = navigation;
            Type = type;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newSubquery = visitor.Visit(Subquery);
            if (newSubquery != Subquery)
            {
                return new MaterializeCollectionNavigationExpression(newSubquery, Navigation, Type);
            }
            return this;
        }
    }

    #endregion

    #region Helper Methods

    private static IFirestoreCollectionManager CreateCollectionManagerMock()
    {
        var mock = new Mock<IFirestoreCollectionManager>();
        mock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns((Type t) => t.Name.ToLower() + "s");
        return mock.Object;
    }

    private static IReadOnlyNavigation CreateNavigationMock(string name, bool isCollection, Type? targetType = null)
    {
        var navMock = new Mock<IReadOnlyNavigation>();
        navMock.Setup(n => n.Name).Returns(name);
        navMock.Setup(n => n.IsCollection).Returns(isCollection);
        navMock.As<INavigationBase>().Setup(n => n.Name).Returns(name);
        navMock.As<INavigationBase>().Setup(n => n.IsCollection).Returns(isCollection);

        // Setup TargetEntityType
        var entityTypeMock = new Mock<IEntityType>();
        entityTypeMock.Setup(e => e.ClrType).Returns(targetType ?? typeof(object));
        navMock.Setup(n => n.TargetEntityType).Returns(entityTypeMock.Object);

        return navMock.Object;
    }

    /// <summary>
    /// Creates a filtered expression chain for Lineas:
    /// .Where(l => l.Cantidad > 0)
    /// .OrderBy(l => l.ProductoId)
    /// .Skip(1)
    /// .Take(5)
    /// </summary>
    private Expression CreateLineasFilteredExpression(ParameterExpression lineaParam)
    {
        var source = Expression.Parameter(typeof(IEnumerable<LineaPedido>), "source");

        // Where predicate: l => l.Cantidad > 0
        var cantidadProperty = Expression.Property(lineaParam, "Cantidad");
        var zero = Expression.Constant(0);
        var comparison = Expression.GreaterThan(cantidadProperty, zero);
        var wherePredicate = Expression.Lambda<Func<LineaPedido, bool>>(comparison, lineaParam);

        var whereMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LineaPedido));
        var whereCall = Expression.Call(whereMethod, source, wherePredicate);

        // OrderBy: l => l.ProductoId
        var productoIdProperty = Expression.Property(lineaParam, "ProductoId");
        var orderBySelector = Expression.Lambda<Func<LineaPedido, string>>(productoIdProperty, lineaParam);

        var orderByMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LineaPedido), typeof(string));
        var orderByCall = Expression.Call(orderByMethod, whereCall, orderBySelector);

        // Skip(1)
        var skipMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Skip" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LineaPedido));
        var skipCall = Expression.Call(skipMethod, orderByCall, Expression.Constant(1));

        // Take(5)
        var takeMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Take" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LineaPedido));
        var takeCall = Expression.Call(takeMethod, skipCall, Expression.Constant(5));

        return takeCall;
    }

    #endregion

    /// <summary>
    /// Tests the REAL structure that EF Core generates for Include + ThenInclude:
    ///
    /// Query: .Include(c => c.Pedidos.Where(...).OrderByDescending(...).Take(10))
    ///        .ThenInclude(p => p.Lineas.Where(...).OrderBy(...).Skip(1).Take(5))
    ///
    /// EF Core structure (discovered via integration tests):
    /// IncludeExpression (Pedidos)
    /// ├── EntityExpression: ParameterExpression (Cliente)
    /// └── NavigationExpression: MaterializeCollectionNavigationExpression
    ///     └── Subquery: .Where(correlation).Select(p => IncludeExpression(Lineas))
    ///
    /// Key insight: ThenInclude is NOT in EntityExpression, it's inside NavigationExpression.Subquery
    /// </summary>
    [Fact]
    public void Visit_RealEFCoreStructure_IncludeWithThenIncludeInSubquery_ShouldDetectBothNavigations()
    {
        // Arrange
        var clienteParam = Expression.Parameter(typeof(Cliente), "c");
        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");

        var pedidosNavigation = CreateNavigationMock("Pedidos", isCollection: true);
        var lineasNavigation = CreateNavigationMock("Lineas", isCollection: true);

        // Build the INNER IncludeExpression (Lineas - ThenInclude)
        var lineasNavigationExpr = CreateLineasFilteredExpression(Expression.Parameter(typeof(LineaPedido), "l"));
        var lineasInclude = new IncludeExpression(
            entityExpression: pedidoParam,
            navigationExpression: lineasNavigationExpr,
            navigation: (INavigationBase)lineasNavigation);

        // Build the Subquery: .Select(p => IncludeExpression(Lineas))
        var selectMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Pedido), typeof(Pedido));

        var sourceForSelect = Expression.Parameter(typeof(IEnumerable<Pedido>), "source");
        var selectLambda = Expression.Lambda<Func<Pedido, Pedido>>(lineasInclude, pedidoParam);
        var selectCall = Expression.Call(selectMethod, sourceForSelect, selectLambda);

        // Build MaterializeCollectionNavigationExpression (mock)
        var materializeExpr = new MaterializeCollectionNavigationExpression(
            subquery: selectCall,
            navigation: (INavigationBase)pedidosNavigation,
            type: typeof(List<Pedido>));

        // Build the OUTER IncludeExpression (Pedidos)
        var pedidosInclude = new IncludeExpression(
            entityExpression: clienteParam,
            navigationExpression: materializeExpr,
            navigation: (INavigationBase)pedidosNavigation);

        // Act
        var visitor = new IncludeExtractionVisitor(CreateCollectionManagerMock());
        visitor.Visit(pedidosInclude);

        // Assert - Should detect BOTH navigations
        visitor.DetectedNavigations.Should().HaveCount(2,
            "Should detect both Pedidos (outer) and Lineas (inside Subquery)");
        visitor.DetectedNavigations.Select(n => n.Name).Should().Contain("Pedidos");
        visitor.DetectedNavigations.Select(n => n.Name).Should().Contain("Lineas");

        visitor.DetectedIncludes.Should().HaveCount(2);

        // Verify Pedidos include
        var pedidosInfo = visitor.DetectedIncludes.FirstOrDefault(i => i.NavigationName == "Pedidos");
        pedidosInfo.Should().NotBeNull();
        pedidosInfo!.IsCollection.Should().BeTrue();

        // Verify Lineas include (the ThenInclude that was inside Subquery)
        var lineasInfo = visitor.DetectedIncludes.FirstOrDefault(i => i.NavigationName == "Lineas");
        lineasInfo.Should().NotBeNull("ThenInclude (Lineas) should be detected from Subquery");
        lineasInfo!.IsCollection.Should().BeTrue();
        lineasInfo.HasOperations.Should().BeTrue();
        lineasInfo.Filters.Should().HaveCount(1, "Lineas has Where(l => l.Cantidad > 0)");
        lineasInfo.OrderByClauses.Should().HaveCount(1, "Lineas has OrderBy(l => l.ProductoId)");
        lineasInfo.Skip.Should().Be(1, "Lineas has Skip(1)");
        lineasInfo.Take.Should().Be(5, "Lineas has Take(5)");
    }

    /// <summary>
    /// Tests Reference navigation (single entity, not collection).
    /// Query: .Include(l => l.Producto)
    ///
    /// For References, EF Core generates a simple IncludeExpression with IsCollection = false.
    /// No MaterializeCollectionNavigationExpression is used.
    /// </summary>
    [Fact]
    public void Visit_ReferenceNavigation_ShouldDetectWithIsCollectionFalse()
    {
        // Arrange
        var lineaParam = Expression.Parameter(typeof(LineaPedido), "l");
        var productoNavigation = CreateNavigationMock("Producto", isCollection: false);

        // For Reference, NavigationExpression is simpler - just a property access or EntityReference
        var productoProperty = Expression.Property(lineaParam, "Producto");

        var productoInclude = new IncludeExpression(
            entityExpression: lineaParam,
            navigationExpression: productoProperty,
            navigation: (INavigationBase)productoNavigation);

        // Act
        var visitor = new IncludeExtractionVisitor(CreateCollectionManagerMock());
        visitor.Visit(productoInclude);

        // Assert
        visitor.DetectedNavigations.Should().HaveCount(1);
        visitor.DetectedNavigations.First().Name.Should().Be("Producto");
        visitor.DetectedNavigations.First().IsCollection.Should().BeFalse();

        visitor.DetectedIncludes.Should().HaveCount(1);
        var productoInfo = visitor.DetectedIncludes.First();
        productoInfo.NavigationName.Should().Be("Producto");
        productoInfo.IsCollection.Should().BeFalse("Reference navigations are not collections");
        productoInfo.HasOperations.Should().BeFalse("Simple Reference has no filters");
    }

    /// <summary>
    /// Tests Reference navigation inside a ComplexType (ValueObject).
    /// Query: .Include(p => p.DireccionEntrega.Vendedor)
    ///
    /// This is a Reference inside a ValueObject. EF Core should still generate
    /// an IncludeExpression with IsCollection = false.
    /// </summary>
    [Fact]
    public void Visit_ReferenceInsideComplexType_ShouldDetectWithIsCollectionFalse()
    {
        // Arrange
        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");
        var vendedorNavigation = CreateNavigationMock("Vendedor", isCollection: false);

        // Simulate the navigation path: Pedido.DireccionEntrega.Vendedor
        // The NavigationExpression would be the property access chain
        var direccionProperty = Expression.Property(pedidoParam, typeof(Pedido).GetProperty("FechaPedido")!); // Placeholder
        var vendedorProperty = Expression.Constant(null, typeof(Vendedor)); // Simplified - real would be nested property

        var vendedorInclude = new IncludeExpression(
            entityExpression: pedidoParam,
            navigationExpression: vendedorProperty,
            navigation: (INavigationBase)vendedorNavigation);

        // Act
        var visitor = new IncludeExtractionVisitor(CreateCollectionManagerMock());
        visitor.Visit(vendedorInclude);

        // Assert
        visitor.DetectedNavigations.Should().HaveCount(1);
        visitor.DetectedNavigations.First().Name.Should().Be("Vendedor");
        visitor.DetectedNavigations.First().IsCollection.Should().BeFalse();

        visitor.DetectedIncludes.Should().HaveCount(1);
        var vendedorInfo = visitor.DetectedIncludes.First();
        vendedorInfo.NavigationName.Should().Be("Vendedor");
        vendedorInfo.IsCollection.Should().BeFalse("Reference inside ComplexType is not a collection");
    }
}
