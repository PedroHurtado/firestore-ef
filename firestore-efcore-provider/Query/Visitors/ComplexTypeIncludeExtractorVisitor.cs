using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Visitor that extracts Include expressions targeting ComplexType properties
    /// and removes them from the expression tree to prevent EF Core from rejecting them.
    /// The extracted includes are stored for later processing during deserialization.
    /// </summary>
    internal class ComplexTypeIncludeExtractorVisitor : ExpressionVisitor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        /// <summary>
        /// Thread-safe storage for ComplexType includes during query compilation.
        /// Used to pass includes from preprocessing to deserialization.
        /// </summary>
        private static readonly AsyncLocal<List<LambdaExpression>> _currentComplexTypeIncludes = new();

        /// <summary>
        /// Gets the current ComplexType includes for this async context.
        /// </summary>
        public static List<LambdaExpression> CurrentComplexTypeIncludes
        {
            get => _currentComplexTypeIncludes.Value ?? new List<LambdaExpression>();
            set => _currentComplexTypeIncludes.Value = value;
        }

        public ComplexTypeIncludeExtractorVisitor(QueryCompilationContext queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
            // Initialize a new list for this query compilation
            _currentComplexTypeIncludes.Value = new List<LambdaExpression>();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is an Include call
            if (node.Method.Name == "Include" &&
                node.Method.DeclaringType == typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
            {
                // Get the lambda expression (second argument)
                // It can be UnaryExpression (Quote) or directly LambdaExpression
                LambdaExpression? lambda = null;
                if (node.Arguments.Count >= 2)
                {
                    if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambdaFromUnary)
                    {
                        lambda = lambdaFromUnary;
                    }
                    else if (node.Arguments[1] is LambdaExpression lambdaDirect)
                    {
                        lambda = lambdaDirect;
                    }
                }

                if (lambda != null && IsComplexTypeInclude(lambda.Body))
                {
                    // Extract and store the include path for later processing
                    StoreComplexTypeInclude(lambda);

                    // Return just the source (first argument) without the Include
                    // This effectively removes the Include from the expression tree
                    return Visit(node.Arguments[0]);
                }
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Determines if the Include expression targets a property inside a ComplexType.
        /// Pattern: e => e.ComplexTypeProperty.NavigationProperty
        /// </summary>
        private bool IsComplexTypeInclude(Expression expression)
        {
            // We're looking for a member access chain like: e.ComplexType.Navigation
            if (expression is MemberExpression memberExpr)
            {
                // Check if there's a chain (e.ComplexType.Something)
                if (memberExpr.Expression is MemberExpression parentMemberExpr)
                {
                    // Check if the parent is accessing a ComplexType property
                    var parentProperty = parentMemberExpr.Member as PropertyInfo;
                    if (parentProperty != null)
                    {
                        var rootType = GetRootEntityType(parentMemberExpr);
                        // Check if this property's type is configured as ComplexType in the model
                        var entityType = _queryCompilationContext.Model.FindEntityType(rootType);

                        if (entityType != null)
                        {
                            var complexProperty = entityType.FindComplexProperty(parentProperty.Name);
                            if (complexProperty != null)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the root entity type from a member expression chain.
        /// </summary>
        private Type GetRootEntityType(MemberExpression memberExpr)
        {
            var current = memberExpr.Expression;
            while (current is MemberExpression parent)
            {
                current = parent.Expression;
            }

            if (current is ParameterExpression param)
            {
                return param.Type;
            }

            return current?.Type ?? typeof(object);
        }

        /// <summary>
        /// Stores the ComplexType include information for later processing during deserialization.
        /// </summary>
        private void StoreComplexTypeInclude(LambdaExpression includeExpression)
        {
            // Store in AsyncLocal for thread-safe access during deserialization
            _currentComplexTypeIncludes.Value ??= new List<LambdaExpression>();
            _currentComplexTypeIncludes.Value.Add(includeExpression);
        }
    }
}