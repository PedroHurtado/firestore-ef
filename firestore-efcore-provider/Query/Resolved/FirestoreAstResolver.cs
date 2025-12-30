using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Resolved
{
    /// <summary>
    /// Resolves a FirestoreQueryExpression (AST) into a ResolvedFirestoreQuery.
    ///
    /// Responsibilities:
    /// - Evaluate all Expressions using IFirestoreQueryContext.ParameterValues
    /// - Resolve navigations using IFirestoreQueryContext.Model
    /// - Detect primary keys for Id optimization (GetDocumentAsync vs Query)
    /// - Resolve Includes with navigation metadata
    /// - Resolve Projections with subcollection info
    ///
    /// After resolution, the Executor becomes "dumb" - it just builds SDK calls.
    /// All smart logic (expression evaluation, Id optimization, PK detection) is done here.
    /// </summary>
    public class FirestoreAstResolver : IFirestoreAstResolver
    {
        private readonly IFirestoreQueryContext _queryContext;
        private readonly IFirestoreCollectionManager _collectionManager;

        public FirestoreAstResolver(
            IFirestoreQueryContext queryContext,
            IFirestoreCollectionManager collectionManager)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
        }

        /// <summary>
        /// Resolves the AST into a fully resolved query ready for execution.
        /// </summary>
        public ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast)
        {
            if (ast == null) throw new ArgumentNullException(nameof(ast));

            var entityType = ast.EntityType;
            var collectionPath = _collectionManager.GetCollectionName(entityType.ClrType);

            // Resolve DocumentId from IdValueExpression (Id optimization)
            string? documentId = null;
            if (ast.IdValueExpression != null)
            {
                documentId = EvaluateExpression<string>(ast.IdValueExpression);
            }

            // Resolve filters
            var filterResults = ResolveFilterResults(ast.FilterResults);

            // Resolve OrderBy (already pure, but map to resolved types)
            var orderByClauses = ast.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(ast.Pagination);

            // Resolve Cursor (already pure)
            ResolvedCursor? cursor = null;
            if (ast.StartAfterCursor != null)
            {
                cursor = new ResolvedCursor(
                    ast.StartAfterCursor.DocumentId,
                    ast.StartAfterCursor.OrderByValues);
            }

            // Resolve Includes using IModel for navigation resolution
            var includes = ResolveIncludes(ast.PendingIncludes, entityType);

            // Resolve Projection
            ResolvedProjectionDefinition? projection = null;
            if (ast.Projection != null)
            {
                projection = ResolveProjection(ast.Projection, entityType);
            }

            return new ResolvedFirestoreQuery(
                CollectionPath: collectionPath,
                EntityClrType: entityType.ClrType,
                DocumentId: documentId,
                FilterResults: filterResults,
                OrderByClauses: orderByClauses,
                Pagination: pagination,
                StartAfterCursor: cursor,
                Includes: includes,
                AggregationType: ast.AggregationType,
                AggregationPropertyName: ast.AggregationPropertyName,
                AggregationResultType: ast.AggregationResultType,
                Projection: projection,
                ReturnDefault: ast.ReturnDefault,
                ReturnType: ast.ReturnType);
        }

        #region Filter Resolution

        private IReadOnlyList<ResolvedFilterResult> ResolveFilterResults(
            IReadOnlyList<FirestoreFilterResult> filterResults)
        {
            return filterResults.Select(ResolveFilterResult).ToList();
        }

        private ResolvedFilterResult ResolveFilterResult(FirestoreFilterResult filterResult)
        {
            var andClauses = filterResult.AndClauses
                .Select(ResolveWhereClause)
                .ToList();

            ResolvedOrFilterGroup? orGroup = null;
            if (filterResult.OrGroup != null)
            {
                orGroup = ResolveOrFilterGroup(filterResult.OrGroup);
            }

            IReadOnlyList<ResolvedOrFilterGroup>? nestedOrGroups = null;
            if (filterResult.NestedOrGroups.Count > 0)
            {
                nestedOrGroups = filterResult.NestedOrGroups
                    .Select(ResolveOrFilterGroup)
                    .ToList();
            }

            return new ResolvedFilterResult(andClauses, orGroup, nestedOrGroups);
        }

        private ResolvedOrFilterGroup ResolveOrFilterGroup(FirestoreOrFilterGroup orGroup)
        {
            var clauses = orGroup.Clauses
                .Select(ResolveWhereClause)
                .ToList();
            return new ResolvedOrFilterGroup(clauses);
        }

        private ResolvedWhereClause ResolveWhereClause(FirestoreWhereClause clause)
        {
            var value = EvaluateWhereClauseValue(clause);
            return new ResolvedWhereClause(
                clause.PropertyName,
                clause.Operator,
                value,
                clause.EnumType);
        }

        private object? EvaluateWhereClauseValue(FirestoreWhereClause clause)
        {
            var expression = clause.ValueExpression;

            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            try
            {
                var replacer = new QueryContextParameterReplacer(_queryContext);
                var replacedExpression = replacer.Visit(expression);

                var lambda = Expression.Lambda<Func<object>>(
                    Expression.Convert(replacedExpression, typeof(object)));

                var compiled = lambda.Compile();
                return compiled();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Pagination Resolution

        private ResolvedPaginationInfo ResolvePagination(FirestorePaginationInfo pagination)
        {
            int? limit = pagination.Limit;
            if (limit == null && pagination.LimitExpression != null)
            {
                limit = EvaluateExpression<int>(pagination.LimitExpression);
            }

            int? limitToLast = pagination.LimitToLast;
            if (limitToLast == null && pagination.LimitToLastExpression != null)
            {
                limitToLast = EvaluateExpression<int>(pagination.LimitToLastExpression);
            }

            int? skip = pagination.Skip;
            if (skip == null && pagination.SkipExpression != null)
            {
                skip = EvaluateExpression<int>(pagination.SkipExpression);
            }

            return new ResolvedPaginationInfo(limit, limitToLast, skip);
        }

        #endregion

        #region Include Resolution

        private IReadOnlyList<ResolvedInclude> ResolveIncludes(
            IReadOnlyList<IncludeInfo> includes,
            IEntityType parentEntityType)
        {
            var result = new List<ResolvedInclude>();

            foreach (var include in includes)
            {
                var resolved = ResolveInclude(include, parentEntityType, includes);
                if (resolved != null)
                {
                    result.Add(resolved);
                }
            }

            return result;
        }

        private ResolvedInclude? ResolveInclude(
            IncludeInfo include,
            IEntityType parentEntityType,
            IReadOnlyList<IncludeInfo> allIncludes)
        {
            var navigation = parentEntityType.FindNavigation(include.NavigationName);
            if (navigation == null)
            {
                return null;
            }

            var targetEntityType = navigation.TargetEntityType;

            // Get collection name for subcollection (simple name, Executor builds full path at runtime)
            var collectionPath = _collectionManager.GetCollectionName(targetEntityType.ClrType);

            // Detect Id optimization from filters
            string? documentId = DetectIdOptimization(include.FilterResults, targetEntityType);

            // Resolve filters
            var filterResults = ResolveFilterResults(include.FilterResults);

            // Resolve OrderBy
            var orderByClauses = include.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(include.Pagination);

            // Resolve nested includes (ThenInclude)
            var nestedIncludes = allIncludes
                .Where(inc => targetEntityType.FindNavigation(inc.NavigationName) != null &&
                              inc.NavigationName != include.NavigationName)
                .Select(inc => ResolveInclude(inc, targetEntityType, allIncludes))
                .Where(inc => inc != null)
                .Cast<ResolvedInclude>()
                .ToList();

            return new ResolvedInclude(
                NavigationName: include.NavigationName,
                IsCollection: include.IsCollection,
                TargetEntityType: targetEntityType.ClrType,
                CollectionPath: collectionPath,
                DocumentId: documentId,
                FilterResults: filterResults,
                OrderByClauses: orderByClauses,
                Pagination: pagination,
                NestedIncludes: nestedIncludes);
        }

        private string? DetectIdOptimization(
            IReadOnlyList<FirestoreFilterResult> filterResults,
            IEntityType entityType)
        {
            if (filterResults.Count != 1)
                return null;

            var filterResult = filterResults[0];

            if (filterResult.OrGroup != null || filterResult.NestedOrGroups.Count > 0)
                return null;

            if (filterResult.AndClauses.Count != 1)
                return null;

            var clause = filterResult.AndClauses[0];

            if (clause.Operator != FirestoreOperator.EqualTo)
                return null;

            var pkPropertyName = GetPrimaryKeyPropertyName(entityType);
            if (pkPropertyName == null)
                return null;

            if (clause.PropertyName != pkPropertyName)
                return null;

            var value = EvaluateWhereClauseValue(clause);
            return value?.ToString();
        }

        private string? GetPrimaryKeyPropertyName(IEntityType entityType)
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null || primaryKey.Properties.Count != 1)
                return null;

            return primaryKey.Properties[0].Name;
        }

        #endregion

        #region Projection Resolution

        private ResolvedProjectionDefinition ResolveProjection(
            FirestoreProjectionDefinition projection,
            IEntityType entityType)
        {
            var subcollections = projection.Subcollections
                .Select(s => ResolveSubcollectionProjection(s, entityType))
                .ToList();

            return new ResolvedProjectionDefinition(
                ResultType: projection.ResultType,
                ClrType: projection.ClrType,
                Fields: projection.Fields,
                Subcollections: subcollections);
        }

        private ResolvedSubcollectionProjection ResolveSubcollectionProjection(
            FirestoreSubcollectionProjection subcollection,
            IEntityType parentEntityType)
        {
            var navigation = parentEntityType.FindNavigation(subcollection.NavigationName);
            var targetEntityType = navigation?.TargetEntityType;
            var targetClrType = targetEntityType?.ClrType ?? typeof(object);

            // Get collection name (simple name, Executor builds full path at runtime)
            string collectionPath = targetEntityType != null
                ? _collectionManager.GetCollectionName(targetEntityType.ClrType)
                : subcollection.CollectionName;

            // Detect Id optimization
            string? documentId = null;
            if (targetEntityType != null)
            {
                documentId = DetectIdOptimizationFromClauses(subcollection.FilterResults, targetEntityType);
            }

            // Resolve filters
            var filterResults = ResolveFilterResults(subcollection.FilterResults);

            // Resolve OrderBy
            var orderByClauses = subcollection.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(subcollection.Pagination);

            // Resolve nested subcollections
            var nestedSubcollections = new List<ResolvedSubcollectionProjection>();
            if (subcollection.NestedSubcollections.Count > 0 && targetEntityType != null)
            {
                nestedSubcollections = subcollection.NestedSubcollections
                    .Select(ns => ResolveSubcollectionProjection(ns, targetEntityType))
                    .ToList();
            }

            return new ResolvedSubcollectionProjection(
                NavigationName: subcollection.NavigationName,
                ResultName: subcollection.ResultName,
                TargetEntityType: targetClrType,
                CollectionPath: collectionPath,
                DocumentId: documentId,
                FilterResults: filterResults,
                OrderByClauses: orderByClauses,
                Pagination: pagination,
                Fields: subcollection.Fields,
                Aggregation: subcollection.Aggregation,
                AggregationPropertyName: subcollection.AggregationPropertyName,
                NestedSubcollections: nestedSubcollections);
        }

        private string? DetectIdOptimizationFromClauses(
            List<FirestoreFilterResult> filterResults,
            IEntityType entityType)
        {
            if (filterResults.Count != 1)
                return null;

            var filterResult = filterResults[0];

            if (filterResult.OrGroup != null || filterResult.NestedOrGroups.Count > 0)
                return null;

            if (filterResult.AndClauses.Count != 1)
                return null;

            var clause = filterResult.AndClauses[0];

            if (clause.Operator != FirestoreOperator.EqualTo)
                return null;

            var pkPropertyName = GetPrimaryKeyPropertyName(entityType);
            if (pkPropertyName == null)
                return null;

            if (clause.PropertyName != pkPropertyName)
                return null;

            var value = EvaluateWhereClauseValue(clause);
            return value?.ToString();
        }

        #endregion

        #region Expression Evaluation

        private T? EvaluateExpression<T>(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return (T?)constant.Value;
            }

            try
            {
                var replacer = new QueryContextParameterReplacer(_queryContext);
                var replacedExpression = replacer.Visit(expression);

                var lambda = Expression.Lambda<Func<T>>(
                    Expression.Convert(replacedExpression, typeof(T)));

                var compiled = lambda.Compile();
                return compiled();
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Visitor that replaces QueryContext parameter references with the actual values.
        /// </summary>
        private class QueryContextParameterReplacer : ExpressionVisitor
        {
            private readonly IFirestoreQueryContext _queryContext;

            public QueryContextParameterReplacer(IFirestoreQueryContext queryContext)
            {
                _queryContext = queryContext;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Name != null && _queryContext.ParameterValues.TryGetValue(node.Name, out var parameterValue))
                {
                    return Expression.Constant(parameterValue, node.Type);
                }

                return base.VisitParameter(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                // Handle closure captures (field access on constant objects)
                if (node.Expression is ConstantExpression constantExpr && constantExpr.Value != null)
                {
                    var member = node.Member;
                    object? value = member switch
                    {
                        System.Reflection.FieldInfo field => field.GetValue(constantExpr.Value),
                        System.Reflection.PropertyInfo prop => prop.GetValue(constantExpr.Value),
                        _ => null
                    };

                    if (value != null)
                    {
                        return Expression.Constant(value, node.Type);
                    }
                }

                return base.VisitMember(node);
            }
        }

        #endregion
    }
}
