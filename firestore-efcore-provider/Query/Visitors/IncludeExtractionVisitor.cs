using Firestore.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Visitor especializado para extraer TODOS los includes del árbol de expresiones,
    /// incluyendo filtros para Filtered Includes.
    /// </summary>
    internal class IncludeExtractionVisitor : ExpressionVisitor
    {
        public List<IReadOnlyNavigation> DetectedNavigations { get; } = new();

        /// <summary>
        /// Includes con información de filtros extraída.
        /// </summary>
        public List<IncludeInfo> DetectedIncludes { get; } = new();

        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                if (includeExpression.Navigation is IReadOnlyNavigation navigation)
                {
                    DetectedNavigations.Add(navigation);

                    // Crear IncludeInfo con información de filtro
                    var includeInfo = new IncludeInfo(navigation);

                    // Intentar extraer filtro del NavigationExpression
                    ExtractFilterFromNavigationExpression(includeExpression.NavigationExpression, includeInfo);

                    // DEBUG: Log para diagnosticar
                    System.Diagnostics.Debug.WriteLine($"[IncludeExtraction] Navigation: {navigation.Name}, HasFilter: {includeInfo.FilterExpression != null}");
                    System.Diagnostics.Debug.WriteLine($"[IncludeExtraction] NavigationExpression Type: {includeExpression.NavigationExpression.GetType().Name}");

                    DetectedIncludes.Add(includeInfo);
                }

                // Visitar EntityExpression y NavigationExpression para ThenInclude anidados
                Visit(includeExpression.EntityExpression);
                Visit(includeExpression.NavigationExpression);

                return node;
            }

            return base.VisitExtension(node);
        }

        /// <summary>
        /// Extrae el filtro Where, OrderBy, Take, Skip del NavigationExpression.
        /// EF Core transforma .Include(c => c.Pedidos.Where(...)) en expresiones con MethodCallExpression.
        /// </summary>
        private void ExtractFilterFromNavigationExpression(Expression navigationExpression, IncludeInfo includeInfo)
        {
            // DEBUG: Log full tree
            System.Diagnostics.Debug.WriteLine($"[ExtractFilter] Expression: {navigationExpression}");
            System.Diagnostics.Debug.WriteLine($"[ExtractFilter] Type: {navigationExpression.GetType().FullName}");

            // EF Core 8 uses CollectionResultExpression with Subquery for filtered includes
            var typeName = navigationExpression.GetType().Name;
            if (typeName == "CollectionResultExpression")
            {
                // Try to get the Navigation property for filter info
                var projectionProperty = navigationExpression.GetType().GetProperty("Projection");
                if (projectionProperty != null)
                {
                    var projection = projectionProperty.GetValue(navigationExpression);
                    if (projection != null)
                    {
                        var projTypeName = projection.GetType().Name;
                        System.Diagnostics.Debug.WriteLine($"[ExtractFilter] Projection Type: {projTypeName}");

                        // CollectionResultShaperExpression has InnerShaper
                        var innerProperty = projection.GetType().GetProperty("InnerShaper") ??
                                           projection.GetType().GetProperty("Subquery") ??
                                           projection.GetType().GetProperty("QueryExpression");

                        if (innerProperty != null)
                        {
                            var inner = innerProperty.GetValue(projection);
                            if (inner is Expression innerExpr)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ExtractFilter] Inner: {innerExpr}");
                                var innerMethodCalls = new List<MethodCallExpression>();
                                CollectMethodCalls(innerExpr, innerMethodCalls);
                                ProcessMethodCalls(innerMethodCalls, includeInfo);
                            }
                        }
                    }
                }
            }

            // El NavigationExpression puede contener MethodCallExpression para Where, OrderBy, Take, etc.
            // O puede ser una MaterializeCollectionNavigationExpression con subquery filtrada

            var methodCalls = new List<MethodCallExpression>();
            CollectMethodCalls(navigationExpression, methodCalls);
            ProcessMethodCalls(methodCalls, includeInfo);
        }

        private void ProcessMethodCalls(List<MethodCallExpression> methodCalls, IncludeInfo includeInfo)
        {
            foreach (var methodCall in methodCalls)
            {
                var methodName = methodCall.Method.Name;
                System.Diagnostics.Debug.WriteLine($"[ProcessMethodCalls] Method: {methodName}, Args: {methodCall.Arguments.Count}");

                switch (methodName)
                {
                    case "Where":
                        if (methodCall.Arguments.Count >= 2)
                        {
                            var predicateArg = methodCall.Arguments[1];
                            LambdaExpression? filterLambda = null;

                            if (predicateArg is UnaryExpression quote && quote.Operand is LambdaExpression lambda)
                            {
                                filterLambda = lambda;
                            }
                            else if (predicateArg is LambdaExpression directLambda)
                            {
                                filterLambda = directLambda;
                            }

                            // Only assign if NOT a correlation filter (EF Core internal join filter)
                            if (filterLambda != null && !IsCorrelationFilter(filterLambda))
                            {
                                includeInfo.FilterExpression = filterLambda;
                                System.Diagnostics.Debug.WriteLine($"[ProcessMethodCalls] Found Where filter: {filterLambda}");
                            }
                        }
                        break;

                    case "OrderBy":
                    case "OrderByDescending":
                        if (methodCall.Arguments.Count >= 2)
                        {
                            var keySelector = ExtractLambda(methodCall.Arguments[1]);
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
                            var keySelector = ExtractLambda(methodCall.Arguments[1]);
                            if (keySelector != null)
                            {
                                includeInfo.OrderByExpressions.Add((keySelector, methodName == "ThenByDescending"));
                            }
                        }
                        break;

                    case "Take":
                        if (methodCall.Arguments.Count >= 2)
                        {
                            var countExpr = methodCall.Arguments[1];
                            if (countExpr is ConstantExpression constant && constant.Value is int count)
                            {
                                includeInfo.Take = count;
                            }
                        }
                        break;

                    case "Skip":
                        if (methodCall.Arguments.Count >= 2)
                        {
                            var countExpr = methodCall.Arguments[1];
                            if (countExpr is ConstantExpression constant && constant.Value is int count)
                            {
                                includeInfo.Skip = count;
                            }
                        }
                        break;
                }
            }
        }

        private LambdaExpression? ExtractLambda(Expression expression)
        {
            if (expression is UnaryExpression quote && quote.Operand is LambdaExpression lambda)
            {
                return lambda;
            }
            if (expression is LambdaExpression directLambda)
            {
                return directLambda;
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
            // EF Core generates correlation filters with Property( calls
            return exprString.Contains("Property(");
        }

        /// <summary>
        /// Recursively collects MethodCallExpressions from the expression tree.
        /// </summary>
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

                default:
                    // Check for EF Core extension expressions using reflection
                    var typeName = expression.GetType().Name;
                    if (typeName == "MaterializeCollectionNavigationExpression" ||
                        typeName == "CollectionResultExpression")
                    {
                        // Try to get the Subquery property
                        var subqueryProperty = expression.GetType().GetProperty("Subquery");
                        if (subqueryProperty != null)
                        {
                            var subquery = subqueryProperty.GetValue(expression) as Expression;
                            if (subquery != null)
                            {
                                CollectMethodCalls(subquery, methodCalls);
                            }
                        }
                    }
                    break;
            }
        }
    }
}
