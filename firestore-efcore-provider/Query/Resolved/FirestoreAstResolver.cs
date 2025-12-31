using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
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
    /// - Detect primary keys for Id optimization (GetDocumentAsync vs Query) using PrimaryKeyPropertyName from AST
    /// - Resolve Includes with navigation metadata from AST
    /// - Resolve Projections with subcollection info from AST
    ///
    /// After resolution, the Executor becomes "dumb" - it just builds SDK calls.
    /// All smart logic (expression evaluation, Id optimization, PK detection) is done here.
    /// </summary>
    public class FirestoreAstResolver : IFirestoreAstResolver
    {
        private readonly IFirestoreQueryContext _queryContext;

        public FirestoreAstResolver(IFirestoreQueryContext queryContext)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
        }

        /// <summary>
        /// Resolves the AST into a fully resolved query ready for execution.
        /// </summary>
        public ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast)
        {
            if (ast == null) throw new ArgumentNullException(nameof(ast));

            // Use CollectionName from AST directly
            var collectionPath = ast.CollectionName;

            // Detect Id optimization from FilterResults using PrimaryKeyPropertyName from AST
            string? documentId = DetectIdOptimization(ast.FilterResults, ast.PrimaryKeyPropertyName);

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

            // Resolve Includes using metadata from AST
            var includes = ResolveIncludes(ast.PendingIncludes);

            // Resolve Projection using metadata from AST
            ResolvedProjectionDefinition? projection = null;
            if (ast.Projection != null)
            {
                projection = ResolveProjection(ast.Projection);
            }

            return new ResolvedFirestoreQuery(
                CollectionPath: collectionPath,
                EntityClrType: ast.EntityType.ClrType,
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

        private IReadOnlyList<ResolvedInclude> ResolveIncludes(IReadOnlyList<IncludeInfo> includes)
        {
            var result = new List<ResolvedInclude>();

            foreach (var include in includes)
            {
                var resolved = ResolveInclude(include, includes);
                result.Add(resolved);
            }

            return result;
        }

        private ResolvedInclude ResolveInclude(IncludeInfo include, IReadOnlyList<IncludeInfo> allIncludes)
        {
            // Use metadata from AST directly
            var collectionPath = include.CollectionName;
            var targetClrType = include.TargetClrType;

            // Detect Id optimization from filters using PrimaryKeyPropertyName from AST
            string? documentId = DetectIdOptimization(include.FilterResults, include.PrimaryKeyPropertyName);

            // Resolve filters
            var filterResults = ResolveFilterResults(include.FilterResults);

            // Resolve OrderBy
            var orderByClauses = include.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(include.Pagination);

            // Note: Nested includes (ThenInclude) would need to be tracked separately
            // For now, we don't support nested includes in IncludeInfo
            var nestedIncludes = new List<ResolvedInclude>();

            return new ResolvedInclude(
                NavigationName: include.NavigationName,
                IsCollection: include.IsCollection,
                TargetEntityType: targetClrType,
                CollectionPath: collectionPath,
                DocumentId: documentId,
                FilterResults: filterResults,
                OrderByClauses: orderByClauses,
                Pagination: pagination,
                NestedIncludes: nestedIncludes);
        }

        #endregion

        #region Projection Resolution

        private ResolvedProjectionDefinition ResolveProjection(FirestoreProjectionDefinition projection)
        {
            var subcollections = projection.Subcollections
                .Select(ResolveSubcollectionProjection)
                .ToList();

            return new ResolvedProjectionDefinition(
                ResultType: projection.ResultType,
                ClrType: projection.ClrType,
                Fields: projection.Fields,
                Subcollections: subcollections);
        }

        private ResolvedSubcollectionProjection ResolveSubcollectionProjection(
            FirestoreSubcollectionProjection subcollection)
        {
            // Use metadata from AST directly
            var collectionPath = subcollection.CollectionName;
            var targetClrType = subcollection.TargetClrType;

            // Detect Id optimization using PrimaryKeyPropertyName from AST
            string? documentId = DetectIdOptimization(subcollection.FilterResults, subcollection.PrimaryKeyPropertyName);

            // Resolve filters
            var filterResults = ResolveFilterResults(subcollection.FilterResults);

            // Resolve OrderBy
            var orderByClauses = subcollection.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(subcollection.Pagination);

            // Resolve nested subcollections
            var nestedSubcollections = subcollection.NestedSubcollections
                .Select(ResolveSubcollectionProjection)
                .ToList();

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

        #endregion

        #region ID Optimization Detection

        /// <summary>
        /// Detects if the FilterResults contain a single PK == value filter,
        /// which allows using GetDocumentAsync instead of a query.
        /// </summary>
        private string? DetectIdOptimization(
            IReadOnlyList<FirestoreFilterResult> filterResults,
            string? primaryKeyPropertyName)
        {
            if (primaryKeyPropertyName == null)
                return null;

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

            if (clause.PropertyName != primaryKeyPropertyName)
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
