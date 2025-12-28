using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System.Linq.Expressions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreIncludeTranslator.
/// The translator delegates to IncludeExtractionVisitor for all the work.
/// </summary>
public class FirestoreIncludeTranslatorTests
{
    /// <summary>
    /// Tests that FirestoreIncludeTranslator delegates to the injected visitor
    /// and returns the visitor's DetectedIncludes.
    /// </summary>
    [Fact]
    public void Translate_DelegatesToVisitor_ReturnsDetectedIncludes()
    {
        // Arrange - Create a mock visitor with pre-populated includes
        var mockVisitor = new TestableIncludeExtractionVisitor();
        mockVisitor.DetectedIncludes.Add(new IncludeInfo("Pedidos", isCollection: true));
        mockVisitor.DetectedIncludes.Add(new IncludeInfo("Lineas", isCollection: true));

        var translator = new FirestoreIncludeTranslator(mockVisitor);

        // Create a minimal IncludeExpression
        var navMock = new Mock<INavigationBase>();
        navMock.Setup(n => n.Name).Returns("Dummy");
        var includeExpr = new IncludeExpression(
            Expression.Parameter(typeof(object), "e"),
            Expression.Constant(null),
            navMock.Object);

        // Act
        var result = translator.Translate(includeExpr);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.NavigationName == "Pedidos" && i.IsCollection);
        result.Should().Contain(i => i.NavigationName == "Lineas" && i.IsCollection);
        mockVisitor.VisitWasCalled.Should().BeTrue();
    }

    /// <summary>
    /// Testable visitor that tracks if Visit was called.
    /// </summary>
    private class TestableIncludeExtractionVisitor : IncludeExtractionVisitor
    {
        public bool VisitWasCalled { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            VisitWasCalled = true;
            return node;
        }
    }
}
