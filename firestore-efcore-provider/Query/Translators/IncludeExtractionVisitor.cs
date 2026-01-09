using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Visitor especializado para extraer TODOS los includes del árbol de expresiones,
    /// incluyendo filtros para Filtered Includes.
    /// Uses ExpressionVisitor pattern to properly traverse the entire tree.
    /// </summary>
    internal class IncludeExtractionVisitor : ExpressionVisitor
    {
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly FirestoreWhereTranslator _whereTranslator = new();
        private readonly FirestoreOrderByTranslator _orderByTranslator = new();
        private readonly FirestoreLimitTranslator _limitTranslator = new();

        // Stack to track the current navigation context for ThenInclude detection
        // When visiting NavigationExpression of an include, we push the target type
        // so nested includes know their parent
        private readonly Stack<System.Type> _navigationContextStack = new();

        public List<IReadOnlyNavigation> DetectedNavigations { get; } = new();

        /// <summary>
        /// Includes con información de filtros extraída.
        /// </summary>
        public List<IncludeInfo> DetectedIncludes { get; } = new();

        public IncludeExtractionVisitor(IFirestoreCollectionManager collectionManager)
        {
            _collectionManager = collectionManager;
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                if (includeExpression.Navigation is IReadOnlyNavigation navigation)
                {
                    DetectedNavigations.Add(navigation);

                    // Obtener información completa de la navegación
                    var targetEntityType = navigation.TargetEntityType;
                    var targetClrType = targetEntityType.ClrType;
                    var collectionName = _collectionManager.GetCollectionName(targetClrType);

                    // Get primary key property name from EF Core metadata
                    var pkProperties = targetEntityType.FindPrimaryKey()?.Properties;
                    var primaryKeyPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;

                    // For ThenInclude chains, the parent type is tracked via the context stack
                    // When we're inside a NavigationExpression of a parent include, the stack
                    // contains the target type of that parent navigation
                    // Example: For .Include(c => c.Pedidos).ThenInclude(p => p.Lineas)
                    //   - "Pedidos" is visited first, stack is empty -> ParentClrType = null
                    //   - We push Pedido to stack and visit NavigationExpression
                    //   - "Lineas" is found inside NavigationExpression, stack has Pedido -> ParentClrType = Pedido
                    System.Type? parentClrType = _navigationContextStack.Count > 0
                        ? _navigationContextStack.Peek()
                        : null;

                    // Crear IncludeInfo con información completa
                    var includeInfo = new IncludeInfo(
                        navigation.Name,
                        navigation.IsCollection,
                        collectionName,
                        targetClrType,
                        primaryKeyPropertyName: primaryKeyPropertyName,
                        parentClrType: parentClrType);

                    // Extraer operaciones del NavigationExpression
                    ExtractOperationsFromNavigationExpression(includeExpression.NavigationExpression, includeInfo);

                    DetectedIncludes.Add(includeInfo);

                    // Push this navigation's target type before visiting NavigationExpression
                    // This way, any ThenInclude inside will know their parent
                    _navigationContextStack.Push(targetClrType);
                    try
                    {
                        // Visitar NavigationExpression - contains ThenIncludes for this navigation
                        Visit(includeExpression.NavigationExpression);
                    }
                    finally
                    {
                        _navigationContextStack.Pop();
                    }

                    // Visitar EntityExpression - this goes up the chain to parent includes
                    Visit(includeExpression.EntityExpression);
                }
                else
                {
                    // Visit children even if no navigation
                    Visit(includeExpression.EntityExpression);
                    Visit(includeExpression.NavigationExpression);
                }

                return node;
            }

            return base.VisitExtension(node);
        }

        /// <summary>
        /// Extrae operaciones (Where, OrderBy, Take, Skip) del NavigationExpression.
        /// Usa los translators existentes para mantener consistencia.
        /// </summary>
        private void ExtractOperationsFromNavigationExpression(Expression navigationExpression, IncludeInfo includeInfo)
        {
            // Extraer method calls del NavigationExpression
            var methodCalls = ExtractMethodCalls(navigationExpression);
            if (methodCalls.Count == 0)
                return;

            // Procesar cada method call
            foreach (var methodCall in methodCalls)
            {
                ProcessMethodCall(methodCall, includeInfo);
            }
        }

        /// <summary>
        /// Extracts method calls from NavigationExpression.
        /// </summary>
        private List<MethodCallExpression> ExtractMethodCalls(Expression expression)
        {
            var collector = new MethodCallCollector();
            collector.Visit(expression);
            return collector.MethodCalls;
        }
        

        /// <summary>
        /// Processes a single method call using the appropriate translator.
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

            var filterResult = _whereTranslator.Translate(predicateLambda.Body);
            if (filterResult != null)
            {
                // Store the filter result for later processing
                includeInfo.AddFilterResult(filterResult);

                if (filterResult.IsOrGroup && filterResult.OrGroup != null)
                {
                    includeInfo.AddOrFilterGroup(filterResult.OrGroup);
                }

                includeInfo.AddFilters(filterResult.AndClauses);

                foreach (var orGroup in filterResult.NestedOrGroups)
                {
                    includeInfo.AddOrFilterGroup(orGroup);
                }
            }
        }

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

        private static LambdaExpression? ExtractLambda(Expression expression)
        {
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Quote)
            {
                return unary.Operand as LambdaExpression;
            }
            return expression as LambdaExpression;
        }

        private static bool IsCorrelationFilter(LambdaExpression predicate)
        {
            var exprString = predicate.Body.ToString();
            return exprString.Contains("Property(");
        }

        private class MethodCallCollector : ExpressionVisitor
        {
            public List<MethodCallExpression> MethodCalls { get; } = new();

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                MethodCalls.Add(node);
                return base.VisitMethodCall(node);
            }
            protected override Expression VisitExtension(Expression node)
            {
                // No atravesar IncludeExpression anidados - esos se manejan por separado
                if (node is IncludeExpression)
                {
                    return node; // No visitar children
                }
                return base.VisitExtension(node);
            }
        }
    }
}
