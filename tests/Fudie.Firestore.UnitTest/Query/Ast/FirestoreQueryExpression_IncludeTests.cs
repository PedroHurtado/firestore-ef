using Firestore.EntityFrameworkCore.Query.Ast;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;

namespace Fudie.Firestore.UnitTest.Query.Ast;

/// <summary>
/// Tests for FirestoreQueryExpression_Include partial class.
/// Tests the Include commands on FirestoreQueryExpression.
///
/// Note: TranslateInclude tests require IncludeExpression from EF Core,
/// which is tested through integration tests. These unit tests focus on
/// the AST commands.
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
}
