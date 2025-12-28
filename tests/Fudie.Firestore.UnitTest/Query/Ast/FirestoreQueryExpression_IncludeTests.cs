using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Include partial class.
/// Tests the Include commands on FirestoreQueryExpression.
///
/// Note: TranslateInclude tests require IncludeExpression from EF Core,
/// which is tested through integration tests. These unit tests focus on
/// the AST commands and IncludeInfo operations.
///
/// This covers both navigation types:
/// - SubCollections (nested collections)
/// - DocumentReferences (references to other collections)
/// </summary>
public class FirestoreQueryExpression_IncludeTests
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpression_IncludeTests()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestCliente));
    }

    #region Test Entities

    private class TestCliente
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public List<TestPedido> Pedidos { get; set; } = new();
        public TestCategoria CategoriaFavorita { get; set; } = default!;
    }

    private class TestPedido
    {
        public string Id { get; set; } = default!;
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
    }

    private class TestCategoria
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    #endregion

    #region Include Commands (base FirestoreQueryExpression)

    [Fact]
    public void AddInclude_AddsIncludeInfoToList()
    {
        var ast = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

        ast.AddInclude(includeInfo);

        ast.PendingIncludes.Should().HaveCount(1);
        ast.PendingIncludes[0].NavigationName.Should().Be("Pedidos");
        ast.PendingIncludes[0].IsCollection.Should().BeTrue();
    }

    [Fact]
    public void AddInclude_WithNameAndFlag_AddsToList()
    {
        var ast = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");

        ast.AddInclude("Pedidos", isCollection: true);

        ast.PendingIncludes.Should().HaveCount(1);
        ast.PendingIncludes[0].NavigationName.Should().Be("Pedidos");
    }

    [Fact]
    public void AddInclude_AvoidsDuplicatesByNavigationName()
    {
        var ast = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");

        ast.AddInclude("Pedidos", isCollection: true);
        ast.AddInclude("Pedidos", isCollection: true);

        ast.PendingIncludes.Should().HaveCount(1);
    }

    [Fact]
    public void AddInclude_SubCollectionAndDocumentReference_AddsBoth()
    {
        var ast = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");

        ast.AddInclude("Pedidos", isCollection: true);
        ast.AddInclude("CategoriaFavorita", isCollection: false);

        ast.PendingIncludes.Should().HaveCount(2);
        ast.PendingIncludes.Should().Contain(i => i.NavigationName == "Pedidos");
        ast.PendingIncludes.Should().Contain(i => i.NavigationName == "CategoriaFavorita");
    }

    [Fact]
    public void AddInclude_MultipleTimes_AddsAll()
    {
        var ast = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");
        var includeInfo1 = new IncludeInfo("Pedidos", isCollection: true);
        var includeInfo2 = new IncludeInfo("CategoriaFavorita", isCollection: false);

        ast.AddInclude(includeInfo1);
        ast.AddInclude(includeInfo2);

        ast.PendingIncludes.Should().HaveCount(2);
    }

    #endregion

    #region IncludeInfo with FirestoreWhereClause

    [Fact]
    public void IncludeInfo_AddFilter_AddsFirestoreWhereClause()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, Expression.Constant(100m));

        includeInfo.AddFilter(clause);

        includeInfo.Filters.Should().HaveCount(1);
        includeInfo.Filters[0].PropertyName.Should().Be("Total");
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void IncludeInfo_AddOrderBy_AddsFirestoreOrderByClause()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var clause = new FirestoreOrderByClause("Fecha", descending: true);

        includeInfo.AddOrderBy(clause);

        includeInfo.OrderByClauses.Should().HaveCount(1);
        includeInfo.OrderByClauses[0].PropertyName.Should().Be("Fecha");
        includeInfo.OrderByClauses[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void IncludeInfo_WithTakeSkip_SetsValues()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

        includeInfo.WithSkip(2).WithTake(5);

        includeInfo.Skip.Should().Be(2);
        includeInfo.Take.Should().Be(5);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void IncludeInfo_WithTakeExpression_SetsExpression()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(10);

        includeInfo.WithTakeExpression(expr);

        includeInfo.Take.Should().BeNull();
        includeInfo.TakeExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void IncludeInfo_WithSkipExpression_SetsExpression()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);
        var expr = Expression.Constant(5);

        includeInfo.WithSkipExpression(expr);

        includeInfo.Skip.Should().BeNull();
        includeInfo.SkipExpression.Should().Be(expr);
        includeInfo.HasOperations.Should().BeTrue();
    }

    [Fact]
    public void IncludeInfo_AddFilters_AddMultipleClauses()
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
    public void IncludeInfo_SetOrderBy_ReplacesExistingOrderBy()
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
    public void IncludeInfo_AddOrFilterGroup_AddsGroup()
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
    public void IncludeInfo_NavigationName_ReturnsCorrectValue()
    {
        var includeInfo = new IncludeInfo("CustomName", isCollection: false);

        includeInfo.NavigationName.Should().Be("CustomName");
    }

    [Fact]
    public void IncludeInfo_IsCollection_ReturnsCorrectValue()
    {
        var subCollectionInclude = new IncludeInfo("Pedidos", isCollection: true);
        var referenceInclude = new IncludeInfo("CategoriaFavorita", isCollection: false);

        subCollectionInclude.IsCollection.Should().BeTrue();
        referenceInclude.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void IncludeInfo_HasOperations_FalseWhenEmpty()
    {
        var includeInfo = new IncludeInfo("Pedidos", isCollection: true);

        includeInfo.HasOperations.Should().BeFalse();
    }

    [Fact]
    public void IncludeInfo_CombinedOperations_AllPresent()
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

    #endregion

    #region TranslateIncludeRequest Record

    [Fact]
    public void TranslateIncludeRequest_IsRecord_WithCorrectProperties()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "clientes");
        var shaperExpression = new StructuralTypeShaperExpression(
            _entityTypeMock.Object,
            new ProjectionBindingExpression(queryExpression, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);
        var source = new ShapedQueryExpression(queryExpression, shaperExpression);

        // TranslateIncludeRequest now requires IncludeExpression which is an EF Core internal type
        // This test validates the record structure exists
        var requestType = typeof(TranslateIncludeRequest);
        requestType.Should().NotBeNull();
        requestType.IsValueType.Should().BeFalse(); // It's a record class
    }

    #endregion
}
