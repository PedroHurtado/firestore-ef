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

        /// <summary>
        /// Creates a new AST resolver with the specified dependencies.
        /// </summary>
        /// <param name="valueConverter">The value converter for CLR to Firestore type conversion.</param>
        /// <param name="expressionEvaluator">The expression evaluator for LINQ expression resolution.</param>
        public FirestoreAstResolver(IFirestoreValueConverter valueConverter, IExpressionEvaluator expressionEvaluator)
        {
            _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
            _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
        }
        /// <summary>
        /// Resolves the AST into a fully resolved query ready for execution.
        /// </summary>
        /// <param name="ast">The AST to resolve</param>
        /// <param name="queryContext">The query context containing parameter values</param>
        public ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast, IFirestoreQueryContext queryContext)
        {
            if (ast == null) throw new ArgumentNullException(nameof(ast));
            if (queryContext == null) throw new ArgumentNullException(nameof(queryContext));

            // Use CollectionName from AST directly
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
                // Don't optimize to document query - use collection query path for field selection
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

            // Resolve filters (pass EntityType for null validation)
            var filterResults = ResolveFilterResults(ast.FilterResults, queryContext, ast.EntityType);

            // Resolve OrderBy (already pure, but map to resolved types)
            var orderByClauses = ast.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(ast.Pagination, queryContext);

            // Resolve Cursor (already pure)
            ResolvedCursor? cursor = null;
            if (ast.StartAfterCursor != null)
            {
                cursor = new ResolvedCursor(
                    ast.StartAfterCursor.DocumentId,
                    ast.StartAfterCursor.OrderByValues);
            }

            // Resolve Includes using metadata from AST
            var includes = ResolveIncludes(ast.PendingIncludes, queryContext);

            // Resolve Projection using metadata from AST
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
            // (decimal → double, enum → string, DateTime → UTC)
            // Pass EnumType for int-to-enum-string conversion when EF Core parameterizes enums
            var convertedValue = _valueConverter.ToFirestore(value, clause.EnumType);

            return new ResolvedWhereClause(
                clause.PropertyName,
                clause.Operator,
                convertedValue);
        }

        /// <summary>
        /// Validates that a null filter is allowed for the property.
        /// Throws NotSupportedException if property doesn't have PersistNullValues configured.
        /// </summary>
        private static void ValidateNullFilter(string propertyName, IEntityType entityType)
        {
            // Skip validation for Id property - it's the document ID and handled separately
            // Also, if we get null for Id, it's likely a parameter evaluation issue, not an actual null filter
            if (propertyName == "Id")
                return;

            // Get property from model - support nested properties
            var propertyPath = propertyName.Split('.');
            var property = entityType.FindProperty(propertyPath[0]);

            if (property == null)
            {
                // If we don't find the property, it might be a nested property of ComplexType
                // In this case, allow the query (no way to validate ComplexTypes yet)
                return;
            }

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
            // Build hierarchy from flat list using ParentClrType
            return BuildIncludeHierarchy(includes, queryContext, parentType: null);
        }

        /// <summary>
        /// Builds include hierarchy by finding includes that belong to a parent type.
        /// Root includes have ParentClrType == null.
        /// ThenIncludes have ParentClrType == parent's TargetClrType.
        /// </summary>
        private IReadOnlyList<ResolvedInclude> BuildIncludeHierarchy(
            IReadOnlyList<IncludeInfo> allIncludes,
            IFirestoreQueryContext queryContext,
            Type? parentType)
        {
            var result = new List<ResolvedInclude>();

            // Find includes that belong to this level (matching parent type)
            var levelIncludes = allIncludes
                .Where(i => i.ParentClrType == parentType)
                .ToList();

            foreach (var include in levelIncludes)
            {
                // Recursively resolve nested includes (ThenInclude)
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
            // Use metadata from AST directly
            var collectionPath = include.CollectionName;
            var targetClrType = include.TargetClrType;

            // Detect Id optimization from filters using PrimaryKeyPropertyName from AST
            string? documentId = DetectIdOptimization(include.FilterResults, include.PrimaryKeyPropertyName, queryContext);

            // Resolve filters
            var filterResults = ResolveFilterResults(include.FilterResults, queryContext);

            // Resolve OrderBy
            var orderByClauses = include.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(include.Pagination, queryContext);

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
            // Use metadata from AST directly
            var collectionPath = subcollection.CollectionName;
            var targetClrType = subcollection.TargetClrType;

            // Detect Id optimization using PrimaryKeyPropertyName from AST
            string? documentId = DetectIdOptimization(subcollection.FilterResults, subcollection.PrimaryKeyPropertyName, queryContext);

            // Resolve filters
            var filterResults = ResolveFilterResults(subcollection.FilterResults, queryContext);

            // Resolve OrderBy
            var orderByClauses = subcollection.OrderByClauses
                .Select(o => new ResolvedOrderByClause(o.PropertyName, o.Descending))
                .ToList();

            // Resolve Pagination
            var pagination = ResolvePagination(subcollection.Pagination, queryContext);

            // Resolve nested subcollections
            var nestedSubcollections = subcollection.NestedSubcollections
                .Select(s => ResolveSubcollectionProjection(s, queryContext))
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
