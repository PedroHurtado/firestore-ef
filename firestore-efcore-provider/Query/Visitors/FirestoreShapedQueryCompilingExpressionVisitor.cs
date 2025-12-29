using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Compiles shaped query expressions into executable Firestore queries.
    /// Creates FirestoreQueryingEnumerable instances that delegate execution to IFirestoreQueryExecutor.
    /// </summary>
    public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;
        private readonly IFirestoreQueryExecutor _queryExecutor;

        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext,
            IFirestoreQueryExecutor queryExecutor,
            IFirestoreCollectionManager collectionManager)
            : base(dependencies, queryCompilationContext)
        {
            // Direct cast - same pattern as Cosmos DB and other official providers
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));

            // collectionManager kept for API compatibility but no longer used here
            _ = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Copy ComplexType Includes from FirestoreQueryCompilationContext to FirestoreQueryExpression
            var complexTypeIncludes = _firestoreContext.ComplexTypeIncludes;
            if (complexTypeIncludes.Count > 0)
            {
                firestoreQueryExpression = firestoreQueryExpression.WithComplexTypeIncludes(
                    new List<LambdaExpression>(complexTypeIncludes));
            }

            // Handle aggregation queries differently
            if (firestoreQueryExpression.IsAggregation)
            {
                return CreateAggregationQueryExpression(firestoreQueryExpression);
            }

            var entityType = firestoreQueryExpression.EntityType.ClrType;

            // Determine if we should track entities
            var isTracking = QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;

            // FirestoreQueryingEnumerable delegates all execution, deserialization and navigation loading to Executor
            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(entityType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(DbContext),
                typeof(bool),
                typeof(IFirestoreQueryExecutor)
            })!;

            // Get DbContext from QueryContext.Context
            var dbContextExpression = Expression.Property(
                QueryCompilationContext.QueryContextParameter,
                nameof(QueryContext.Context));

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                dbContextExpression,
                Expression.Constant(isTracking),
                Expression.Constant(_queryExecutor));

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
                typeof(Type),
                typeof(IFirestoreQueryExecutor)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(entityType),
                Expression.Constant(_queryExecutor));

            return newExpression;
        }
    }
}