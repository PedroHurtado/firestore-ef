using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System.Linq.Expressions;
using System.Reflection;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreIncludeTranslator.
///
/// The translator:
/// - Receives: IncludeExpression (from EF Core)
/// - Returns: List&lt;IncludeInfo&gt;
///
/// We use Moq to create IncludeExpression instances since it's a sealed class
/// with internal constructor. We mock the Navigation property which provides
/// NavigationName and IsCollection.
/// </summary>
public class FirestoreIncludeTranslatorTests
{
    private readonly FirestoreIncludeTranslator _translator;

    public FirestoreIncludeTranslatorTests()
    {
        _translator = new FirestoreIncludeTranslator();
    }

    #region Test Entities

    private class Pedido
    {
        public string Id { get; set; } = default!;
        public DateTime Fecha { get; set; }
        public EstadoPedido Estado { get; set; }
        public decimal Total { get; set; }
        public List<LineaPedido> Lineas { get; set; } = new();
    }

    private class LineaPedido
    {
        public string Id { get; set; } = default!;
        public int Cantidad { get; set; }
    }

    private class Categoria
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    private enum EstadoPedido
    {
        Pendiente = 0,
        Confirmado = 1,
        Enviado = 2
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock INavigationBase for testing.
    /// </summary>
    private INavigationBase CreateNavigationMock(string name, bool isCollection)
    {
        var navMock = new Mock<INavigationBase>();
        navMock.Setup(n => n.Name).Returns(name);
        navMock.Setup(n => n.IsCollection).Returns(isCollection);
        return navMock.Object;
    }

    /// <summary>
    /// Creates an IncludeExpression using the public constructor.
    /// Constructor: IncludeExpression(Expression entityExpression, Expression navigationExpression, INavigationBase navigation)
    /// </summary>
    private static IncludeExpression CreateIncludeExpression(
        Expression entityExpression,
        Expression navigationExpression,
        INavigationBase navigation)
    {
        return new IncludeExpression(entityExpression, navigationExpression, navigation);
    }

    /// <summary>
    /// Creates a simple parameter expression for entity.
    /// </summary>
    private ParameterExpression CreateEntityParameter<T>() => Expression.Parameter(typeof(T), "e");

    /// <summary>
    /// Creates a member access expression for a property.
    /// </summary>
    private MemberExpression CreatePropertyAccess<T>(ParameterExpression param, string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        if (property == null)
            throw new ArgumentException($"Property {propertyName} not found on {typeof(T).Name}");
        return Expression.MakeMemberAccess(param, property);
    }

    /// <summary>
    /// Creates a Where method call expression.
    /// </summary>
    private MethodCallExpression CreateWhereCall<T>(Expression source, Expression<Func<T, bool>> predicate)
    {
        var whereMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        return Expression.Call(whereMethod, source, predicate);
    }

    /// <summary>
    /// Creates an OrderBy method call expression.
    /// </summary>
    private MethodCallExpression CreateOrderByCall<T, TKey>(Expression source, Expression<Func<T, TKey>> keySelector, bool descending = false)
    {
        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var orderByMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), typeof(TKey));

