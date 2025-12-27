using Firestore.EntityFrameworkCore.Query.Translators;
using Firestore.EntityFrameworkCore.Query.Visitors;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Where.
    /// El PredicateBody ya debe tener los parámetros de runtime reemplazados por el Visitor.
    /// </summary>
    public record TranslateWhereRequest(
        ShapedQueryExpression Source,
        Expression PredicateBody);

    /// <summary>
    /// Feature: Where translation.
    /// Where applies filter clauses to the query.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Where Translation

        /// <summary>
        /// Traduce Where.
        /// Preprocesa patrones de array, traduce el predicado y aplica los filtros.
        /// Maneja optimización de Id-only queries y grupos OR.
        /// </summary>
        public static ShapedQueryExpression? TranslateWhere(TranslateWhereRequest request)
        {
            var (source, predicateBody) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Preprocess for array patterns (ArrayContains, ArrayContainsAny)
            var preprocessedBody = PreprocessArrayContainsPatterns(predicateBody);

            // Translate using FirestoreWhereTranslator
            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(preprocessedBody);

            if (filterResult == null)
            {
                return null;
            }

            // Handle OR groups
            if (filterResult.IsOrGroup)
            {
                if (ast.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot add OR filters to an ID-only query.");
                }

                ast.AddOrFilterGroup(filterResult.OrGroup!);
                return source.UpdateQueryExpression(ast);
            }

            // Handle AND clauses (single or multiple) with possible nested OR groups
            var clauses = filterResult.AndClauses;
            var nestedOrGroups = filterResult.NestedOrGroups;

            if (clauses.Count == 0 && nestedOrGroups.Count == 0)
            {
                return null;
            }

            // Check for ID-only queries (optimization: use GetDocumentAsync instead of query)
            // Only valid when there's a SINGLE Id == clause with NO other filters
            if (clauses.Count == 1 && clauses[0].PropertyName == "Id")
            {
                var whereClause = clauses[0];
                if (whereClause.Operator != FirestoreOperator.EqualTo)
                {
                    throw new InvalidOperationException(
                        "Firestore ID queries only support the '==' operator.");
                }

                // If there are already other filters, treat Id as a normal filter
                // (executor will use FieldPath.DocumentId)
                if (ast.Filters.Count > 0 || ast.OrFilterGroups.Count > 0)
                {
                    ast.AddFilter(whereClause);
                    return source.UpdateQueryExpression(ast);
                }

                if (ast.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot apply multiple ID filters.");
                }

                // Create IdOnlyQuery (optimization for single document fetch)
                ast.WithIdValueExpression(whereClause.ValueExpression);
                return source.UpdateQueryExpression(ast);
            }

            // If we already have an IdOnlyQuery and need to add more filters,
            // convert it to a normal query with FieldPath.DocumentId
            if (ast.IsIdOnlyQuery)
            {
                // Create Id clause from the existing IdValueExpression
                var idClause = new FirestoreWhereClause(
                    "Id", FirestoreOperator.EqualTo, ast.IdValueExpression!, null);

                // Create new query without IdValueExpression (will use FieldPath.DocumentId)
                // Clear IdValueExpression by setting filters with the id clause
                ast.ClearIdValueExpressionWithFilters(new[] { idClause });

                // Add the new clauses
                ast.AddFilters(clauses);
                return source.UpdateQueryExpression(ast);
            }

            // Add all AND clauses
            ast.AddFilters(clauses);

            // Add nested OR groups (for patterns like A && (B || C))
            foreach (var orGroup in nestedOrGroups)
            {
                ast.AddOrFilterGroup(orGroup);
            }

            return source.UpdateQueryExpression(ast);
        }

        #endregion

        #region Where Preprocessing

        /// <summary>
        /// Preprocess expression tree to transform array Contains patterns
        /// into FirestoreArrayContainsExpression markers.
        /// </summary>
        private static Expression PreprocessArrayContainsPatterns(Expression expression)
        {
            // Handle the specific pattern: EF.Property<List<T>>().AsQueryable().Contains(value)
            if (expression is MethodCallExpression methodCall)
            {
                // First, recursively preprocess children
                var newObject = methodCall.Object != null
                    ? PreprocessArrayContainsPatterns(methodCall.Object)
                    : null;
                var newArgs = methodCall.Arguments.Select(PreprocessArrayContainsPatterns).ToList();

                // Check if this is the pattern we're looking for
                if (methodCall.Method.Name == "Contains")
                {
                    // Pattern 1: Instance .Contains(value) where object is AsQueryable()
                    if (newObject is MethodCallExpression asQueryableCall &&
                        asQueryableCall.Method.Name == "AsQueryable")
                    {
                        var propertyName = ExtractPropertyNameFromEFPropertyChain(
                            asQueryableCall.Arguments.Count == 1
                                ? asQueryableCall.Arguments[0]
                                : asQueryableCall.Object);

                        if (propertyName != null && newArgs.Count == 1)
                        {
                            return new FirestoreArrayContainsExpression(propertyName, newArgs[0]);
                        }
                    }

                    // Pattern 2: Static Enumerable.Contains(asQueryable, value)
                    if (newObject == null && newArgs.Count == 2 &&
                        newArgs[0] is MethodCallExpression asQueryable &&
                        asQueryable.Method.Name == "AsQueryable")
                    {
                        var propertyName = ExtractPropertyNameFromEFPropertyChain(
                            asQueryable.Arguments.Count == 1
                                ? asQueryable.Arguments[0]
                                : asQueryable.Object);

                        if (propertyName != null)
                        {
                            return new FirestoreArrayContainsExpression(propertyName, newArgs[1]);
                        }
                    }
                }

                // Pattern for ArrayContainsAny: .Any(t => list.Contains(t))
                if (methodCall.Method.Name == "Any")
                {
                    // Get the source (should be AsQueryable() or direct property)
                    Expression? sourceExpr = newObject ?? (newArgs.Count > 0 ? newArgs[0] : null);
                    Expression? predicateExpr = newObject != null
                        ? (newArgs.Count > 0 ? newArgs[0] : null)
                        : (newArgs.Count > 1 ? newArgs[1] : null);

                    string? propertyName = null;

                    // Pattern 1: AsQueryable() wrapping EF.Property
                    if (sourceExpr is MethodCallExpression asQueryableCall &&
                        asQueryableCall.Method.Name == "AsQueryable")
                    {
                        propertyName = ExtractPropertyNameFromEFPropertyChain(
                            asQueryableCall.Arguments.Count == 1
                                ? asQueryableCall.Arguments[0]
                                : asQueryableCall.Object);
                    }
                    // Pattern 2: Direct MemberExpression (e.Tags) - before EF transformation
                    else if (sourceExpr is MemberExpression memberExpr)
                    {
                        propertyName = memberExpr.Member.Name;
                    }

                    // Extract lambda from predicate (might be wrapped in Quote for Queryable methods)
                    LambdaExpression? predicateLambda = predicateExpr as LambdaExpression;
                    if (predicateLambda == null && predicateExpr is UnaryExpression quote &&
                        quote.NodeType == ExpressionType.Quote &&
                        quote.Operand is LambdaExpression quotedLambda)
                    {
                        predicateLambda = quotedLambda;
                    }

                    if (propertyName != null && predicateLambda != null)
                    {
                        // Check if lambda body is list.Contains(parameter)
                        var listExpr = ExtractListFromContainsPredicate(predicateLambda);
                        if (listExpr != null)
                        {
                            return new FirestoreArrayContainsAnyExpression(propertyName, listExpr);
                        }
                    }
                }

                // Return updated method call if children changed
                if (newObject != methodCall.Object || !newArgs.SequenceEqual(methodCall.Arguments))
                {
                    return methodCall.Update(newObject, newArgs);
                }
            }

            // Handle lambda expressions
            if (expression is LambdaExpression lambda)
            {
                var newBody = PreprocessArrayContainsPatterns(lambda.Body);
                if (newBody != lambda.Body)
                {
                    return Expression.Lambda(lambda.Type, newBody, lambda.Parameters);
                }
            }

            // Handle binary expressions (AND, OR)
            if (expression is BinaryExpression binary)
            {
                var newLeft = PreprocessArrayContainsPatterns(binary.Left);
                var newRight = PreprocessArrayContainsPatterns(binary.Right);
                if (newLeft != binary.Left || newRight != binary.Right)
                {
                    return binary.Update(newLeft, binary.Conversion, newRight);
                }
            }

            // Handle unary expressions (NOT)
            if (expression is UnaryExpression unary)
            {
                var newOperand = PreprocessArrayContainsPatterns(unary.Operand);
                if (newOperand != unary.Operand)
                {
                    return unary.Update(newOperand);
                }
            }

            return expression;
        }

        /// <summary>
        /// Extracts property name from EF.Property chain like:
        /// EF.Property(e, "Field").AsQueryable() or just EF.Property(e, "Field")
        /// </summary>
        private static string? ExtractPropertyNameFromEFPropertyChain(Expression? expression)
        {
            if (expression is MethodCallExpression methodCall)
            {
                // Check if it's AsQueryable() wrapping EF.Property
                if (methodCall.Method.Name == "AsQueryable" && methodCall.Arguments.Count == 1)
                {
                    return ExtractPropertyNameFromEFPropertyChain(methodCall.Arguments[0]);
                }

                // Check if it's EF.Property<T>(entity, "PropertyName")
                if (methodCall.Method.Name == "Property" &&
                    methodCall.Method.DeclaringType?.Name == "EF")
                {
                    if (methodCall.Arguments.Count >= 2 && methodCall.Arguments[1] is ConstantExpression constant)
                    {
                        return constant.Value as string;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the list expression from a predicate like: t => list.Contains(t)
        /// Returns the 'list' expression if the pattern matches.
        /// </summary>
        private static Expression? ExtractListFromContainsPredicate(LambdaExpression lambda)
        {
            // Lambda should have exactly one parameter
            if (lambda.Parameters.Count != 1)
                return null;

            var parameter = lambda.Parameters[0];

            // Body should be a method call to Contains
            if (lambda.Body is not MethodCallExpression containsCall ||
                containsCall.Method.Name != "Contains")
                return null;

            // Pattern 1: list.Contains(param) - instance method
            if (containsCall.Object != null && containsCall.Arguments.Count == 1)
            {
                // Check if argument references the lambda parameter
                if (IsParameterReference(containsCall.Arguments[0], parameter))
                {
                    return containsCall.Object;
                }
            }

            // Pattern 2: Enumerable.Contains(list, param) - static method
            if (containsCall.Object == null && containsCall.Arguments.Count == 2)
            {
                // Check if second argument references the lambda parameter
                if (IsParameterReference(containsCall.Arguments[1], parameter))
                {
                    return containsCall.Arguments[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if an expression references a lambda parameter
        /// </summary>
        private static bool IsParameterReference(Expression expression, ParameterExpression parameter)
        {
            // Direct parameter reference
            if (expression is ParameterExpression paramExpr)
            {
                return paramExpr == parameter || paramExpr.Name == parameter.Name;
            }

            // Wrapped in Convert/ConvertChecked (common for value types)
            if (expression is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                return IsParameterReference(unary.Operand, parameter);
            }

            return false;
        }

        #endregion
    }
}
