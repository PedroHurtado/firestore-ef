using Firestore.EntityFrameworkCore.Query.Visitors;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Preprocessing
{
    /// <summary>
    /// Transforms array Contains patterns in expression trees into Firestore-specific marker expressions.
    ///
    /// This transformer handles patterns that EF Core creates when processing array operations:
    /// - EF.Property&lt;List&lt;T&gt;&gt;().AsQueryable().Contains(value) → FirestoreArrayContainsExpression
    /// - array.Any(t => list.Contains(t)) → FirestoreArrayContainsAnyExpression
    ///
    /// Must be applied BEFORE EF Core's base visitor processes the expression,
    /// otherwise EF Core interprets AsQueryable() as a subquery and fails.
    /// </summary>
    public static class ArrayContainsPatternTransformer
    {
        /// <summary>
        /// Transforms array Contains patterns into Firestore marker expressions.
        /// Returns the original expression if no patterns are found.
        /// </summary>
        public static Expression Transform(Expression expression)
        {
            if (expression == null) return null!;

            // Handle method calls (Contains, Any)
            if (expression is MethodCallExpression methodCall)
            {
                return TransformMethodCall(methodCall);
            }

            // Handle lambda expressions
            if (expression is LambdaExpression lambda)
            {
                var newBody = Transform(lambda.Body);
                if (newBody != lambda.Body)
                {
                    return Expression.Lambda(lambda.Type, newBody, lambda.Parameters);
                }
            }

            // Handle binary expressions (AND, OR)
            if (expression is BinaryExpression binary)
            {
                var newLeft = Transform(binary.Left);
                var newRight = Transform(binary.Right);
                if (newLeft != binary.Left || newRight != binary.Right)
                {
                    return binary.Update(newLeft, binary.Conversion, newRight);
                }
            }

            // Handle unary expressions (NOT)
            if (expression is UnaryExpression unary)
            {
                var newOperand = Transform(unary.Operand);
                if (newOperand != unary.Operand)
                {
                    return unary.Update(newOperand);
                }
            }

            return expression;
        }

        private static Expression TransformMethodCall(MethodCallExpression methodCall)
        {
            // First, recursively transform children
            var newObject = methodCall.Object != null
                ? Transform(methodCall.Object)
                : null;
            var newArgs = methodCall.Arguments.Select(Transform).ToList();

            // Check if this is a Contains pattern
            if (methodCall.Method.Name == "Contains")
            {
                var result = TryTransformContains(methodCall, newObject, newArgs);
                if (result != null) return result;
            }

            // Check if this is an Any pattern (for ArrayContainsAny)
            if (methodCall.Method.Name == "Any")
            {
                var result = TryTransformAny(methodCall, newObject, newArgs);
                if (result != null) return result;
            }

            // Return updated method call if children changed
            if (newObject != methodCall.Object || !newArgs.SequenceEqual(methodCall.Arguments))
            {
                return methodCall.Update(newObject, newArgs);
            }

            return methodCall;
        }

        /// <summary>
        /// Tries to transform Contains patterns to FirestoreArrayContainsExpression.
        /// Pattern 1: instance.Contains(value) where instance is AsQueryable()
        /// Pattern 2: Enumerable.Contains(asQueryable, value)
        /// </summary>
        private static Expression? TryTransformContains(
            MethodCallExpression methodCall,
            Expression? newObject,
            List<Expression> newArgs)
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

            return null;
        }

        /// <summary>
        /// Tries to transform Any patterns to FirestoreArrayContainsAnyExpression.
        /// Pattern: array.Any(t => list.Contains(t))
        /// </summary>
        private static Expression? TryTransformAny(
            MethodCallExpression methodCall,
            Expression? newObject,
            List<Expression> newArgs)
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

            return null;
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
    }
}
