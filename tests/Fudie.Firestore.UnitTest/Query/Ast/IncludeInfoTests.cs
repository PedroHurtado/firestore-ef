using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for IncludeInfo class.
/// </summary>
public class IncludeInfoTests
{
    [Fact]
    public void AddFilter_AddsFirestoreWhereClause()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, Expression.Constant(100m));

        includeInfo.AddFilter(clause);

        includeInfo.Filters.Should().HaveCount(1);
        includeInfo.Filters[0].PropertyName.Should().Be("Total");
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void AddOrderBy_AddsFirestoreOrderByClause()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreOrderByClause("Fecha", descending: true);

        includeInfo.AddOrderBy(clause);

        includeInfo.OrderByClauses.Should().HaveCount(1);
        includeInfo.OrderByClauses[0].PropertyName.Should().Be("Fecha");
        includeInfo.OrderByClauses[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void WithTakeSkip_SetsValues()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

        includeInfo.WithSkip(2).WithTake(5);

        includeInfo.Skip.Should().Be(2);
        includeInfo.Take.Should().Be(5);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void WithTakeExpression_SetsExpression()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(10);

        includeInfo.WithTakeExpression(expr);

        includeInfo.Take.Should().BeNull();
        includeInfo.TakeExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void WithSkipExpression_SetsExpression()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(5);

        includeInfo.WithSkipExpression(expr);

        includeInfo.Skip.Should().BeNull();
        includeInfo.SkipExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void AddFilters_AddMultipleClauses()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
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
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
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
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
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
        var includeInfo = new IncludeInfo("CustomName", isCollection: false);

        includeInfo.NavigationName.Should().Be("CustomName");
    }

    [Fact]
    public void IsCollection_ReturnsCorrectValue()
    {
        var subCollectionInclude = new IncludeInfo("Pedidos", isCollection: true);
        var referenceInclude = new IncludeInfo("CategoriaFavorita", isCollection: false);

        subCollectionInclude.IsCollection.Should().BeTrue();
        referenceInclude.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void HasOperations_FalseWhenEmpty()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

        includeInfo.HasOperations.Should().BeFalse();
    }

    [Fact]
    public void CombinedOperations_AllPresent()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

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
}
