using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for IncludeInfo class.
/// </summary>
public class IncludeInfoTests
{
    #region Test Entities

    private class Pedido { public string Id { get; set; } = ""; }
    private class Categoria { public string Id { get; set; } = ""; }

    #endregion

    #region Helper Methods

    private static IncludeInfo CreateIncludeInfo(string navigationName, bool isCollection, string? collectionName = null, Type? targetType = null)
    {
        return new IncludeInfo(
            navigationName,
            isCollection,
            collectionName ?? navigationName.ToLower(),
            targetType ?? typeof(Pedido));
    }

    #endregion

    [Fact]
    public void AddFilter_AddsFirestoreWhereClause()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, Expression.Constant(100m));

        includeInfo.AddFilter(clause);

        includeInfo.Filters.Should().HaveCount(1);
        includeInfo.Filters[0].PropertyName.Should().Be("Total");
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void AddOrderBy_AddsFirestoreOrderByClause()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreOrderByClause("Fecha", descending: true);

        includeInfo.AddOrderBy(clause);

        includeInfo.OrderByClauses.Should().HaveCount(1);
        includeInfo.OrderByClauses[0].PropertyName.Should().Be("Fecha");
        includeInfo.OrderByClauses[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void WithTakeSkip_SetsValues()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);

        includeInfo.WithSkip(2).WithTake(5);

        includeInfo.Skip.Should().Be(2);
        includeInfo.Take.Should().Be(5);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void WithTakeExpression_SetsExpression()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(10);

        includeInfo.WithTakeExpression(expr);

        includeInfo.Take.Should().BeNull();
        includeInfo.TakeExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void WithSkipExpression_SetsExpression()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(5);

        includeInfo.WithSkipExpression(expr);

        includeInfo.Skip.Should().BeNull();
        includeInfo.SkipExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void AddFilters_AddMultipleClauses()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var clauses = new[]
        {
            new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, Expression.Constant(100m)),
            new FirestoreWhereClause("Fecha", FirestoreOperator.GreaterThanOrEqualTo, Expression.Constant(DateTime.Now))
        };

        includeInfo.AddFilters(clauses);

        includeInfo.Filters.Should().HaveCount(2);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void SetOrderBy_ReplacesExistingOrderBy()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var clause1 = new FirestoreOrderByClause("Total", descending: false);
        var clause2 = new FirestoreOrderByClause("Fecha", descending: true);

        includeInfo.AddOrderBy(clause1);
        includeInfo.SetOrderBy(clause2);

        includeInfo.OrderByClauses.Should().HaveCount(1);
        includeInfo.OrderByClauses[0].PropertyName.Should().Be("Fecha");
    }

    [Fact]
    public void AddOrFilterGroup_AddsGroup()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);
        var orGroup = new FirestoreOrFilterGroup(new[]
        {
            new FirestoreWhereClause("Estado", FirestoreOperator.EqualTo, Expression.Constant(1)),
            new FirestoreWhereClause("Estado", FirestoreOperator.EqualTo, Expression.Constant(2))
        });

        includeInfo.AddOrFilterGroup(orGroup);

        includeInfo.OrFilterGroups.Should().HaveCount(1);
        includeInfo.OrFilterGroups[0].Clauses.Should().HaveCount(2);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void NavigationName_ReturnsCorrectValue()
    {
        var includeInfo = CreateIncludeInfo("CustomName", isCollection: false, targetType: typeof(Categoria));

        includeInfo.NavigationName.Should().Be("CustomName");
    }

    [Fact]
    public void IsCollection_ReturnsCorrectValue()
    {
        var subCollectionInclude = CreateIncludeInfo("Pedidos", isCollection: true);
        var referenceInclude = CreateIncludeInfo("CategoriaFavorita", isCollection: false, targetType: typeof(Categoria));

        subCollectionInclude.IsCollection.Should().BeTrue();
        referenceInclude.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void HasOperations_FalseWhenEmpty()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);

        includeInfo.HasOperations.Should().BeFalse();
    }

    [Fact]
    public void CombinedOperations_AllPresent()
    {
        var includeInfo = CreateIncludeInfo("Pedidos", isCollection: true);

        includeInfo
            .AddFilter(new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, Expression.Constant(100m)))
            .AddOrderBy(new FirestoreOrderByClause("Fecha", descending: true))
            .WithSkip(1)
            .WithTake(5);

        includeInfo.Filters.Should().HaveCount(1);
        includeInfo.OrderByClauses.Should().HaveCount(1);
        includeInfo.Skip.Should().Be(1);
        includeInfo.Take.Should().Be(5);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void CollectionName_ReturnsCorrectValue()
    {
        var includeInfo = new IncludeInfo("Pedidos", true, "orders", typeof(Pedido));

        includeInfo.CollectionName.Should().Be("orders");
    }

    [Fact]
    public void TargetClrType_ReturnsCorrectValue()
    {
        var includeInfo = new IncludeInfo("Pedidos", true, "orders", typeof(Pedido));

        includeInfo.TargetClrType.Should().Be(typeof(Pedido));
    }
}
