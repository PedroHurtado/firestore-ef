using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Compiles shaped query expressions into executable Firestore queries.
    /// Creates FirestorePipelineQueryingEnumerable instances that execute through the pipeline.
    /// </summary>
    public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
    {
        private readonly IQueryPipelineMediator _mediator;

        private static readonly MethodInfo _createPipelineContextMethod =
            typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(CreatePipelineContext), BindingFlags.NonPublic | BindingFlags.Static)!;

        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext,
            IQueryPipelineMediator mediator)
            : base(dependencies, queryCompilationContext)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Add ComplexType Includes to the AST
            FirestoreQueryExpression.AddComplexTypeIncludes(firestoreQueryExpression, QueryCompilationContext);

            // Add ArrayOf Reference Includes to the AST
            FirestoreQueryExpression.AddArrayOfIncludes(firestoreQueryExpression, QueryCompilationContext);

            // Determine result type and query kind
            Type resultType;
            QueryKind queryKind;
            Type? entityType;
            bool isTracking;

            if (firestoreQueryExpression.IsAggregation)
            {
                resultType = firestoreQueryExpression.AggregationResultType ?? typeof(int);
                queryKind = QueryKind.Aggregation;
                entityType = firestoreQueryExpression.EntityType.ClrType;
                isTracking = false; // Aggregations don't track
            }
            else if (firestoreQueryExpression.HasProjection)
            {
                // Projection: use the projected type, not entity type
                resultType = firestoreQueryExpression.Projection!.ClrType;
                queryKind = QueryKind.Projection;
                entityType = firestoreQueryExpression.EntityType.ClrType;
                isTracking = false; // Projections don't track (anonymous types, DTOs)
            }
            else
            {
                resultType = firestoreQueryExpression.EntityType.ClrType;
                queryKind = QueryKind.Entity;
                entityType = resultType;
                isTracking = QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;
            }

            // Create FirestorePipelineQueryingEnumerable<T>
            var enumerableType = typeof(FirestorePipelineQueryingEnumerable<>).MakeGenericType(resultType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(IQueryPipelineMediator),
                typeof(PipelineContext)
            })!;

            // Build PipelineContext at runtime via helper method
            var createContextMethod = _createPipelineContextMethod.MakeGenericMethod(resultType);
            var contextExpression = Expression.Call(
                createContextMethod,
                Expression.Constant(firestoreQueryExpression),
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(isTracking),
                Expression.Constant(queryKind),
                Expression.Constant(entityType, typeof(Type)));

            var newExpression = Expression.New(
                constructor,
                Expression.Constant(_mediator),
                contextExpression);

            return newExpression;
        }

        /// <summary>
        /// Creates a PipelineContext at runtime from query parameters.
        /// Called via reflection from the compiled expression.
        /// </summary>
        private static PipelineContext CreatePipelineContext<T>(
            FirestoreQueryExpression ast,
            QueryContext queryContext,
            bool isTracking,
            QueryKind kind,
            Type? entityType)
        {
            return new PipelineContext
            {
                Ast = ast,
                QueryContext = (IFirestoreQueryContext)queryContext,
                IsTracking = isTracking,
                ResultType = typeof(T),
                Kind = kind,
                EntityType = entityType
            };
        }
    }
}