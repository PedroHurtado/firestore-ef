using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates IncludeExpression to IncludeInfo.
    ///
    /// Receives IncludeExpression from EF Core and returns IncludeInfo with:
    /// - NavigationName (string)
    /// - IsCollection (bool)
    /// - Filters (FirestoreWhereClause)
    /// - OrderBy (FirestoreOrderByClause)
    /// - Take/Skip (int? or Expression?)
    ///
    /// Reuses existing translators:
    /// - FirestoreWhereTranslator for Where filters
    /// - FirestoreOrderByTranslator for OrderBy/ThenBy
    /// - FirestoreLimitTranslator for Take/Skip
    /// </summary>
    internal class FirestoreIncludeTranslator
    {
        private readonly FirestoreWhereTranslator _whereTranslator = new();
        private readonly FirestoreOrderByTranslator _orderByTranslator = new();
        private readonly FirestoreLimitTranslator _limitTranslator = new();

        /// <summary>
        /// Translates an IncludeExpression to a list of IncludeInfo.
        /// Returns multiple items when there are ThenInclude chains.
        /// </summary>
        public List<IncludeInfo> Translate(IncludeExpression includeExpression)
        {
            var results = new List<IncludeInfo>();
            TranslateRecursive(includeExpression, results);
            return results;
        }

        /// <summary>
        /// Recursively translates IncludeExpression and nested ThenInclude chains.
        /// </summary>
        private void TranslateRecursive(IncludeExpression includeExpression, List<IncludeInfo> results)
        {
            // Extract navigation info
            var navigationName = includeExpression.Navigation.Name;
            var isCollection = includeExpression.Navigation.IsCollection;

            // Create IncludeInfo
            var includeInfo = new IncludeInfo(navigationName, isCollection);

            // Extract and translate filter operations from NavigationExpression
            TranslateOperations(includeExpression.NavigationExpression, includeInfo);

            results.Add(includeInfo);

            // Process nested includes (ThenInclude chains)
            if (includeExpression.EntityExpression is IncludeExpression nestedInclude)
            {
                TranslateRecursive(nestedInclude, results);
            }
        }

        /// <summary>
        /// Translates filter operations from NavigationExpression into IncludeInfo.
        /// </summary>
        private void TranslateOperations(Expression? navigationExpression, IncludeInfo includeInfo)
        {
            if (navigationExpression == null)
                return;

            // Extract method chain from NavigationExpression
            var methodCalls = ExtractMethodCalls(navigationExpression);
            if (methodCalls.Count == 0)
                return;

            // Process each method call
            foreach (var methodCall in methodCalls)
            {
                ProcessMethodCall(methodCall, includeInfo);
            }
        }

        /// <summary>
        /// Extracts method calls from NavigationExpression.
        /// Handles MaterializeCollectionNavigationExpression and direct method chains.
        /// </summary>
        private List<MethodCallExpression> ExtractMethodCalls(Expression expression)
        {
            var methodCalls = new List<MethodCallExpression>();

            // Handle MaterializeCollectionNavigationExpression
            var typeName = expression.GetType().Name;
            if (typeName == "MaterializeCollectionNavigationExpression")
            {
                var subqueryProp = expression.GetType().GetProperty("Subquery");
                if (subqueryProp != null)
                {
                    var subquery = subqueryProp.GetValue(expression) as Expression;
                    if (subquery != null)
                    {
                        CollectMethodCalls(subquery, methodCalls);
                    }
                }
                return methodCalls;
            }

            // Direct method chain
            CollectMethodCalls(expression, methodCalls);
            return methodCalls;
        }

        /// <summary>
        /// Collects method calls walking down the expression tree.
        /// </summary>
        private void CollectMethodCalls(Expression expression, List<MethodCallExpression> methodCalls)
        {
            var current = expression;
            while (current is MethodCallExpression methodCall)
            {
                methodCalls.Add(methodCall);
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
        }

        /// <summary>
        /// Processes a single method call and adds operations to IncludeInfo.
        /// </summary>
        private void ProcessMethodCall(MethodCallExpression methodCall, IncludeInfo includeInfo)
        {
            var methodName = methodCall.Method.Name;

            switch (methodName)
            {
                case "Where":
                    ProcessWhere(methodCall, includeInfo);
                    break;

                case "OrderBy":
                    ProcessOrderBy(methodCall, includeInfo, isFirst: true, descending: false);
                    break;

                case "OrderByDescending":
                    ProcessOrderBy(methodCall, includeInfo, isFirst: true, descending: true);
                    break;

                case "ThenBy":
                    ProcessOrderBy(methodCall, includeInfo, isFirst: false, descending: false);
                    break;

                case "ThenByDescending":
                    ProcessOrderBy(methodCall, includeInfo, isFirst: false, descending: true);
                    break;

                case "Take":
                    ProcessTake(methodCall, includeInfo);
                    break;

                case "Skip":
                    ProcessSkip(methodCall, includeInfo);
                    break;
            }
        }

        /// <summary>
        /// Processes Where using FirestoreWhereTranslator.
        /// </summary>
        private void ProcessWhere(MethodCallExpression methodCall, IncludeInfo includeInfo)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var predicateLambda = ExtractLambda(methodCall.Arguments[1]);
            if (predicateLambda == null)
                return;

            // Skip correlation filters (EF Core internal join conditions)
            if (IsCorrelationFilter(predicateLambda))
                return;

            // Use FirestoreWhereTranslator
            var filterResult = _whereTranslator.Translate(predicateLambda.Body);
            if (filterResult != null)
            {
                // Handle top-level OR group (pure OR expression)
                if (filterResult.IsOrGroup && filterResult.OrGroup != null)
                {
                    includeInfo.AddOrFilterGroup(filterResult.OrGroup);
                }

                // Handle AND clauses
                includeInfo.AddFilters(filterResult.AndClauses);

                // Handle nested OR groups (OR within AND)
                foreach (var orGroup in filterResult.NestedOrGroups)
                {
                    includeInfo.AddOrFilterGroup(orGroup);
                }
            }
        }

        /// <summary>
        /// Processes OrderBy/ThenBy using FirestoreOrderByTranslator.
        /// </summary>
        private void ProcessOrderBy(MethodCallExpression methodCall, IncludeInfo includeInfo, bool isFirst, bool descending)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var keySelector = ExtractLambda(methodCall.Arguments[1]);
            if (keySelector == null)
                return;

            var clause = _orderByTranslator.Translate(keySelector, ascending: !descending);
            if (clause != null)
            {
                if (isFirst)
                    includeInfo.SetOrderBy(clause);
                else
                    includeInfo.AddOrderBy(clause);
            }
        }

        /// <summary>
        /// Processes Take using FirestoreLimitTranslator.
        /// </summary>
        private void ProcessTake(MethodCallExpression methodCall, IncludeInfo includeInfo)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var countExpression = methodCall.Arguments[1];
            var constantValue = _limitTranslator.Translate(countExpression);
            if (constantValue.HasValue)
            {
                includeInfo.WithTake(constantValue.Value);
            }
            else
            {
                includeInfo.WithTakeExpression(countExpression);
            }
        }

        /// <summary>
        /// Processes Skip using FirestoreLimitTranslator.
        /// </summary>
        private void ProcessSkip(MethodCallExpression methodCall, IncludeInfo includeInfo)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var countExpression = methodCall.Arguments[1];
            var constantValue = _limitTranslator.Translate(countExpression);
            if (constantValue.HasValue)
            {
                includeInfo.WithSkip(constantValue.Value);
            }
            else
            {
                includeInfo.WithSkipExpression(countExpression);
            }
        }

        /// <summary>
        /// Extracts a LambdaExpression from an argument (handles Quote wrapper).
        /// </summary>
        private static LambdaExpression? ExtractLambda(Expression expression)
        {
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Quote)
            {
                return unary.Operand as LambdaExpression;
            }
            return expression as LambdaExpression;
        }

        /// <summary>
        /// Checks if a predicate is a correlation filter generated by EF Core.
        /// </summary>
        private static bool IsCorrelationFilter(LambdaExpression predicate)
        {
            var exprString = predicate.Body.ToString();
            return exprString.Contains("Property(");
        }
    }
}
