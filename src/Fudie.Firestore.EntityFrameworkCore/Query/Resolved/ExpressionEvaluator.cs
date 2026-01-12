using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Resolved;

/// <summary>
/// Evaluates LINQ expressions using the query context's parameter values.
/// Handles EF Core's parameterized expressions and closure captures.
/// </summary>
public class ExpressionEvaluator : IExpressionEvaluator
{
    /// <inheritdoc />
    public object? EvaluateWhereClause(FirestoreWhereClause clause, IFirestoreQueryContext queryContext)
    {
        var expression = clause.ValueExpression;

        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        // Handle StartsWithUpperBoundExpression - compute prefix + \uffff
        if (expression is StartsWithUpperBoundExpression startsWithUpperBound)
        {
            var prefixValue = Evaluate<string>(startsWithUpperBound.PrefixExpression, queryContext);
            return StartsWithUpperBoundExpression.ComputeUpperBound(prefixValue ?? "");
        }

        return CompileAndExecute<object>(expression, queryContext);
    }

    /// <inheritdoc />
    public T? Evaluate<T>(Expression expression, IFirestoreQueryContext queryContext)
    {
        if (expression is ConstantExpression constant)
        {
            return (T?)constant.Value;
        }

        return CompileAndExecute<T>(expression, queryContext);
    }

    /// <inheritdoc />
    public string? EvaluateIdExpression(Expression expression, IFirestoreQueryContext queryContext)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value?.ToString();
        }

        // For ID expressions, we need to handle EF Core's QueryContext parameter references
        // which access queryContext.ParameterValues[...] directly
        var result = CompileAndExecuteWithQueryContext(expression, queryContext.AsQueryContext);
        return result?.ToString();
    }

    /// <summary>
    /// Compiles and executes an expression using IFirestoreQueryContext parameter values.
    /// </summary>
    private static T? CompileAndExecute<T>(Expression expression, IFirestoreQueryContext queryContext)
    {
        try
        {
            var replacer = new ParameterReplacerVisitor(queryContext.ParameterValues);
            var replacedExpression = replacer.Visit(expression);

            var lambda = Expression.Lambda<Func<T>>(
                Expression.Convert(replacedExpression, typeof(T)));

            var compiled = lambda.Compile();
            return compiled();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Compiles and executes an expression that references the EF Core QueryContext directly.
    /// Used for FindAsync ID expressions that access queryContext.ParameterValues[...].
    /// </summary>
    private static object? CompileAndExecuteWithQueryContext(Expression expression, QueryContext queryContext)
    {
        try
        {
            var replacer = new QueryContextReplacerVisitor(queryContext);
            var replacedExpression = replacer.Visit(expression);

            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(replacedExpression, typeof(object)));

            var compiled = lambda.Compile();
            return compiled();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Base visitor that handles closure captures (field/property access on constant objects).
    /// Both parameter replacement strategies share this behavior.
    /// </summary>
    private abstract class ClosureCaptureVisitor : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            // Handle closure captures (field access on constant objects)
            if (node.Expression is ConstantExpression constantExpr && constantExpr.Value != null)
            {
                var member = node.Member;
                object? value = member switch
                {
                    FieldInfo field => field.GetValue(constantExpr.Value),
                    PropertyInfo prop => prop.GetValue(constantExpr.Value),
                    _ => null
                };

                if (value != null)
                {
                    return Expression.Constant(value, node.Type);
                }
            }

            return base.VisitMember(node);
        }
    }

    /// <summary>
    /// Replaces named parameters using IFirestoreQueryContext.ParameterValues dictionary.
    /// Used for where clause values, pagination, etc.
    /// </summary>
    private class ParameterReplacerVisitor : ClosureCaptureVisitor
    {
        private readonly IReadOnlyDictionary<string, object?> _parameterValues;

        public ParameterReplacerVisitor(IReadOnlyDictionary<string, object?> parameterValues)
        {
            _parameterValues = parameterValues;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name != null && _parameterValues.TryGetValue(node.Name, out var parameterValue))
            {
                return Expression.Constant(parameterValue, node.Type);
            }

            return base.VisitParameter(node);
        }
    }

    /// <summary>
    /// Replaces QueryContext parameter with the actual instance.
    /// Also replaces named parameters from QueryContext.ParameterValues.
    /// Used for FindAsync ID expressions that reference queryContext.ParameterValues[...].
    /// </summary>
    private class QueryContextReplacerVisitor : ClosureCaptureVisitor
    {
        private readonly QueryContext _queryContext;

        public QueryContextReplacerVisitor(QueryContext queryContext)
        {
            _queryContext = queryContext;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            // Replace the queryContext parameter with the actual QueryContext
            if (node.Name == "queryContext" && node.Type == typeof(QueryContext))
            {
                return Expression.Constant(_queryContext, typeof(QueryContext));
            }

            // Replace named parameters from ParameterValues
            if (node.Name != null && _queryContext.ParameterValues.TryGetValue(node.Name, out var parameterValue))
            {
                return Expression.Constant(parameterValue, node.Type);
            }

            return base.VisitParameter(node);
        }
    }
}
