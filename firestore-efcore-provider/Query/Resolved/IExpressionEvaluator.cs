using Firestore.EntityFrameworkCore.Query.Ast;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Resolved;

/// <summary>
/// Evaluates LINQ expressions using the query context's parameter values.
/// Handles EF Core's parameterized expressions and closure captures.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates a where clause's value expression.
    /// Handles ConstantExpression, StartsWithUpperBoundExpression, and parameterized expressions.
    /// </summary>
    object? EvaluateWhereClause(FirestoreWhereClause clause, IFirestoreQueryContext queryContext);

    /// <summary>
    /// Evaluates an expression to the specified type.
    /// Used for pagination expressions (limit, skip, etc.)
    /// </summary>
    T? Evaluate<T>(Expression expression, IFirestoreQueryContext queryContext);

    /// <summary>
    /// Evaluates an ID value expression from FindAsync.
    /// Uses the underlying EF Core QueryContext for complex expressions.
    /// </summary>
    string? EvaluateIdExpression(Expression expression, IFirestoreQueryContext queryContext);
}