        return Expression.Call(orderByMethod, source, keySelector);
    }

    /// <summary>
    /// Creates a Take method call expression.
    /// </summary>
    private MethodCallExpression CreateTakeCall<T>(Expression source, int count)
    {
        var takeMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Take" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        return Expression.Call(takeMethod, source, Expression.Constant(count));
    }

    /// <summary>
    /// Creates a Skip method call expression.
    /// </summary>
    private MethodCallExpression CreateSkipCall<T>(Expression source, int count)
    {
        var skipMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Skip" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T));

        return Expression.Call(skipMethod, source, Expression.Constant(count));
    }

    #endregion

    #region Simple Include - Collection

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos)
    /// Verifica que un Include simple de colección extrae NavigationName e IsCollection.
    /// </summary>
    [Fact]
    public void Translate_SimpleCollectionInclude_ReturnsIncludeInfoWithNavigationName()
    {
        // LINQ: .Include(c => c.Pedidos)
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();
        var navExpression = Expression.Constant(new List<Pedido>());

        var includeExpression = CreateIncludeExpression(
            entityParam,
            navExpression,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].NavigationName.Should().Be("Pedidos");
        result[0].IsCollection.Should().BeTrue();
        result[0].HasOperations.Should().BeFalse();
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.CategoriaFavorita)
    /// Verifica que un Include de referencia (no colección) extrae IsCollection = false.
    /// </summary>
    [Fact]
    public void Translate_SimpleReferenceInclude_ReturnsIncludeInfoForReference()
    {
        // LINQ: .Include(c => c.CategoriaFavorita)
        var navigation = CreateNavigationMock("CategoriaFavorita", isCollection: false);
        var entityParam = CreateEntityParameter<object>();
        var navExpression = Expression.Constant(null, typeof(Categoria));

        var includeExpression = CreateIncludeExpression(
            entityParam,
            navExpression,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].NavigationName.Should().Be("CategoriaFavorita");
        result[0].IsCollection.Should().BeFalse();
        result[0].HasOperations.Should().BeFalse();
    }

    #endregion

    #region ThenInclude (Nested Includes)

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos).ThenInclude(p =&gt; p.Lineas)
    /// Verifica que ThenInclude anidado genera múltiples IncludeInfo.
    /// </summary>
    [Fact]
    public void Translate_ThenInclude_ReturnsMultipleIncludeInfo()
    {
        // LINQ: .Include(c => c.Pedidos).ThenInclude(p => p.Lineas)
        var lineasNavigation = CreateNavigationMock("Lineas", isCollection: true);
        var innerEntityParam = CreateEntityParameter<Pedido>();
        var lineasExpression = Expression.Constant(new List<LineaPedido>());

        var innerInclude = CreateIncludeExpression(
            innerEntityParam,
            lineasExpression,
            lineasNavigation);

        var pedidosNavigation = CreateNavigationMock("Pedidos", isCollection: true);
        var pedidosExpression = Expression.Constant(new List<Pedido>());

        var outerInclude = CreateIncludeExpression(
            innerInclude,
            pedidosExpression,
            pedidosNavigation);

        var result = _translator.Translate(outerInclude);

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.NavigationName == "Pedidos");
        result.Should().Contain(i => i.NavigationName == "Lineas");
    }

    #endregion

    #region Filtered Include - Where

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Where(p =&gt; p.Estado == EstadoPedido.Confirmado))
    /// Verifica que un Where simple extrae el filtro con PropertyName y Operator.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithWhere_ExtractsFilters()
    {
        // LINQ: .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");
        var estadoProperty = Expression.Property(pedidoParam, "Estado");
        var confirmado = Expression.Constant(EstadoPedido.Confirmado);
        var comparison = Expression.Equal(estadoProperty, confirmado);
        var predicate = Expression.Lambda<Func<Pedido, bool>>(comparison, pedidoParam);

        var sourceList = Expression.Constant(new List<Pedido>());
        var whereCall = CreateWhereCall<Pedido>(sourceList, predicate);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            whereCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].NavigationName.Should().Be("Pedidos");
        result[0].Filters.Should().HaveCount(1);
        result[0].Filters[0].PropertyName.Should().Be("Estado");
        result[0].Filters[0].Operator.Should().Be(FirestoreOperator.EqualTo);
        result[0].HasOperations.Should().BeTrue();
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Where(p =&gt; p.Total &gt; 100 &amp;&amp; p.Estado == EstadoPedido.Confirmado))
    /// Verifica que condiciones AND extraen múltiples filtros.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithMultipleConditions_ExtractsAllFilters()
    {
        // LINQ: .Include(c => c.Pedidos.Where(p => p.Total > 100 && p.Estado == EstadoPedido.Confirmado))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");

        var totalProperty = Expression.Property(pedidoParam, "Total");
        var totalComparison = Expression.GreaterThan(totalProperty, Expression.Constant(100m));

        var estadoProperty = Expression.Property(pedidoParam, "Estado");
        var estadoComparison = Expression.Equal(estadoProperty, Expression.Constant(EstadoPedido.Confirmado));

        var andExpression = Expression.AndAlso(totalComparison, estadoComparison);
        var predicate = Expression.Lambda<Func<Pedido, bool>>(andExpression, pedidoParam);

        var sourceList = Expression.Constant(new List<Pedido>());
        var whereCall = CreateWhereCall<Pedido>(sourceList, predicate);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            whereCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].Filters.Should().HaveCount(2);
        result[0].Filters.Should().Contain(f => f.PropertyName == "Total");
        result[0].Filters.Should().Contain(f => f.PropertyName == "Estado");
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Where(p =&gt; p.Estado == Confirmado || p.Estado == Enviado))
    /// Verifica que condiciones OR extraen un OrFilterGroup.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithOrCondition_ExtractsOrFilterGroup()
    {
        // LINQ: .Include(c => c.Pedidos.Where(p => p.Estado == Confirmado || p.Estado == Enviado))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");
        var estadoProperty = Expression.Property(pedidoParam, "Estado");

        var confirmadoComparison = Expression.Equal(estadoProperty, Expression.Constant(EstadoPedido.Confirmado));
        var enviadoComparison = Expression.Equal(estadoProperty, Expression.Constant(EstadoPedido.Enviado));

        var orExpression = Expression.OrElse(confirmadoComparison, enviadoComparison);
        var predicate = Expression.Lambda<Func<Pedido, bool>>(orExpression, pedidoParam);

        var sourceList = Expression.Constant(new List<Pedido>());
        var whereCall = CreateWhereCall<Pedido>(sourceList, predicate);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            whereCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].OrFilterGroups.Should().HaveCount(1);
        result[0].OrFilterGroups[0].Clauses.Should().HaveCount(2);
    }

    #endregion

    #region Filtered Include - OrderBy

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.OrderBy(p =&gt; p.Fecha))
    /// Verifica que OrderBy extrae PropertyName y Descending = false.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithOrderBy_ExtractsOrderByClause()
    {
        // LINQ: .Include(c => c.Pedidos.OrderBy(p => p.Fecha))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var sourceList = Expression.Constant(new List<Pedido>());
        var orderByCall = CreateOrderByCall<Pedido, DateTime>(sourceList, p => p.Fecha);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            orderByCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].OrderByClauses.Should().HaveCount(1);
        result[0].OrderByClauses[0].PropertyName.Should().Be("Fecha");
        result[0].OrderByClauses[0].Descending.Should().BeFalse();
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.OrderByDescending(p =&gt; p.Fecha))
    /// Verifica que OrderByDescending extrae Descending = true.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithOrderByDescending_ExtractsDescendingClause()
    {
        // LINQ: .Include(c => c.Pedidos.OrderByDescending(p => p.Fecha))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var sourceList = Expression.Constant(new List<Pedido>());
        var orderByCall = CreateOrderByCall<Pedido, DateTime>(sourceList, p => p.Fecha, descending: true);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            orderByCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].OrderByClauses.Should().HaveCount(1);
        result[0].OrderByClauses[0].Descending.Should().BeTrue();
    }

    #endregion

    #region Filtered Include - Take/Skip

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Take(5))
    /// Verifica que Take extrae el valor límite.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithTake_ExtractsTakeValue()
    {
        // LINQ: .Include(c => c.Pedidos.Take(5))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var sourceList = Expression.Constant(new List<Pedido>());
        var takeCall = CreateTakeCall<Pedido>(sourceList, 5);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            takeCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].Take.Should().Be(5);
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Skip(2))
    /// Verifica que Skip extrae el valor de offset.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithSkip_ExtractsSkipValue()
    {
        // LINQ: .Include(c => c.Pedidos.Skip(2))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var sourceList = Expression.Constant(new List<Pedido>());
        var skipCall = CreateSkipCall<Pedido>(sourceList, 2);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            skipCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].Skip.Should().Be(2);
    }

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Skip(2).Take(5))
    /// Verifica que Skip y Take combinados extraen ambos valores.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithSkipAndTake_ExtractsBothValues()
    {
        // LINQ: .Include(c => c.Pedidos.Skip(2).Take(5))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var sourceList = Expression.Constant(new List<Pedido>());
        var skipCall = CreateSkipCall<Pedido>(sourceList, 2);
        var takeCall = CreateTakeCall<Pedido>(skipCall, 5);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            takeCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].Skip.Should().Be(2);
        result[0].Take.Should().Be(5);
    }

    #endregion

    #region Combined Operations

    /// <summary>
    /// LINQ: .Include(c =&gt; c.Pedidos.Where(p =&gt; p.Total &gt; 100).OrderByDescending(p =&gt; p.Fecha).Skip(1).Take(5))
    /// Verifica que todas las operaciones combinadas se extraen correctamente.
    /// </summary>
    [Fact]
    public void Translate_FilteredIncludeWithAllOperations_ExtractsAll()
    {
        // LINQ: .Include(c => c.Pedidos.Where(p => p.Total > 100).OrderByDescending(p => p.Fecha).Skip(1).Take(5))
        var navigation = CreateNavigationMock("Pedidos", isCollection: true);
        var entityParam = CreateEntityParameter<object>();

        var pedidoParam = Expression.Parameter(typeof(Pedido), "p");
        var totalProperty = Expression.Property(pedidoParam, "Total");
        var comparison = Expression.GreaterThan(totalProperty, Expression.Constant(100m));
        var predicate = Expression.Lambda<Func<Pedido, bool>>(comparison, pedidoParam);

        var sourceList = Expression.Constant(new List<Pedido>());
        var whereCall = CreateWhereCall<Pedido>(sourceList, predicate);
        var orderByCall = CreateOrderByCall<Pedido, DateTime>(whereCall, p => p.Fecha, descending: true);
        var skipCall = CreateSkipCall<Pedido>(orderByCall, 1);
        var takeCall = CreateTakeCall<Pedido>(skipCall, 5);

        var includeExpression = CreateIncludeExpression(
            entityParam,
            takeCall,
            navigation);

        var result = _translator.Translate(includeExpression);

        result.Should().HaveCount(1);
        result[0].NavigationName.Should().Be("Pedidos");
        result[0].Filters.Should().HaveCount(1);
        result[0].OrderByClauses.Should().HaveCount(1);
        result[0].Skip.Should().Be(1);
        result[0].Take.Should().Be(5);
        result[0].HasOperations.Should().BeTrue();
    }

    #endregion
}
