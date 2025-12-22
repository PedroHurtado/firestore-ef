using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Query;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;

        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            // Direct cast - same pattern as Cosmos DB and other official providers
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Copy ComplexType Includes from FirestoreQueryCompilationContext to FirestoreQueryExpression
            var complexTypeIncludes = _firestoreContext.ComplexTypeIncludes;
            if (complexTypeIncludes.Count > 0)
            {
                firestoreQueryExpression = firestoreQueryExpression.Update(
                    complexTypeIncludes: new List<LambdaExpression>(complexTypeIncludes));
            }

            // Copy Filtered Includes from FirestoreQueryCompilationContext to FirestoreQueryExpression
            var filteredIncludes = _firestoreContext.FilteredIncludes;
            if (filteredIncludes.Count > 0)
            {
                foreach (var kvp in filteredIncludes)
                {
                    var navigationName = kvp.Key;
                    var filterInfo = kvp.Value;

                    // Find corresponding include in PendingIncludesWithFilters
                    var existingInclude = firestoreQueryExpression.PendingIncludesWithFilters
                        .FirstOrDefault(i => i.EffectiveNavigationName == navigationName);

                    if (existingInclude != null)
                    {
                        existingInclude.FilterExpression = filterInfo.FilterExpression;
                        foreach (var orderBy in filterInfo.OrderByExpressions)
                            existingInclude.OrderByExpressions.Add(orderBy);
                        existingInclude.Take = filterInfo.Take;
                        existingInclude.Skip = filterInfo.Skip;
                    }
                    else
                    {
                        var nav = firestoreQueryExpression.PendingIncludes.FirstOrDefault(n => n.Name == navigationName);
                        if (nav != null)
                        {
                            var newIncludeInfo = new IncludeInfo(nav)
                            {
                                FilterExpression = filterInfo.FilterExpression,
                                Take = filterInfo.Take,
                                Skip = filterInfo.Skip
                            };
                            foreach (var orderBy in filterInfo.OrderByExpressions)
                                newIncludeInfo.OrderByExpressions.Add(orderBy);
                            firestoreQueryExpression.PendingIncludesWithFilters.Add(newIncludeInfo);
                        }
                        else
                        {
                            firestoreQueryExpression.PendingIncludesWithFilters.Add(filterInfo);
                        }
                    }
                }
            }

            // Handle aggregation queries differently
            if (firestoreQueryExpression.IsAggregation)
            {
                return CreateAggregationQueryExpression(firestoreQueryExpression);
            }

            // Handle projection queries with subcollections (load entity + includes, then project in memory)
            if (firestoreQueryExpression.HasSubcollectionProjection)
            {
                return CreateSubcollectionProjectionQueryExpression(firestoreQueryExpression);
            }

            // Handle simple projection queries (Select without subcollections)
            if (firestoreQueryExpression.HasProjection)
            {
                return CreateProjectionQueryExpression(firestoreQueryExpression);
            }

            var entityType = firestoreQueryExpression.EntityType.ClrType;

            // Determinar si debemos trackear las entidades
            var isTracking = QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");
            var isTrackingParameter = Expression.Parameter(typeof(bool), "isTracking");

            var shaperExpression = CreateShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                firestoreQueryExpression);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter);

            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(entityType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Func<,,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), typeof(bool), entityType),
                typeof(Type),
                typeof(bool)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType),
                Expression.Constant(isTracking));

            return newExpression;
        }

        /// <summary>
        /// Creates the expression for aggregation queries (Count, Sum, Average, Min, Max, Any).
        /// </summary>
        private Expression CreateAggregationQueryExpression(FirestoreQueryExpression firestoreQueryExpression)
        {
            var resultType = firestoreQueryExpression.AggregationResultType ?? typeof(int);
            var entityType = firestoreQueryExpression.EntityType.ClrType;

            var enumerableType = typeof(FirestoreAggregationQueryingEnumerable<>).MakeGenericType(resultType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Type)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(entityType));

            return newExpression;
        }

        /// <summary>
        /// Creates the expression for projection queries (Select).
        /// The shaper deserializes the entity and then applies the projection selector.
        /// </summary>
        private Expression CreateProjectionQueryExpression(FirestoreQueryExpression firestoreQueryExpression)
        {
            var entityType = firestoreQueryExpression.EntityType.ClrType;
            var projectionType = firestoreQueryExpression.ProjectionType!;
            var projectionSelector = firestoreQueryExpression.ProjectionSelector!;

            // Proyecciones no deben trackearse (son DTOs/tipos anónimos)
            var isTracking = false;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");
            var isTrackingParameter = Expression.Parameter(typeof(bool), "isTracking");

            // Crear el shaper que: 1) deserializa la entidad, 2) aplica la proyección
            var shaperExpression = CreateProjectionShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                firestoreQueryExpression,
                projectionSelector,
                projectionType);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter);

            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(projectionType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Func<,,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), typeof(bool), projectionType),
                typeof(Type),
                typeof(bool)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType),
                Expression.Constant(isTracking));

            return newExpression;
        }

        /// <summary>
        /// Creates the expression for projection queries that include subcollections.
        /// The entity is loaded with all its subcollections, then the projection is applied in memory.
        /// </summary>
        private Expression CreateSubcollectionProjectionQueryExpression(FirestoreQueryExpression firestoreQueryExpression)
        {
            var entityType = firestoreQueryExpression.EntityType.ClrType;
            var projectionType = firestoreQueryExpression.ProjectionType!;
            var projectionSelector = firestoreQueryExpression.ProjectionSelector!;

            // Proyecciones no deben trackearse (son DTOs/tipos anónimos)
            var isTracking = false;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");
            var isTrackingParameter = Expression.Parameter(typeof(bool), "isTracking");

            // Crear el shaper que: 1) deserializa la entidad con subcollections, 2) aplica la proyección
            var shaperExpression = CreateSubcollectionProjectionShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                firestoreQueryExpression,
                projectionSelector,
                projectionType);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter);

            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(projectionType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Func<,,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), typeof(bool), projectionType),
                typeof(Type),
                typeof(bool)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType),
                Expression.Constant(isTracking));

            return newExpression;
        }

        /// <summary>
        /// Creates a shaper expression for subcollection projections.
        /// The shaper deserializes the entity with its subcollections and applies the projection.
        /// </summary>
        private Expression CreateSubcollectionProjectionShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            ParameterExpression isTrackingParameter,
            FirestoreQueryExpression queryExpression,
            LambdaExpression projectionSelector,
            Type projectionType)
        {
            var entityType = queryExpression.EntityType.ClrType;

            // El selector contiene MaterializeCollectionNavigationExpression de EF Core
            // que no se puede compilar directamente. Necesitamos transformarlo para
            // reemplazar esas expresiones con accesos directos a las propiedades.
            var cleanedSelector = CleanProjectionSelector(projectionSelector, entityType);
            var compiledSelector = cleanedSelector.Compile();

            // Llamar al método genérico DeserializeWithIncludesAndProject<TEntity, TProjection>
            var deserializeAndProjectMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeWithIncludesAndProject), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType, projectionType);

            return Expression.Call(
                deserializeAndProjectMethod,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                Expression.Constant(queryExpression),
                Expression.Constant(compiledSelector));
        }

        /// <summary>
        /// Cleans a projection selector by replacing EF Core internal expressions
        /// (like MaterializeCollectionNavigationExpression) with direct property accesses.
        /// </summary>
        private LambdaExpression CleanProjectionSelector(LambdaExpression selector, Type entityType)
        {
            var parameter = selector.Parameters[0];

            // Create a new parameter of the correct entity type
            var newParameter = Expression.Parameter(entityType, parameter.Name);

            var cleaner = new ProjectionSelectorCleaner(parameter, newParameter);
            var cleanedBody = cleaner.Visit(selector.Body);

            return Expression.Lambda(cleanedBody, newParameter);
        }

        /// <summary>
        /// Visitor that replaces EF Core internal expressions with compilable expressions.
        /// Handles:
        /// 1. MaterializeCollectionNavigationExpression -> direct property access
        /// 2. EntityQueryRootExpression with correlated Where -> extract LINQ operations to apply on collection property
        /// </summary>
        private class ProjectionSelectorCleaner : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;
            private readonly Type _entityType;

            public ProjectionSelectorCleaner(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
                _entityType = newParameter.Type;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _oldParameter)
                {
                    return _newParameter;
                }
                return base.VisitParameter(node);
            }

            protected override Expression VisitNew(NewExpression node)
            {
                // Visit arguments - some may change type (e.g., List<Pedido> -> List<AnonymousType>)
                var visitedArguments = node.Arguments.Select(arg =>
                {
                    var visited = Visit(arg);
                    // Debug: if types differ, log
                    if (visited != null && arg != null && visited.Type != arg.Type)
                    {
                        System.Diagnostics.Debug.WriteLine($"Type changed: {arg.Type.Name} -> {visited.Type.Name}");
                    }
                    return visited;
                }).ToList();

                // Check if any argument types changed
                bool typesChanged = false;
                for (int i = 0; i < node.Arguments.Count; i++)
                {
                    if (visitedArguments[i].Type != node.Arguments[i].Type)
                    {
                        typesChanged = true;
                        break;
                    }
                }

                // If types didn't change, use normal Update
                if (!typesChanged)
                {
                    return node.Update(visitedArguments);
                }

                // Types changed - we need to create a new anonymous type with the correct member types
                // For anonymous types, we need to create a new expression with matching constructor
                if (node.Type.Name.StartsWith("<>f__AnonymousType"))
                {
                    // Find or create a constructor that matches the new argument types
                    var argumentTypes = visitedArguments.Select(a => a.Type).ToArray();

                    // For anonymous types, we can't change the type - instead we need to construct
                    // a new anonymous type. The simplest approach is to use reflection to find
                    // a compatible anonymous type or construct a new NewExpression with the right types.

                    // Since anonymous types are compiler-generated and we can't create new ones at runtime,
                    // we'll create a NewExpression for an existing anonymous type if possible,
                    // or fall back to creating a dynamic object.

                    // The actual anonymous type is determined by the projection selector's return type,
                    // which should already have the correct types. The issue is that the original
                    // NewExpression has member types based on EF Core's analysis, not the final types.

                    // For now, try to find a constructor that matches the visited argument types
                    var constructor = node.Type.GetConstructors()
                        .FirstOrDefault(c =>
                        {
                            var parameters = c.GetParameters();
                            if (parameters.Length != argumentTypes.Length) return false;
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                if (!parameters[i].ParameterType.IsAssignableFrom(argumentTypes[i]))
                                    return false;
                            }
                            return true;
                        });

                    if (constructor != null)
                    {
                        return Expression.New(constructor, visitedArguments, node.Members);
                    }

                    // If we can't find a matching constructor, the types are incompatible
                    // This happens when the subcollection projection changes the element type
                    // In this case, we need to use the projection type that EF Core determined
                    // The projection result type should be set correctly in the query expression
                }

                // Fall back to trying the update (may fail if types are truly incompatible)
                // Debug info before potential failure
                for (int i = 0; i < node.Arguments.Count; i++)
                {
                    var originalArg = node.Arguments[i];
                    var visitedArg = visitedArguments[i];
                    if (originalArg.Type != visitedArg?.Type)
                    {
                        // Throw with detailed info
                        var propsInfo = string.Join(", ", _entityType.GetProperties().Select(p => $"{p.Name}:{p.PropertyType.Name}"));
                        throw new InvalidOperationException(
                            $"Type mismatch at argument {i}. " +
                            $"Original: {originalArg.Type.FullName}, " +
                            $"Visited: {visitedArg?.Type.FullName ?? "null"}. " +
                            $"Original expression type: {originalArg.GetType().Name}. " +
                            $"_entityType: {_entityType.FullName}. " +
                            $"Properties: [{propsInfo}]. " +
                            $"Expression: {originalArg}");
                    }
                }
                return node.Update(visitedArguments!);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                // Check if this is a LINQ method call chain that ultimately comes from an EntityQueryRootExpression
                // Pattern: [EntityQueryRootExpression].Where(...).OrderBy(...).Take(...).ToList()
                if (node.Arguments.Count > 0)
                {
                    // First, check if this method operates on a subquery from a navigation
                    // Handle the case where the source is ALSO a method call (chained methods)
                    var navigationInfo = TryExtractNavigationFromSubquery(node);

                    if (navigationInfo != null)
                    {
                        // Build: entity.NavigationProperty.Method(...)
                        var collectionAccess = Expression.Property(_newParameter, navigationInfo.PropertyInfo!);

                        // Rebuild the ENTIRE method call chain on the collection property
                        return RebuildEntireMethodCallChain(node, collectionAccess, navigationInfo);
                    }

                    // Also check if this is a direct call on EntityQueryRootExpression
                    if (node.Arguments[0].GetType().Name == "EntityQueryRootExpression")
                    {
                        var entityTypeProperty = node.Arguments[0].GetType().GetProperty("EntityType");
                        if (entityTypeProperty != null)
                        {
                            var queryEntityType = entityTypeProperty.GetValue(node.Arguments[0]) as IEntityType;
                            if (queryEntityType != null)
                            {
                                // Find navigation for this entity type
                                foreach (var prop in _entityType.GetProperties())
                                {
                                    if (prop.PropertyType.IsGenericType)
                                    {
                                        var elementType = prop.PropertyType.GetGenericArguments().FirstOrDefault();
                                        if (elementType == queryEntityType.ClrType)
                                        {
                                            var model = queryEntityType.Model;
                                            var parentEntityType = model.FindEntityType(_entityType);
                                            if (parentEntityType != null)
                                            {
                                                var navigation = parentEntityType.FindNavigation(prop.Name);
                                                if (navigation != null && navigation.IsCollection)
                                                {
                                                    var collectionAccessDirect = Expression.Property(_newParameter, prop);
                                                    return RebuildEntireMethodCallChain(node, collectionAccessDirect, navigation);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return base.VisitMethodCall(node);
            }

            /// <summary>
            /// Rebuilds an entire method call chain, including nested calls, on a collection property.
            /// Handles patterns like: .Where().OrderBy().Take().ToList()
            /// </summary>
            private Expression RebuildEntireMethodCallChain(
                MethodCallExpression node,
                Expression collectionAccess,
                IReadOnlyNavigation navigation)
            {
                var elementType = navigation.TargetEntityType.ClrType;

                // Collect the entire method chain
                var methodChain = new List<MethodCallExpression>();
                Expression current = node;

                while (current is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
                {
                    methodChain.Insert(0, methodCall);

                    // Move to the source
                    var source = methodCall.Arguments[0];
                    var sourceTypeName = source.GetType().Name;

                    // Stop if we hit EntityQueryRootExpression or another type
                    if (sourceTypeName == "EntityQueryRootExpression")
                        break;

                    // Skip correlation filters (e.g., Where clauses that join to parent entity)
                    if (source is MethodCallExpression sourceMethod && IsCorrelationFilter(sourceMethod))
                    {
                        // Skip the correlation Where and continue to the source
                        if (sourceMethod.Arguments.Count > 0)
                        {
                            current = sourceMethod.Arguments[0];
                            continue;  // Continue iterating, don't break
                        }
                        break;
                    }

                    current = source;
                }

                // Now rebuild the chain starting from the collection access
                Expression result = collectionAccess;

                foreach (var method in methodChain)
                {
                    (result, elementType) = RebuildSingleMethodCall(method, result, elementType);
                }

                return result;
            }

            /// <summary>
            /// Tries to extract navigation info from a subquery source expression.
            /// Follows the entire method call chain to find EntityQueryRootExpression.
            /// </summary>
            private IReadOnlyNavigation? TryExtractNavigationFromSubquery(Expression source)
            {
                // Follow the method call chain until we find EntityQueryRootExpression
                Expression current = source;
                int depth = 0;

                while (current != null && depth < 50) // prevent infinite loop
                {
                    depth++;
                    var currentTypeName = current.GetType().Name;

                    // Handle method calls (Where, OrderBy, Take, etc.)
                    if (current is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
                    {
                        current = methodCall.Arguments[0];
                        continue;
                    }

                    // Check for EntityQueryRootExpression
                    if (currentTypeName == "EntityQueryRootExpression")
                    {
                        var entityTypeProperty = current.GetType().GetProperty("EntityType");
                        if (entityTypeProperty != null)
                        {
                            var queryEntityType = entityTypeProperty.GetValue(current) as IEntityType;
                            if (queryEntityType != null)
                            {
                                // Find the navigation that targets this entity type
                                foreach (var prop in _entityType.GetProperties())
                                {
                                    if (prop.PropertyType.IsGenericType)
                                    {
                                        var elementType = prop.PropertyType.GetGenericArguments().FirstOrDefault();
                                        if (elementType == queryEntityType.ClrType)
                                        {
                                            var model = queryEntityType.Model;
                                            var parentEntityType = model.FindEntityType(_entityType);
                                            if (parentEntityType != null)
                                            {
                                                var navigation = parentEntityType.FindNavigation(prop.Name);
                                                if (navigation != null && navigation.IsCollection)
                                                {
                                                    return navigation;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        return null;
                    }

                    // Unknown expression type - stop looking
                    break;
                }

                return null;
            }

            /// <summary>
            /// Rebuilds a single method call using Enumerable methods.
            /// Returns the new expression and the (potentially changed) element type.
            /// </summary>
            private (Expression result, Type newElementType) RebuildSingleMethodCall(MethodCallExpression method, Expression source, Type elementType)
            {
                var methodName = method.Method.Name;

                // Skip correlation Where (the one with Property(c, "Id") pattern)
                if (methodName == "Where" && IsCorrelationFilter(method))
                {
                    return (source, elementType);
                }

                if (methodName == "Where" && method.Arguments.Count >= 2)
                {
                    var predicateArg = method.Arguments[1];
                    var cleanedPredicate = CleanLambdaExpression(predicateArg, elementType);

                    var whereMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Where" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                        .MakeGenericMethod(elementType);

                    return (Expression.Call(whereMethod, source, cleanedPredicate), elementType);
                }

                if ((methodName == "OrderBy" || methodName == "OrderByDescending") && method.Arguments.Count >= 2)
                {
                    var keySelectorArg = method.Arguments[1];
                    var cleanedKeySelector = CleanLambdaExpression(keySelectorArg, elementType);

                    var orderMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, cleanedKeySelector.ReturnType);

                    return (Expression.Call(orderMethod, source, cleanedKeySelector), elementType);
                }

                if (methodName == "Take" && method.Arguments.Count >= 2)
                {
                    var countArg = Visit(method.Arguments[1]);

                    var takeMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Take" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType == typeof(int))
                        .MakeGenericMethod(elementType);

                    return (Expression.Call(takeMethod, source, countArg), elementType);
                }

                if (methodName == "Select" && method.Arguments.Count >= 2)
                {
                    var selectorArg = method.Arguments[1];
                    var cleanedSelector = CleanLambdaExpression(selectorArg, elementType);

                    // Select changes the element type to the return type of the selector
                    var newElementType = cleanedSelector.ReturnType;

                    var selectMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, newElementType);

                    return (Expression.Call(selectMethod, source, cleanedSelector), newElementType);
                }

                if (methodName == "Count")
                {
                    var countMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Count" && m.GetParameters().Length == 1)
                        .MakeGenericMethod(elementType);

                    // Count returns int, not a collection
                    return (Expression.Call(countMethod, source), typeof(int));
                }

                if (methodName == "ToList")
                {
                    var toListMethod = typeof(Enumerable)
                        .GetMethod("ToList")!
                        .MakeGenericMethod(elementType);

                    return (Expression.Call(toListMethod, source), elementType);
                }

                // For unknown methods, just return source
                return (source, elementType);
            }

            /// <summary>
            /// Rebuilds a LINQ method call chain to operate on a collection property.
            /// Skips the correlated Where and keeps other operations.
            /// </summary>
            private Expression RebuildMethodCallOnCollection(
                MethodCallExpression node,
                Expression collectionAccess,
                IReadOnlyNavigation navigation)
            {
                var methodName = node.Method.Name;
                var elementType = navigation.TargetEntityType.ClrType;

                // For Select, Where, OrderBy, etc. - rebuild using Enumerable methods
                // Skip the correlation Where if present
                if (methodName == "Select" && node.Arguments.Count >= 2)
                {
                    // Get the actual source - might have a Where for correlation
                    var sourceExpr = RebuildSourceExpression(node.Arguments[0], collectionAccess, elementType);

                    // Get the selector lambda
                    var selectorArg = node.Arguments[1];
                    var cleanedSelector = CleanLambdaExpression(selectorArg, elementType);

                    // Build Enumerable.Select call
                    var selectMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, cleanedSelector.ReturnType);

                    return Expression.Call(selectMethod, sourceExpr, cleanedSelector);
                }

                if (methodName == "Where" && node.Arguments.Count >= 2)
                {
                    var sourceExpr = RebuildSourceExpression(node.Arguments[0], collectionAccess, elementType);
                    var predicateArg = node.Arguments[1];
                    var cleanedPredicate = CleanLambdaExpression(predicateArg, elementType);

                    var whereMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Where" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                        .MakeGenericMethod(elementType);

                    return Expression.Call(whereMethod, sourceExpr, cleanedPredicate);
                }

                if ((methodName == "OrderBy" || methodName == "OrderByDescending") && node.Arguments.Count >= 2)
                {
                    var sourceExpr = RebuildSourceExpression(node.Arguments[0], collectionAccess, elementType);
                    var keySelectorArg = node.Arguments[1];
                    var cleanedKeySelector = CleanLambdaExpression(keySelectorArg, elementType);

                    var orderMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, cleanedKeySelector.ReturnType);

                    return Expression.Call(orderMethod, sourceExpr, cleanedKeySelector);
                }

                if (methodName == "Take" && node.Arguments.Count >= 2)
                {
                    var sourceExpr = RebuildSourceExpression(node.Arguments[0], collectionAccess, elementType);
                    var countArg = Visit(node.Arguments[1]);

                    var takeMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Take" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType == typeof(int))
                        .MakeGenericMethod(elementType);

                    return Expression.Call(takeMethod, sourceExpr, countArg);
                }

                if (methodName == "Count" && node.Arguments.Count >= 1)
                {
                    var sourceExpr = RebuildSourceExpression(node.Arguments[0], collectionAccess, elementType);

                    var countMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Count" && m.GetParameters().Length == 1)
                        .MakeGenericMethod(elementType);

                    return Expression.Call(countMethod, sourceExpr);
                }

                // Fallback: just return the collection access
                return collectionAccess;
            }

            /// <summary>
            /// Recursively rebuilds the source expression, handling nested method calls.
            /// </summary>
            private Expression RebuildSourceExpression(Expression source, Expression collectionAccess, Type elementType)
            {
                // If source is EntityQueryRootExpression, return the collection access
                if (source.GetType().Name == "EntityQueryRootExpression")
                {
                    return collectionAccess;
                }

                // If source is a method call (e.g., Where for correlation), rebuild it
                if (source is MethodCallExpression methodCall)
                {
                    var methodName = methodCall.Method.Name;

                    // Skip correlation Where (the one with Property(c, "Id") pattern)
                    if (methodName == "Where" && IsCorrelationFilter(methodCall))
                    {
                        // Return just the collection without the correlation filter
                        return RebuildSourceExpression(methodCall.Arguments[0], collectionAccess, elementType);
                    }

                    // For other method calls, rebuild them
                    var innerSource = RebuildSourceExpression(methodCall.Arguments[0], collectionAccess, elementType);
                    return RebuildMethodCallOnSource(methodCall, innerSource, elementType);
                }

                return collectionAccess;
            }

            /// <summary>
            /// Checks if a Where call is a correlation filter (joins to parent entity).
            /// </summary>
            private bool IsCorrelationFilter(MethodCallExpression methodCall)
            {
                // First check that this is actually a Where method
                if (methodCall.Method.Name != "Where")
                    return false;

                if (methodCall.Arguments.Count < 2)
                    return false;

                var predicate = methodCall.Arguments[1];
                var exprString = predicate.ToString();

                // Check if predicate references parent entity properties via Property() calls
                return exprString.Contains("Property(c,") || exprString.Contains("Property(c.") ||
                       (exprString.Contains("Property(") && exprString.Contains("\"Id\""));
            }

            /// <summary>
            /// Rebuilds a method call on a new source expression.
            /// </summary>
            private Expression RebuildMethodCallOnSource(MethodCallExpression original, Expression newSource, Type elementType)
            {
                var methodName = original.Method.Name;

                if (methodName == "Where" && original.Arguments.Count >= 2)
                {
                    var predicateArg = original.Arguments[1];
                    var cleanedPredicate = CleanLambdaExpression(predicateArg, elementType);

                    var whereMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Where" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                        .MakeGenericMethod(elementType);

                    return Expression.Call(whereMethod, newSource, cleanedPredicate);
                }

                if ((methodName == "OrderBy" || methodName == "OrderByDescending") && original.Arguments.Count >= 2)
                {
                    var keySelectorArg = original.Arguments[1];
                    var cleanedKeySelector = CleanLambdaExpression(keySelectorArg, elementType);

                    var orderMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, cleanedKeySelector.ReturnType);

                    return Expression.Call(orderMethod, newSource, cleanedKeySelector);
                }

                if (methodName == "Take" && original.Arguments.Count >= 2)
                {
                    var countArg = Visit(original.Arguments[1]);

                    var takeMethod = typeof(Enumerable)
                        .GetMethods()
                        .First(m => m.Name == "Take" && m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType == typeof(int))
                        .MakeGenericMethod(elementType);

                    return Expression.Call(takeMethod, newSource, countArg);
                }

                return newSource;
            }

            /// <summary>
            /// Cleans a lambda expression argument, extracting from Quote if necessary.
            /// Also handles nested navigation accesses within the lambda body.
            /// </summary>
            private LambdaExpression CleanLambdaExpression(Expression arg, Type elementType)
            {
                // Unwrap Quote
                if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Quote)
                {
                    arg = unary.Operand;
                }

                if (arg is LambdaExpression lambda)
                {
                    // Create new parameter for the element type
                    var newParam = Expression.Parameter(elementType, lambda.Parameters[0].Name);

                    // First replace parameter references
                    var paramReplacer = new ParameterReplacerVisitor(lambda.Parameters[0], newParam);
                    var bodyWithNewParam = paramReplacer.Visit(lambda.Body);

                    // Then clean any nested navigation accesses (like p.Lineas.Count())
                    var nestedCleaner = new NestedNavigationCleaner(newParam, elementType);
                    var cleanedBody = nestedCleaner.Visit(bodyWithNewParam);

                    return Expression.Lambda(cleanedBody, newParam);
                }

                throw new InvalidOperationException($"Expected lambda expression but got {arg.GetType().Name}");
            }

            /// <summary>
            /// Visitor that cleans nested navigation accesses within lambda bodies.
            /// Handles patterns like p.Lineas.Count() where Lineas is a navigation on Pedido.
            /// </summary>
            private class NestedNavigationCleaner : ExpressionVisitor
            {
                private readonly ParameterExpression _parameter;
                private readonly Type _entityType;

                public NestedNavigationCleaner(ParameterExpression parameter, Type entityType)
                {
                    _parameter = parameter;
                    _entityType = entityType;
                }

                protected override Expression VisitMethodCall(MethodCallExpression node)
                {
                    // Check if this is a Count() on an EntityQueryRootExpression
                    if (node.Method.Name == "Count" && node.Arguments.Count >= 1)
                    {
                        var navigationAccess = TryExtractNestedNavigationAccess(node.Arguments[0]);
                        if (navigationAccess != null)
                        {
                            // Build: parameter.NavigationProperty.Count()
                            var countMethod = typeof(Enumerable)
                                .GetMethods()
                                .First(m => m.Name == "Count" && m.GetParameters().Length == 1)
                                .MakeGenericMethod(navigationAccess.ElementType);

                            return Expression.Call(countMethod, navigationAccess.PropertyAccess);
                        }
                    }

                    return base.VisitMethodCall(node);
                }

                /// <summary>
                /// Tries to extract a navigation property access from an EntityQueryRootExpression.
                /// </summary>
                private NavigationAccessInfo? TryExtractNestedNavigationAccess(Expression source)
                {
                    // Follow method call chain to find EntityQueryRootExpression
                    Expression current = source;
                    while (current != null)
                    {
                        if (current is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
                        {
                            current = methodCall.Arguments[0];
                            continue;
                        }

                        if (current.GetType().Name == "EntityQueryRootExpression")
                        {
                            var entityTypeProperty = current.GetType().GetProperty("EntityType");
                            if (entityTypeProperty != null)
                            {
                                var queryEntityType = entityTypeProperty.GetValue(current) as IEntityType;
                                if (queryEntityType != null)
                                {
                                    // Find navigation property on the entity type that targets this type
                                    foreach (var prop in _entityType.GetProperties())
                                    {
                                        if (prop.PropertyType.IsGenericType)
                                        {
                                            var elementType = prop.PropertyType.GetGenericArguments().FirstOrDefault();
                                            if (elementType == queryEntityType.ClrType)
                                            {
                                                return new NavigationAccessInfo
                                                {
                                                    PropertyAccess = Expression.Property(_parameter, prop),
                                                    ElementType = elementType
                                                };
                                            }
                                        }
                                    }
                                }
                            }
                            return null;
                        }

                        break;
                    }

                    return null;
                }

                private class NavigationAccessInfo
                {
                    public Expression PropertyAccess { get; set; } = null!;
                    public Type ElementType { get; set; } = null!;
                }
            }

            protected override Expression VisitExtension(Expression node)
            {
                // Replace MaterializeCollectionNavigationExpression with direct property access
                if (node.GetType().Name == "MaterializeCollectionNavigationExpression")
                {
                    var navigationProperty = node.GetType().GetProperty("Navigation");
                    if (navigationProperty != null)
                    {
                        var navigation = navigationProperty.GetValue(node) as IReadOnlyNavigation;
                        if (navigation?.PropertyInfo != null)
                        {
                            return Expression.Property(_newParameter, navigation.PropertyInfo);
                        }
                    }
                }

                return base.VisitExtension(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression == _oldParameter)
                {
                    return Expression.MakeMemberAccess(_newParameter, node.Member);
                }

                return base.VisitMember(node);
            }
        }

        /// <summary>
        /// Simple visitor to replace parameters in expressions.
        /// </summary>
        private class ParameterReplacerVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParam;
            private readonly ParameterExpression _newParam;

            public ParameterReplacerVisitor(ParameterExpression oldParam, ParameterExpression newParam)
            {
                _oldParam = oldParam;
                _newParam = newParam;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParam ? _newParam : base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Deserializes an entity with its subcollections and applies the projection selector.
        /// </summary>
        private static TProjection DeserializeWithIncludesAndProject<TEntity, TProjection>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            bool isTracking,
            FirestoreQueryExpression queryExpression,
            Delegate projectionSelector) where TEntity : class, new()
        {
            // Deserializar la entidad completa con sus subcollections
            var entity = DeserializeEntity<TEntity>(queryContext, documentSnapshot, false, queryExpression);

            // Aplicar la proyección
            var selector = (Func<TEntity, TProjection>)projectionSelector;
            return selector(entity);
        }

        /// <summary>
        /// Creates a shaper expression that deserializes the entity and applies the projection.
        /// </summary>
        private Expression CreateProjectionShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            ParameterExpression isTrackingParameter,
            FirestoreQueryExpression queryExpression,
            LambdaExpression projectionSelector,
            Type projectionType)
        {
            var entityType = queryExpression.EntityType.ClrType;

            // Llamar al método genérico DeserializeAndProject<TEntity, TProjection>
            var deserializeAndProjectMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeAndProject), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType, projectionType);

            return Expression.Call(
                deserializeAndProjectMethod,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                Expression.Constant(queryExpression),
                Expression.Constant(projectionSelector.Compile()));
        }

        /// <summary>
        /// Deserializes an entity from a DocumentSnapshot and applies the projection selector.
        /// </summary>
        private static TProjection DeserializeAndProject<TEntity, TProjection>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            bool isTracking,
            FirestoreQueryExpression queryExpression,
            Delegate projectionSelector) where TEntity : class, new()
        {
            // Deserializar la entidad (sin tracking ya que es para proyección)
            var entity = DeserializeEntity<TEntity>(queryContext, documentSnapshot, false, queryExpression);

            // Aplicar la proyección
            var selector = (Func<TEntity, TProjection>)projectionSelector;
            return selector(entity);
        }

        #region Shaper Creation

        private Expression CreateShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            ParameterExpression isTrackingParameter,
            FirestoreQueryExpression queryExpression)
        {
            var entityType = queryExpression.EntityType.ClrType;
            var deserializeMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeEntity), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType);

            return Expression.Call(
                deserializeMethod,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                Expression.Constant(queryExpression));
        }

        private static T DeserializeEntity<T>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            bool isTracking,
            FirestoreQueryExpression queryExpression) where T : class
        {
            var dbContext = queryContext.Context;
            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;

            var model = dbContext.Model;

            // Identity Resolution: verificar si la entidad ya está trackeada antes de deserializar
            if (isTracking)
            {
                var existingEntity = TryGetTrackedEntity<T>(dbContext, documentSnapshot.Id);
                if (existingEntity != null)
                {
                    return existingEntity;
                }
            }

            var typeMappingSource = (ITypeMappingSource)serviceProvider.GetService(typeof(ITypeMappingSource))!;
            var collectionManager = (IFirestoreCollectionManager)serviceProvider.GetService(typeof(IFirestoreCollectionManager))!;
            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory))!;
            var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;

            var deserializerLogger = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<Storage.FirestoreDocumentDeserializer>(loggerFactory);
            var deserializer = new Storage.FirestoreDocumentDeserializer(
                model,
                typeMappingSource,
                collectionManager,
                deserializerLogger);

            // El deserializer se encarga de crear la entidad (con proxy si lazy loading está habilitado)
            var entity = deserializer.DeserializeEntity<T>(documentSnapshot, dbContext, serviceProvider);

            // Cargar includes de navegaciones normales
            if (queryExpression.PendingIncludes.Count > 0)
            {
                LoadIncludes(entity, documentSnapshot, queryExpression.PendingIncludes, queryExpression.PendingIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext)
                    .GetAwaiter().GetResult();
            }

            // Cargar includes en ComplexTypes (ej: .Include(e => e.DireccionPrincipal.SucursalCercana))
            if (queryExpression.ComplexTypeIncludes.Count > 0)
            {
                LoadComplexTypeIncludes(entity, documentSnapshot, queryExpression.ComplexTypeIncludes, clientWrapper, deserializer, model, isTracking, dbContext)
                    .GetAwaiter().GetResult();
            }

            // Adjuntar al ChangeTracker como Unchanged para habilitar tracking de cambios
            // Solo si QueryTrackingBehavior es TrackAll (no NoTracking)
            if (isTracking)
            {
                dbContext.Attach(entity);

                // Establecer shadow FK properties para navegaciones con DocumentReference
                SetShadowForeignKeys(entity, documentSnapshot, model.FindEntityType(typeof(T))!, dbContext);
            }

            return entity;
        }

        /// <summary>
        /// Sets shadow foreign key properties for navigations that use DocumentReference.
        /// This enables EF Core's Explicit Loading and Lazy Loading to work.
        /// </summary>
        private static void SetShadowForeignKeys(
            object entity,
            DocumentSnapshot documentSnapshot,
            IEntityType entityType,
            DbContext dbContext)
        {
            var data = documentSnapshot.ToDictionary();
            var entry = dbContext.Entry(entity);

            foreach (var navigation in entityType.GetNavigations())
            {
                // Skip collections
                if (navigation.IsCollection)
                    continue;

                // Skip if it's a subcollection
                if (navigation.IsSubCollection())
                    continue;

                // Get the DocumentReference from data
                if (!data.TryGetValue(navigation.Name, out var value))
                    continue;

                if (value is not Google.Cloud.Firestore.DocumentReference docRef)
                    continue;

                // Find the FK property for this navigation
                var foreignKey = navigation.ForeignKey;
                foreach (var fkProperty in foreignKey.Properties)
                {
                    // If it's a shadow property, set it via the entry
                    if (fkProperty.IsShadowProperty())
                    {
                        // Extract the ID from the DocumentReference
                        var referencedId = docRef.Id;

                        // Convert to the FK property type if needed
                        var convertedValue = ConvertKeyValue(referencedId, fkProperty);
                        entry.Property(fkProperty.Name).CurrentValue = convertedValue;
                    }
                }
            }
        }

        /// <summary>
        /// Identity Resolution: busca si la entidad ya está siendo trackeada usando IStateManager.
        /// Usa O(1) lookup por clave primaria.
        /// </summary>
        private static T? TryGetTrackedEntity<T>(DbContext dbContext, string documentId) where T : class
        {
            var entityType = dbContext.Model.FindEntityType(typeof(T));
            if (entityType == null) return null;

            var key = entityType.FindPrimaryKey();
            if (key == null) return null;

            if (key.Properties.Count == 0) return null;
            var keyProperty = key.Properties[0];

            // Convertir el ID del documento al tipo de la PK
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            // Usar IStateManager para lookup O(1)
            var stateManager = dbContext.GetService<IStateManager>();
            var entry = stateManager.TryGetEntry(key, keyValues);

            return entry?.Entity as T;
        }

        /// <summary>
        /// Convierte el ID de Firestore (siempre string) al tipo de la clave primaria.
        /// Soporta ValueConverters configurados en el modelo.
        /// </summary>
        private static object ConvertKeyValue(string firestoreId, IReadOnlyProperty keyProperty)
        {
            var targetType = keyProperty.ClrType;

            // Usar ValueConverter si está configurado
            var converter = keyProperty.GetValueConverter();
            if (converter != null)
            {
                return converter.ConvertFromProvider(firestoreId)!;
            }

            // Conversión estándar por tipo
            if (targetType == typeof(string)) return firestoreId;
            if (targetType == typeof(int)) return int.Parse(firestoreId);
            if (targetType == typeof(long)) return long.Parse(firestoreId);
            if (targetType == typeof(Guid)) return Guid.Parse(firestoreId);

            return Convert.ChangeType(firestoreId, targetType);
        }

        /// <summary>
        /// Identity Resolution para entidades incluidas (versión no genérica).
        /// Busca si la entidad ya está siendo trackeada usando IStateManager.
        /// </summary>
        private static object? TryGetTrackedEntity(DbContext dbContext, IReadOnlyEntityType entityType, string documentId)
        {
            var key = entityType.FindPrimaryKey();
            if (key == null) return null;

            if (key.Properties.Count == 0) return null;
            var keyProperty = key.Properties[0];

            // Convertir el ID del documento al tipo de la PK
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            // Usar IStateManager para lookup O(1)
            var stateManager = dbContext.GetService<IStateManager>();
            var entry = stateManager.TryGetEntry((IKey)key, keyValues);

            return entry?.Entity;
        }

        #endregion

        #region Include Loading

        private static async Task LoadIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<IReadOnlyNavigation> allIncludes,
            List<IncludeInfo> allIncludesWithFilters,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext) where T : class
        {
            var rootNavigations = allIncludes
                .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
                .ToList();

            var tasks = rootNavigations.Select(navigation =>
                LoadNavigationAsync(entity, documentSnapshot, navigation, allIncludes, allIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext));

            await Task.WhenAll(tasks);
        }

        private static async Task LoadNavigationAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            List<IncludeInfo> allIncludesWithFilters,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext)
        {
            if (navigation.IsCollection)
            {
                await LoadSubCollectionAsync(entity, documentSnapshot, navigation, allIncludes, allIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext);
            }
            else
            {
                await LoadReferenceAsync(entity, documentSnapshot, navigation, allIncludes, allIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext);
            }
        }

        private static async Task LoadSubCollectionAsync(
            object parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            List<IncludeInfo> allIncludesWithFilters,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext)
        {
            if (!navigation.IsSubCollection())
                return;

            var subCollectionName = GetSubCollectionName(navigation);

            // Ciclo 9: Usar el wrapper en lugar de llamada directa al SDK
            var snapshot = await clientWrapper.GetSubCollectionAsync(parentDoc.Reference, subCollectionName);

            // Usar el Deserializer para crear la colección del tipo correcto (List<T>, HashSet<T>, etc.)
            var collection = deserializer.CreateEmptyCollection(navigation);

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethods()
                .First(m => m.Name == nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity) && m.GetParameters().Length == 1)
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            // Buscar IncludeInfo para esta navegación (para Filtered Includes)
            var includeInfo = allIncludesWithFilters.FirstOrDefault(i =>
                i.EffectiveNavigationName == navigation.Name);

            // Compilar el filtro si existe
            Func<object, bool>? filterPredicate = null;
            if (includeInfo?.FilterExpression != null)
            {
                filterPredicate = CompileFilterPredicate(includeInfo.FilterExpression, navigation.TargetEntityType.ClrType, queryContext);
                if (filterPredicate == null)
                {
                    throw new Exception($"Failed to compile filter for {navigation.Name}");
                }
            }

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists)
                    continue;

                // Identity Resolution: verificar si la entidad ya está trackeada
                object? childEntity = null;
                if (isTracking)
                {
                    childEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, doc.Id);
                }

                // Si no está trackeada, deserializar
                if (childEntity == null)
                {
                    childEntity = deserializeMethod.Invoke(deserializer, new object[] { doc });
                    if (childEntity == null)
                        continue;

                    var childIncludes = allIncludes
                        .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                        .ToList();

                    if (childIncludes.Count > 0)
                    {
                        var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                            .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                        await (Task)loadIncludesMethod.Invoke(null, new object[]
                        {
                            childEntity, doc, allIncludes, allIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext
                        })!;
                    }

                    // Adjuntar al ChangeTracker como Unchanged
                    if (isTracking)
                    {
                        dbContext.Attach(childEntity);
                    }
                }

                // Aplicar filtro si existe (Filtered Include)
                if (filterPredicate != null && !filterPredicate(childEntity))
                {
                    continue; // No incluir esta entidad si no pasa el filtro
                }

                ApplyFixup(parentEntity, childEntity, navigation);

                deserializer.AddToCollection(collection, childEntity);
            }

            navigation.PropertyInfo?.SetValue(parentEntity, collection);
        }

        private static async Task LoadReferenceAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            List<IncludeInfo> allIncludesWithFilters,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext)
        {
            var data = documentSnapshot.ToDictionary();

            object? referenceValue = null;

            if (data.TryGetValue(navigation.Name, out var directValue))
            {
                referenceValue = directValue;
            }
            else if (data.TryGetValue($"{navigation.Name}Id", out var idValue))
            {
                referenceValue = idValue;
            }

            if (referenceValue == null)
                return;

            // Obtener el ID de la referencia para identity resolution
            string? referencedId = null;
            DocumentSnapshot? referencedDoc = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;

                // Identity Resolution: verificar si la entidad ya está trackeada
                if (isTracking)
                {
                    var existingEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                // Ciclo 10: Usar wrapper en lugar de llamada directa al SDK
                referencedDoc = await clientWrapper.GetDocumentByReferenceAsync(docRef);
            }
            else if (referenceValue is string id)
            {
                referencedId = id;

                // Identity Resolution: verificar si la entidad ya está trackeada
                if (isTracking)
                {
                    var existingEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                var targetEntityType = model.FindEntityType(navigation.TargetEntityType.ClrType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    // Ciclo 10: Usar wrapper en lugar de llamada directa al SDK
                    referencedDoc = await clientWrapper.GetDocumentByReferenceAsync(docRefFromId);
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
                return;

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethods()
                .First(m => m.Name == nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity) && m.GetParameters().Length == 1)
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            var referencedEntity = deserializeMethod.Invoke(deserializer, new object[] { referencedDoc });

            if (referencedEntity != null)
            {
                var childIncludes = allIncludes
                    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        referencedEntity, referencedDoc, allIncludes, allIncludesWithFilters, clientWrapper, deserializer, model, isTracking, dbContext, queryContext
                    })!;
                }

                // Adjuntar al ChangeTracker como Unchanged
                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                ApplyFixup(entity, referencedEntity, navigation);

                navigation.PropertyInfo?.SetValue(entity, referencedEntity);
            }
        }

        private static void ApplyFixup(
            object parent,
            object child,
            IReadOnlyNavigation navigation)
        {
            if (navigation.Inverse != null)
            {
                var inverseProperty = navigation.Inverse.PropertyInfo;
                if (inverseProperty != null)
                {
                    if (navigation.IsCollection)
                    {
                        inverseProperty.SetValue(child, parent);
                    }
                    else
                    {
                        if (navigation.Inverse.IsCollection)
                        {
                            var collection = inverseProperty.GetValue(parent) as System.Collections.IList;
                            if (collection != null && !collection.Contains(child))
                            {
                                collection.Add(child);
                            }
                        }
                        else
                        {
                            inverseProperty.SetValue(parent, child);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads references inside ComplexTypes based on extracted Include expressions.
        /// Example: .Include(e => e.DireccionPrincipal.SucursalCercana)
        /// </summary>
        private static async Task LoadComplexTypeIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<LambdaExpression> complexTypeIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext) where T : class
        {
            var data = documentSnapshot.ToDictionary();

            foreach (var includeExpr in complexTypeIncludes)
            {
                await LoadComplexTypeInclude(entity, data, includeExpr, clientWrapper, deserializer, model, isTracking, dbContext);
            }
        }

        /// <summary>
        /// Loads a single reference inside a ComplexType.
        /// Parses the expression to get: ComplexTypeProperty.ReferenceProperty
        /// </summary>
        private static async Task LoadComplexTypeInclude(
            object entity,
            Dictionary<string, object> data,
            LambdaExpression includeExpr,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext)
        {
            // Parse the expression: e => e.DireccionPrincipal.SucursalCercana
            // We need to extract: ComplexTypeProp = DireccionPrincipal, ReferenceProp = SucursalCercana
            if (includeExpr.Body is not MemberExpression refMemberExpr)
                return;

            var referenceProperty = refMemberExpr.Member as PropertyInfo;
            if (referenceProperty == null)
                return;

            if (refMemberExpr.Expression is not MemberExpression complexTypeMemberExpr)
                return;

            var complexTypeProperty = complexTypeMemberExpr.Member as PropertyInfo;
            if (complexTypeProperty == null)
                return;

            // Get the ComplexType instance from the entity
            var complexTypeInstance = complexTypeProperty.GetValue(entity);
            if (complexTypeInstance == null)
                return;

            // Get the raw data for the ComplexType from the document
            if (!data.TryGetValue(complexTypeProperty.Name, out var complexTypeData) ||
                complexTypeData is not Dictionary<string, object> complexTypeDict)
                return;

            // Get the DocumentReference from the ComplexType data
            if (!complexTypeDict.TryGetValue(referenceProperty.Name, out var referenceValue))
                return;

            if (referenceValue == null)
                return;

            // Load the referenced entity
            DocumentSnapshot? referencedDoc = null;
            string? referencedId = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;
                // Ciclo 10: Usar wrapper en lugar de llamada directa al SDK
                referencedDoc = await clientWrapper.GetDocumentByReferenceAsync(docRef);
            }
            else if (referenceValue is string id)
            {
                referencedId = id;
                var targetType = referenceProperty.PropertyType;
                var targetEntityType = model.FindEntityType(targetType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    // Ciclo 10: Usar wrapper en lugar de llamada directa al SDK
                    referencedDoc = await clientWrapper.GetDocumentByReferenceAsync(docRefFromId);
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
                return;

            // Identity Resolution
            if (isTracking && referencedId != null)
            {
                var targetEntityType = model.FindEntityType(referenceProperty.PropertyType);
                if (targetEntityType != null)
                {
                    var existingEntity = TryGetTrackedEntity(dbContext, targetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        referenceProperty.SetValue(complexTypeInstance, existingEntity);
                        return;
                    }
                }
            }

            // Deserialize the referenced entity
            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethods()
                .First(m => m.Name == nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity) && m.GetParameters().Length == 1)
                .MakeGenericMethod(referenceProperty.PropertyType);

            var referencedEntity = deserializeMethod.Invoke(deserializer, new object[] { referencedDoc });

            if (referencedEntity != null)
            {
                // Track the referenced entity
                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                // Set the reference property on the ComplexType instance
                referenceProperty.SetValue(complexTypeInstance, referencedEntity);
            }
        }

        #endregion

        #region Helper Methods

        private static string GetSubCollectionName(IReadOnlyNavigation navigation)
        {
            var childEntityType = navigation.ForeignKey.DeclaringEntityType;

            // Pluralizar el nombre del tipo de entidad
            return Pluralize(childEntityType.ClrType.Name);
        }

        private static string GetCollectionNameForEntityType(IEntityType entityType)
        {
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            return Pluralize(entityType.ClrType.Name);
        }

        private static string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                name.Length > 1 &&
                !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }

            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char c)
        {
            c = char.ToLowerInvariant(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        #endregion

        #region Filtered Include Support

        /// <summary>
        /// Compiles a filter expression into a predicate function.
        /// Handles both closure evaluation and EF Core parameter resolution from QueryContext.
        /// </summary>
        private static Func<object, bool>? CompileFilterPredicate(LambdaExpression filterExpression, Type entityType, QueryContext queryContext)
        {
            try
            {
                // EF Core reescribe las variables capturadas como ParameterExpression con nombres como __variableName_N
                // Estos valores están en QueryContext.ParameterValues
                var parameterResolver = new EfCoreParameterResolvingVisitor(queryContext.ParameterValues);
                var resolvedBody = parameterResolver.Visit(filterExpression.Body);

                // También evaluar closures tradicionales (MemberExpression sobre ConstantExpression)
                var evaluator = new ClosureEvaluatingVisitor();
                var evaluatedBody = evaluator.Visit(resolvedBody);

                var newLambda = Expression.Lambda(evaluatedBody, filterExpression.Parameters);

                // Compilar la expresión con las constantes evaluadas
                var compiledDelegate = newLambda.Compile();

                // Crear un wrapper que llama al delegado compilado
                return (obj) =>
                {
                    try
                    {
                        return (bool)compiledDelegate.DynamicInvoke(obj)!;
                    }
                    catch
                    {
                        return false;
                    }
                };
            }
            catch (Exception ex)
            {
                // Show the actual error
                throw new Exception($"CompileFilterPredicate failed: {ex.Message}, Filter: {filterExpression}", ex);
            }
        }

        /// <summary>
        /// Visitor that resolves EF Core parameter expressions (e.g., __variableName_0)
        /// using values from QueryContext.ParameterValues.
        /// </summary>
        private class EfCoreParameterResolvingVisitor : ExpressionVisitor
        {
            private readonly IReadOnlyDictionary<string, object?> _parameterValues;

            public EfCoreParameterResolvingVisitor(IReadOnlyDictionary<string, object?> parameterValues)
            {
                _parameterValues = parameterValues;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                // Check if this is an EF Core parameter (starts with __)
                if (node.Name != null && node.Name.StartsWith("__"))
                {
                    if (_parameterValues.TryGetValue(node.Name, out var value))
                    {
                        return Expression.Constant(value, node.Type);
                    }
                }

                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Visitor that evaluates closure references (captured variables) to their constant values.
        /// </summary>
        private class ClosureEvaluatingVisitor : ExpressionVisitor
        {
            protected override Expression VisitMember(MemberExpression node)
            {
                // Check if this is a closure reference (accessing a field on a constant object)
                if (node.Expression is ConstantExpression constantExpr && constantExpr.Value != null)
                {
                    try
                    {
                        var value = GetMemberValue(node.Member, constantExpr.Value);
                        return Expression.Constant(value, node.Type);
                    }
                    catch
                    {
                        return base.VisitMember(node);
                    }
                }

                // Nested member access - evaluate the inner expression first
                if (node.Expression != null && !(node.Expression is ParameterExpression))
                {
                    var visitedExpr = Visit(node.Expression);
                    if (visitedExpr is ConstantExpression constExpr && constExpr.Value != null)
                    {
                        try
                        {
                            var value = GetMemberValue(node.Member, constExpr.Value);
                            return Expression.Constant(value, node.Type);
                        }
                        catch
                        {
                            return base.VisitMember(node);
                        }
                    }
                }

                return base.VisitMember(node);
            }

            private static object? GetMemberValue(System.Reflection.MemberInfo member, object instance)
            {
                return member switch
                {
                    System.Reflection.FieldInfo field => field.GetValue(instance),
                    System.Reflection.PropertyInfo prop => prop.GetValue(instance),
                    _ => throw new NotSupportedException($"Unsupported member type: {member.GetType()}")
                };
            }
        }

        #endregion
    }
}
