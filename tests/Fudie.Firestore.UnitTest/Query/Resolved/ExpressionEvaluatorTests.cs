using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;
using Moq;
using System.Linq.Expressions;

namespace Fudie.Firestore.UnitTest.Query.Resolved;

/// <summary>
/// Tests for ExpressionEvaluator implementation.
/// Verifies expression evaluation with parameter substitution.
/// </summary>
public class ExpressionEvaluatorTests
{
    private readonly IExpressionEvaluator _evaluator = new ExpressionEvaluator();

    #region EvaluateWhereClause Tests

    [Fact]
    public void EvaluateWhereClause_ConstantExpression_ReturnsValue()
    {
        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("John"));
        var queryContext = CreateQueryContext();

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().Be("John");
    }

    [Fact]
    public void EvaluateWhereClause_ParameterExpression_ResolvesFromContext()
    {
        var paramExpr = Expression.Parameter(typeof(string), "__name_0");
        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, paramExpr);
        var queryContext = CreateQueryContext(new Dictionary<string, object?> { { "__name_0", "Jane" } });

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().Be("Jane");
    }

    [Fact]
    public void EvaluateWhereClause_StartsWithUpperBound_ComputesUpperBound()
    {
        var prefixExpr = Expression.Constant("abc");
        var upperBoundExpr = new StartsWithUpperBoundExpression(prefixExpr);
        var clause = new FirestoreWhereClause("Name", FirestoreOperator.LessThan, upperBoundExpr);
        var queryContext = CreateQueryContext();

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().Be("abc\uffff");
    }

    [Fact]
    public void EvaluateWhereClause_NullConstant_ReturnsNull()
    {
        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant(null, typeof(string)));
        var queryContext = CreateQueryContext();

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().BeNull();
    }

    [Fact]
    public void EvaluateWhereClause_IntegerConstant_ReturnsValue()
    {
        var clause = new FirestoreWhereClause("Age", FirestoreOperator.GreaterThan, Expression.Constant(25));
        var queryContext = CreateQueryContext();

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().Be(25);
    }

    #endregion

    #region Evaluate<T> Tests

    [Fact]
    public void Evaluate_ConstantInt_ReturnsValue()
    {
        var expression = Expression.Constant(10);
        var queryContext = CreateQueryContext();

        var result = _evaluator.Evaluate<int>(expression, queryContext);

        result.Should().Be(10);
    }

    [Fact]
    public void Evaluate_ParameterInt_ResolvesFromContext()
    {
        var paramExpr = Expression.Parameter(typeof(int), "__limit_0");
        var queryContext = CreateQueryContext(new Dictionary<string, object?> { { "__limit_0", 50 } });

        var result = _evaluator.Evaluate<int>(paramExpr, queryContext);

        result.Should().Be(50);
    }

    [Fact]
    public void Evaluate_ConstantString_ReturnsValue()
    {
        var expression = Expression.Constant("test");
        var queryContext = CreateQueryContext();

        var result = _evaluator.Evaluate<string>(expression, queryContext);

        result.Should().Be("test");
    }

    #endregion

    #region EvaluateIdExpression Tests

    // Note: EvaluateIdExpression requires a real QueryContext (AsQueryContext property)
    // which cannot be easily mocked. These scenarios are tested through FirestoreAstResolverTests
    // integration tests instead.

    #endregion

    #region Closure Capture Tests

    [Fact]
    public void EvaluateWhereClause_ClosureCapture_ResolvesValue()
    {
        // Simulate a closure capture: accessing a field on a constant object
        var capturedValue = "captured";
        var closureObject = new { Value = capturedValue };
        var constantExpr = Expression.Constant(closureObject);
        var memberExpr = Expression.Property(constantExpr, "Value");

        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, memberExpr);
        var queryContext = CreateQueryContext();

        var result = _evaluator.EvaluateWhereClause(clause, queryContext);

        result.Should().Be("captured");
    }

    #endregion

    #region Helper Methods

    private static IFirestoreQueryContext CreateQueryContext(Dictionary<string, object?>? parameters = null)
    {
        var mockContext = new Mock<IFirestoreQueryContext>();
        mockContext.Setup(x => x.ParameterValues)
            .Returns(parameters ?? new Dictionary<string, object?>());

        return mockContext.Object;
    }

    #endregion
}
