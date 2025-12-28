using Firestore.EntityFrameworkCore.Query.Visitors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System.Linq.Expressions;

namespace Fudie.Firestore.UnitTest.Query.Visitors;

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

    private static IReadOnlyNavigation CreateNavigationMock(string name, bool isCollection)
    {
        var navMock = new Mock<IReadOnlyNavigation>();
        navMock.Setup(n => n.Name).Returns(name);
        navMock.Setup(n => n.IsCollection).Returns(isCollection);
        navMock.As<INavigationBase>().Setup(n => n.Name).Returns(name);
        navMock.As<INavigationBase>().Setup(n => n.IsCollection).Returns(isCollection);
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
        var visitor = new IncludeExtractionVisitor();
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
}
