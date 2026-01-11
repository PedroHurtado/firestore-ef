using Firestore.EntityFrameworkCore.Extensions;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Query.Resolved
{
    /// <summary>
    /// Resolves a FirestoreQueryExpression (AST) into a ResolvedFirestoreQuery.
    ///
    /// Responsibilities:
    /// - Orchestrate resolution of AST components
    /// - Convert CLR values to Firestore types using IFirestoreValueConverter
    /// - Detect primary keys for Id optimization (GetDocumentAsync vs Query)
    /// - Resolve Includes, Projections, and Pagination
    ///
    /// Delegates expression evaluation to IExpressionEvaluator.
    /// After resolution, the Executor becomes "dumb" - it just builds SDK calls.
    /// Registered as Singleton - context is passed per-request via Resolve method.
    /// </summary>
    public class FirestoreAstResolver : IFirestoreAstResolver
    {
        private readonly IFirestoreValueConverter _valueConverter;
        private readonly IExpressionEvaluator _expressionEvaluator;

        public FirestoreAstResolver(IFirestoreValueConverter valueConverter, IExpressionEvaluator expressionEvaluator)
        {
            _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
            _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
        }

        public ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast, IFirestoreQueryContext queryContext)
        {
            if (ast == null) throw new ArgumentNullException(nameof(ast));
            if (queryContext == null) throw new ArgumentNullException(nameof(queryContext));

            var collectionPath = ast.CollectionName;

            // Detect Id optimization:
            // 1. First check IsIdOnlyQuery/IdValueExpression (used by FindAsync)
            // 2. Then check FilterResults with PrimaryKeyPropertyName
            // NOTE: Skip optimization if there's a projection with specific fields,
            // because GetDocumentAsync returns ALL fields and defeats the purpose of projections.
            string? documentId = null;
            bool hasProjectionWithFields = ast.Projection?.Fields != null && ast.Projection.Fields.Count > 0;

            if (hasProjectionWithFields)
            {
                documentId = null;
            }
            else if (ast.IsIdOnlyQuery && ast.IdValueExpression != null)
            {
                documentId = _expressionEvaluator.EvaluateIdExpression(ast.IdValueExpression, queryContext);
            }
            else
            {
                documentId = DetectIdOptimization(ast.FilterResults, ast.PrimaryKeyPropertyName, queryContext);
            }

            // When DocumentId is set, don't include query operations - they're not used in GetDocument
            // The ExecutionHandler will use GetDocumentAsync directly with the DocumentId
            IReadOnlyList<ResolvedFilterResult> filterResults;
            IReadOnlyList<ResolvedOrderByClause> orderByClauses;
            ResolvedPaginationInfo pagination;
            ResolvedCursor? cursor = null;

            if (documentId != null)
            {
                // GetDocument mode - clear all query operations
                filterResults = Array.Empty<ResolvedFilterResult>();
                orderByClauses = Array.Empty<ResolvedOrderByClause>();
                pagination = ResolvedPaginationInfo.None;
            }
            else
            {
                // Query mode - resolve all operations
                filterResults = ResolveFilterResults(ast.FilterResults, queryContext, ast.EntityType);

                orderByClauses = ast.OrderByClauses
                    .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                    .ToList();

                pagination = ResolvePagination(ast.Pagination, queryContext);

                if (ast.StartAfterCursor != null)
                {
                    cursor = new ResolvedCursor(
                        ast.StartAfterCursor.DocumentId,
                        ast.StartAfterCursor.OrderByValues);
                }
            }

            // Resolve Includes
            var includes = ResolveIncludes(ast.PendingIncludes, queryContext);

            // Resolve Projection
            ResolvedProjectionDefinition? projection = null;
            if (ast.Projection != null)
            {
                projection = ResolveProjection(ast.Projection, queryContext);
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
            IReadOnlyList<FirestoreFilterResult> filterResults,
            IFirestoreQueryContext queryContext,
            IEntityType? entityType = null)
        {
            return filterResults.Select(fr => ResolveFilterResult(fr, queryContext, entityType)).ToList();
        }

        private ResolvedFilterResult ResolveFilterResult(
            FirestoreFilterResult filterResult,
            IFirestoreQueryContext queryContext,
            IEntityType? entityType = null)
        {
            var andClauses = filterResult.AndClauses
                .Select(c => ResolveWhereClause(c, queryContext, entityType))
                .ToList();

            ResolvedOrFilterGroup? orGroup = null;
            if (filterResult.OrGroup != null)
            {
                orGroup = ResolveOrFilterGroup(filterResult.OrGroup, queryContext, entityType);
            }

            IReadOnlyList<ResolvedOrFilterGroup>? nestedOrGroups = null;
            if (filterResult.NestedOrGroups.Count > 0)
            {
                nestedOrGroups = filterResult.NestedOrGroups
                    .Select(g => ResolveOrFilterGroup(g, queryContext, entityType))
                    .ToList();
            }

            return new ResolvedFilterResult(andClauses, orGroup, nestedOrGroups);
        }

        private ResolvedOrFilterGroup ResolveOrFilterGroup(
            FirestoreOrFilterGroup orGroup,
            IFirestoreQueryContext queryContext,
            IEntityType? entityType = null)
        {
            var clauses = orGroup.Clauses
                .Select(c => ResolveWhereClause(c, queryContext, entityType))
                .ToList();
            return new ResolvedOrFilterGroup(clauses);
        }

        private ResolvedWhereClause ResolveWhereClause(
            FirestoreWhereClause clause,
            IFirestoreQueryContext queryContext,
            IEntityType? entityType = null)
        {
            var value = _expressionEvaluator.EvaluateWhereClause(clause, queryContext);

            // Validate null filter - requires PersistNullValues configured
            if (value == null && entityType != null)
            {
                ValidateNullFilter(clause.PropertyName, entityType);
            }

            // Convert CLR value to Firestore-compatible type
            var convertedValue = _valueConverter.ToFirestore(value, clause.EnumType);

            // Determine if this clause filters by primary key
            var isPrimaryKey = false;
            if (entityType != null)
            {
                var pkProperties = entityType.FindPrimaryKey()?.Properties;
                var pkPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;
                isPrimaryKey = pkPropertyName != null && clause.PropertyName == pkPropertyName;
            }

            return new ResolvedWhereClause(
                clause.PropertyName,
                clause.Operator,
                convertedValue,
                isPrimaryKey);
        }

        private static void ValidateNullFilter(string propertyName, IEntityType entityType)
        {
            var pkProperties = entityType.FindPrimaryKey()?.Properties;
            var pkPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;
            if (pkPropertyName != null && propertyName == pkPropertyName)
                return;

            var propertyPath = propertyName.Split('.');
            var property = entityType.FindProperty(propertyPath[0]);

            if (property == null)
                return;

            if (!property.IsPersistNullValuesEnabled())
            {
                throw new NotSupportedException(
                    $"Filtering by null on property '{propertyName}' is not supported. " +
                    "Firestore does not store null values by default. " +
                    "Configure the property with .PersistNullValues() in OnModelCreating if you need this functionality.");
            }
        }

        #endregion

        #region Pagination Resolution

        private ResolvedPaginationInfo ResolvePagination(
            FirestorePaginationInfo pagination,
            IFirestoreQueryContext queryContext)
        {
            int? limit = pagination.Limit;
            if (limit == null && pagination.LimitExpression != null)
            {
                limit = _expressionEvaluator.Evaluate<int>(pagination.LimitExpression, queryContext);
            }

            int? limitToLast = pagination.LimitToLast;
            if (limitToLast == null && pagination.LimitToLastExpression != null)
            {
                limitToLast = _expressionEvaluator.Evaluate<int>(pagination.LimitToLastExpression, queryContext);
            }

            int? skip = pagination.Skip;
            if (skip == null && pagination.SkipExpression != null)
            {
                skip = _expressionEvaluator.Evaluate<int>(pagination.SkipExpression, queryContext);
            }

            return new ResolvedPaginationInfo(limit, limitToLast, skip);
        }

        #endregion

        #region Include Resolution

        private IReadOnlyList<ResolvedInclude> ResolveIncludes(
            IReadOnlyList<IncludeInfo> includes,
            IFirestoreQueryContext queryContext)
        {
            return BuildIncludeHierarchy(includes, queryContext, parentType: null);
        }

        private IReadOnlyList<ResolvedInclude> BuildIncludeHierarchy(
            IReadOnlyList<IncludeInfo> allIncludes,
            IFirestoreQueryContext queryContext,
            Type? parentType)
        {
            var result = new List<ResolvedInclude>();

            var levelIncludes = allIncludes
                .Where(i => i.ParentClrType == parentType)
                .ToList();

            foreach (var include in levelIncludes)
            {
                var nestedIncludes = BuildIncludeHierarchy(
                    allIncludes,
                    queryContext,
                    parentType: include.TargetClrType);

                var resolved = ResolveInclude(include, queryContext, nestedIncludes);
                result.Add(resolved);
            }

            return result;
        }

        private ResolvedInclude ResolveInclude(
            IncludeInfo include,
            IFirestoreQueryContext queryContext,
            IReadOnlyList<ResolvedInclude> nestedIncludes)
        {
            var collectionPath = include.CollectionName;
            var targetClrType = include.TargetClrType;
            var targetEntityType = queryContext.Model?.FindEntityType(targetClrType);

            // Detect Id optimization
            string? documentId = DetectIdOptimization(include.FilterResults, include.PrimaryKeyPropertyName, queryContext);

            // When DocumentId is set, don't include query operations - they're not used in GetDocument
            IReadOnlyList<ResolvedFilterResult> filterResults;
            IReadOnlyList<ResolvedOrderByClause> orderByClauses;
            ResolvedPaginationInfo pagination;

            if (documentId != null)
            {
                // GetDocument mode - clear all query operations
                filterResults = Array.Empty<ResolvedFilterResult>();
                orderByClauses = Array.Empty<ResolvedOrderByClause>();
                pagination = ResolvedPaginationInfo.None;
            }
            else
            {
                // Query mode - resolve all operations
                filterResults = ResolveFilterResults(include.FilterResults, queryContext, targetEntityType);

                orderByClauses = include.OrderByClauses
                    .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                    .ToList();

                pagination = ResolvePagination(include.Pagination, queryContext);
            }

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

        private ResolvedProjectionDefinition ResolveProjection(
            FirestoreProjectionDefinition projection,
            IFirestoreQueryContext queryContext)
        {
            var subcollections = projection.Subcollections
                .Select(s => ResolveSubcollectionProjection(s, queryContext))
                .ToList();

            return new ResolvedProjectionDefinition(
                ResultType: projection.ResultType,
                ClrType: projection.ClrType,
                Fields: projection.Fields,
                Subcollections: subcollections);
        }

        private ResolvedSubcollectionProjection ResolveSubcollectionProjection(
            FirestoreSubcollectionProjection subcollection,
            IFirestoreQueryContext queryContext)
        {
            var collectionPath = subcollection.CollectionName;
            var targetClrType = subcollection.TargetClrType;
            var targetEntityType = queryContext.Model?.FindEntityType(targetClrType);

            // Detect Id optimization
            string? documentId = DetectIdOptimization(subcollection.FilterResults, subcollection.PrimaryKeyPropertyName, queryContext);

            // When DocumentId is set, don't include query operations - they're not used in GetDocument
            IReadOnlyList<ResolvedFilterResult> filterResults;
            IReadOnlyList<ResolvedOrderByClause> orderByClauses;
            ResolvedPaginationInfo pagination;

            if (documentId != null)
            {
                // GetDocument mode - clear all query operations
                filterResults = Array.Empty<ResolvedFilterResult>();
                orderByClauses = Array.Empty<ResolvedOrderByClause>();
                pagination = ResolvedPaginationInfo.None;
            }
            else
            {
                // Query mode - resolve all operations
                filterResults = ResolveFilterResults(subcollection.FilterResults, queryContext, targetEntityType);

                orderByClauses = subcollection.OrderByClauses
                    .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                    .ToList();

                pagination = ResolvePagination(subcollection.Pagination, queryContext);
            }

            var nestedSubcollections = subcollection.NestedSubcollections
                .Select(s => ResolveSubcollectionProjection(s, queryContext))
                .ToList();

            // Resolve includes (FK references within subcollection)
            var includes = ResolveIncludes(subcollection.Includes, queryContext);

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
                NestedSubcollections: nestedSubcollections,
                Includes: includes);
        }

        #endregion

        #region ID Optimization Detection

        private string? DetectIdOptimization(
            IReadOnlyList<FirestoreFilterResult> filterResults,
            string? primaryKeyPropertyName,
            IFirestoreQueryContext queryContext)
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

            var value = _expressionEvaluator.EvaluateWhereClause(clause, queryContext);
            return value?.ToString();
        }

        #endregion
    }
}
