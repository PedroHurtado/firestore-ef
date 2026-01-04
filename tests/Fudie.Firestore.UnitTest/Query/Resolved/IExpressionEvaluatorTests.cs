using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Resolved;

/// <summary>
/// Tests for the IExpressionEvaluator interface contract.
/// Documents the expected behavior that any implementation must provide.
/// </summary>
public class IExpressionEvaluatorTests
{
    #region Interface Contract Tests

    [Fact]
    public void IExpressionEvaluator_Should_Have_EvaluateWhereClause_Method()
    {
        // Documents that EvaluateWhereClause evaluates where clause value expressions
        // Handles ConstantExpression, StartsWithUpperBoundExpression, and parameterized expressions
        var method = typeof(IExpressionEvaluator).GetMethod("EvaluateWhereClause");

        method.Should().NotBeNull("IExpressionEvaluator must have EvaluateWhereClause method");
        method!.ReturnType.Should().Be(typeof(object), "EvaluateWhereClause returns object");
        method.GetParameters().Should().HaveCount(2, "EvaluateWhereClause takes clause and queryContext");
    }

    [Fact]
    public void IExpressionEvaluator_Should_Have_Evaluate_Generic_Method()
    {
        // Documents that Evaluate<T> evaluates expressions to a specific type
        // Used for pagination expressions (limit, skip, etc.)
        var method = typeof(IExpressionEvaluator).GetMethod("Evaluate");

        method.Should().NotBeNull("IExpressionEvaluator must have Evaluate<T> method");
        method!.IsGenericMethod.Should().BeTrue("Evaluate should be generic");
        method.GetParameters().Should().HaveCount(2, "Evaluate takes expression and queryContext");
    }

    [Fact]
    public void IExpressionEvaluator_Should_Have_EvaluateIdExpression_Method()
    {
        // Documents that EvaluateIdExpression evaluates ID value expressions from FindAsync
        // Uses the underlying EF Core QueryContext for complex expressions
        var method = typeof(IExpressionEvaluator).GetMethod("EvaluateIdExpression");

        method.Should().NotBeNull("IExpressionEvaluator must have EvaluateIdExpression method");
        method!.ReturnType.Should().Be(typeof(string), "EvaluateIdExpression returns string");
        method.GetParameters().Should().HaveCount(2, "EvaluateIdExpression takes expression and queryContext");
    }

    [Fact]
    public void IExpressionEvaluator_Should_Have_Three_Methods()
    {
        // Documents that IExpressionEvaluator has exactly 3 methods for expression evaluation
        typeof(IExpressionEvaluator).GetMethods()
            .Should().HaveCount(3, "IExpressionEvaluator has EvaluateWhereClause, Evaluate<T>, and EvaluateIdExpression methods");
    }

    #endregion
}
