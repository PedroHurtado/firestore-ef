using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Visitor that extracts filter expressions from Filtered Includes before EF Core processes them.
    /// Pattern: .Include(c => c.Collection.Where(x => ...))
    /// The extracted filters are stored in FirestoreQueryCompilationContext for later use.
    /// </summary>
    internal class FilteredIncludeExtractorVisitor : ExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;

        public FilteredIncludeExtractorVisitor(QueryCompilationContext queryCompilationContext)
        {
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is an Include or ThenInclude call
            if ((node.Method.Name == "Include" || node.Method.Name == "ThenInclude") &&
                node.Method.DeclaringType == typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
            {
                System.Diagnostics.Debug.WriteLine($"[FilteredIncludeExtractor] Found {node.Method.Name} call");

                // Get the lambda expression (second argument)
                LambdaExpression? lambda = ExtractLambda(node);

                if (lambda != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FilteredIncludeExtractor] Lambda body: {lambda.Body}");
                    System.Diagnostics.Debug.WriteLine($"[FilteredIncludeExtractor] Lambda body type: {lambda.Body.GetType().Name}");

                    // Try to extract filter operations from the lambda body
                    var filterInfo = ExtractFilterInfo(lambda.Body);
                    if (filterInfo != null)
                    {
                        // Get the navigation name from the filter chain
                        var navigationName = ExtractNavigationName(lambda.Body);
                        System.Diagnostics.Debug.WriteLine($"[FilteredIncludeExtractor] Navigation: {navigationName}, HasFilter: {filterInfo.FilterExpression != null}");

                        if (!string.IsNullOrEmpty(navigationName))
                        {
                            _firestoreContext.AddFilteredInclude(navigationName, filterInfo);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[FilteredIncludeExtractor] No filter info extracted");
                    }
                }
            }

            return base.VisitMethodCall(node);
        }

        private LambdaExpression? ExtractLambda(MethodCallExpression node)
        {
            if (node.Arguments.Count >= 2)
            {
                if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambdaFromUnary)
                {
                    return lambdaFromUnary;
                }
                else if (node.Arguments[1] is LambdaExpression lambdaDirect)
                {
                    return lambdaDirect;
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts filter information from a filtered include expression.
        /// Handles patterns like: c.Pedidos.Where(p => ...).OrderBy(p => ...).Take(n)
        /// </summary>
        private IncludeInfo? ExtractFilterInfo(Expression body)
        {
            var methodCalls = new List<MethodCallExpression>();
            CollectMethodCalls(body, methodCalls);

            // Look for LINQ methods that indicate a filtered include
            bool hasFilterOperations = methodCalls.Any(m =>
                m.Method.Name == "Where" ||
                m.Method.Name == "OrderBy" ||
                m.Method.Name == "OrderByDescending" ||
                m.Method.Name == "ThenBy" ||
                m.Method.Name == "ThenByDescending" ||
                m.Method.Name == "Take" ||
                m.Method.Name == "Skip");

            if (!hasFilterOperations)
                return null;

            // Get the navigation name first to create IncludeInfo
            var navigationName = ExtractNavigationName(body);
            if (string.IsNullOrEmpty(navigationName))
                return null;

            // Create IncludeInfo with navigation name (navigation will be matched later)
            var includeInfo = new IncludeInfo(navigationName);

            foreach (var methodCall in methodCalls)
            {
                ProcessMethodCall(methodCall, includeInfo);
            }

            return includeInfo.HasOperations ? includeInfo : null;
        }

        private void ProcessMethodCall(MethodCallExpression methodCall, IncludeInfo includeInfo)
        {
            var methodName = methodCall.Method.Name;

            switch (methodName)
            {
                case "Where":
                    // For Enumerable.Where, Args[0] is source, Args[1] is predicate
                    if (methodCall.Arguments.Count >= 2)
                    {
                        var predicate = ExtractLambdaFromArg(methodCall.Arguments[1]);
                        if (predicate != null && !IsCorrelationFilter(predicate))
                        {
                            includeInfo.FilterExpression = predicate;
                        }
                    }
                    break;

                case "OrderBy":
                case "OrderByDescending":
                    if (methodCall.Arguments.Count >= 2)
                    {
                        var keySelector = ExtractLambdaFromArg(methodCall.Arguments[1]);
                        if (keySelector != null)
                        {
                            includeInfo.OrderByExpressions.Clear();
                            includeInfo.OrderByExpressions.Add((keySelector, methodName == "OrderByDescending"));
                        }
                    }
                    break;

                case "ThenBy":
                case "ThenByDescending":
                    if (methodCall.Arguments.Count >= 2)
                    {
                        var keySelector = ExtractLambdaFromArg(methodCall.Arguments[1]);
                        if (keySelector != null)
                        {
                            includeInfo.OrderByExpressions.Add((keySelector, methodName == "ThenByDescending"));
                        }
                    }
                    break;

                case "Take":
                    if (methodCall.Arguments.Count >= 2)
                    {
                        var count = ExtractConstantInt(methodCall.Arguments[1]);
                        if (count.HasValue)
                        {
                            includeInfo.Take = count.Value;
                        }
                    }
                    break;

                case "Skip":
                    if (methodCall.Arguments.Count >= 2)
                    {
                        var count = ExtractConstantInt(methodCall.Arguments[1]);
                        if (count.HasValue)
                        {
                            includeInfo.Skip = count.Value;
                        }
                    }
                    break;
            }
        }

        private LambdaExpression? ExtractLambdaFromArg(Expression arg)
        {
            if (arg is UnaryExpression quote && quote.Operand is LambdaExpression lambda)
            {
                return lambda;
            }
            if (arg is LambdaExpression directLambda)
            {
                return directLambda;
            }
            return null;
        }

        private int? ExtractConstantInt(Expression expr)
        {
            if (expr is ConstantExpression constant && constant.Value is int value)
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// Checks if a predicate is a correlation filter generated by EF Core.
        /// Correlation filters use Property(c, "Id") patterns to join parent-child.
        /// These are NOT user-specified filters and should be ignored.
        /// </summary>
        private bool IsCorrelationFilter(LambdaExpression predicate)
        {
            var exprString = predicate.Body.ToString();

            // EF Core generates correlation filters with patterns like:
            // Property(c, "Id") or Equals(Property(...), Property(...))
            // These always contain "Property(" which user filters don't have
            return exprString.Contains("Property(");
        }

        /// <summary>
        /// Extracts the navigation property name from a filtered include expression.
        /// </summary>
        private string? ExtractNavigationName(Expression body)
        {
            // Walk down the method call chain to find the member access
            var current = body;

            while (current is MethodCallExpression methodCall)
            {
                // Source is usually the first argument
                if (methodCall.Arguments.Count > 0)
                {
                    current = methodCall.Arguments[0];
                }
                else if (methodCall.Object != null)
                {
                    current = methodCall.Object;
                }
                else
                {
                    break;
                }
            }

            if (current is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }

            return null;
        }

        private void CollectMethodCalls(Expression expression, List<MethodCallExpression> methodCalls)
        {
            switch (expression)
            {
                case MethodCallExpression methodCall:
                    methodCalls.Add(methodCall);
                    // Continue traversing - the source is usually in Arguments[0]
                    if (methodCall.Arguments.Count > 0)
                    {
                        CollectMethodCalls(methodCall.Arguments[0], methodCalls);
                    }
                    if (methodCall.Object != null)
                    {
                        CollectMethodCalls(methodCall.Object, methodCalls);
                    }
                    break;

                case UnaryExpression unary:
                    CollectMethodCalls(unary.Operand, methodCalls);
                    break;
            }
        }
    }
}
