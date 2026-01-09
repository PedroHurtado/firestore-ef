using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Query.Resolved
{
    #region Where Clauses

    /// <summary>
    /// Resolved version of FirestoreWhereClause.
    /// Contains the evaluated and converted value instead of an Expression.
    /// Values are already converted to Firestore types (enum→string, decimal→double, etc.)
    /// </summary>
    public record ResolvedWhereClause(
        string PropertyName,
        FirestoreOperator Operator,
        object? Value,
        bool IsPrimaryKey = false)
    {
        public override string ToString()
        {
            var operatorSymbol = Operator switch
            {
                FirestoreOperator.EqualTo => "==",
                FirestoreOperator.NotEqualTo => "!=",
                FirestoreOperator.LessThan => "<",
                FirestoreOperator.LessThanOrEqualTo => "<=",
                FirestoreOperator.GreaterThan => ">",
                FirestoreOperator.GreaterThanOrEqualTo => ">=",
                FirestoreOperator.ArrayContains => "array-contains",
                FirestoreOperator.In => "in",
                FirestoreOperator.ArrayContainsAny => "array-contains-any",
                FirestoreOperator.NotIn => "not-in",
                _ => Operator.ToString()
            };

            var pkIndicator = IsPrimaryKey ? " [PK]" : "";
            return $"{PropertyName} {operatorSymbol} {Value}{pkIndicator}";
        }
    }

    /// <summary>
    /// Resolved version of FirestoreOrFilterGroup.
    /// Contains resolved clauses instead of Expression-based clauses.
    /// </summary>
    public record ResolvedOrFilterGroup(IReadOnlyList<ResolvedWhereClause> Clauses)
    {
        public override string ToString() => $"OR({string.Join(", ", Clauses)})";
    }

    /// <summary>
    /// Resolved version of FirestoreFilterResult.
    /// Contains resolved clauses and groups.
    /// The Executor/Resolver will use this to apply filters - no need to "expand" into separate lists.
    /// </summary>
    public record ResolvedFilterResult(
        IReadOnlyList<ResolvedWhereClause> AndClauses,
        ResolvedOrFilterGroup? OrGroup = null,
        IReadOnlyList<ResolvedOrFilterGroup>? NestedOrGroups = null)
    {
        /// <summary>
        /// Indicates if this result contains a top-level OR group (pure OR expression)
        /// </summary>
        public bool IsOrGroup => OrGroup != null && AndClauses.Count == 0 && (NestedOrGroups == null || NestedOrGroups.Count == 0);

        /// <summary>
        /// Indicates if this result has nested OR groups (AND with OR pattern)
        /// </summary>
        public bool HasNestedOrGroups => NestedOrGroups != null && NestedOrGroups.Count > 0;

        /// <summary>
        /// Creates an empty result
        /// </summary>
        public static ResolvedFilterResult Empty => new(Array.Empty<ResolvedWhereClause>());

        /// <summary>
        /// Creates a result with a single AND clause
        /// </summary>
        public static ResolvedFilterResult FromClause(ResolvedWhereClause clause)
            => new(new[] { clause });

        /// <summary>
        /// Creates a result with an OR group
        /// </summary>
        public static ResolvedFilterResult FromOrGroup(ResolvedOrFilterGroup orGroup)
            => new(Array.Empty<ResolvedWhereClause>(), orGroup);
    }

    #endregion

    #region OrderBy

    /// <summary>
    /// Resolved version of FirestoreOrderByClause.
    /// Note: OrderBy doesn't have Expressions, so this is effectively the same structure.
    /// Included for consistency in the resolved types hierarchy.
    /// </summary>
    public record ResolvedOrderByClause(string PropertyName, bool Descending = false)
    {
        public override string ToString() => $"{PropertyName} {(Descending ? "DESC" : "ASC")}";
    }

    #endregion

    #region Pagination

    /// <summary>
    /// Resolved version of FirestorePaginationInfo.
    /// Contains resolved int values instead of Expressions.
    /// </summary>
    public record ResolvedPaginationInfo(
        int? Limit = null,
        int? LimitToLast = null,
        int? Skip = null)
    {
        /// <summary>
        /// Whether any pagination is configured.
        /// </summary>
        public bool HasPagination => Limit.HasValue || LimitToLast.HasValue || Skip.HasValue;

        /// <summary>
        /// Whether Limit is set.
        /// </summary>
        public bool HasLimit => Limit.HasValue;

        /// <summary>
        /// Whether LimitToLast is set.
        /// </summary>
        public bool HasLimitToLast => LimitToLast.HasValue;

        /// <summary>
        /// Whether Skip is set.
        /// </summary>
        public bool HasSkip => Skip.HasValue;

        /// <summary>
        /// Empty pagination info (no limits or skip).
        /// </summary>
        public static ResolvedPaginationInfo None => new();

        public override string ToString()
        {
            var parts = new List<string>();

            if (Limit.HasValue)
                parts.Add($"Limit={Limit.Value}");
            if (LimitToLast.HasValue)
                parts.Add($"LimitToLast={LimitToLast.Value}");
            if (Skip.HasValue)
                parts.Add($"Skip={Skip.Value}");

            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }
    }

    #endregion

    #region Cursor

    /// <summary>
    /// Resolved version of FirestoreCursor.
    /// Note: Cursor already contains resolved values (no Expressions).
    /// Included for consistency in the resolved types hierarchy.
    /// </summary>
    public record ResolvedCursor(string DocumentId, IReadOnlyList<object?>? OrderByValues = null)
    {
        public override string ToString()
        {
            if (OrderByValues == null || OrderByValues.Count == 0)
                return $"Cursor({DocumentId})";
            return $"Cursor({DocumentId}, [{string.Join(", ", OrderByValues)}])";
        }
    }

    #endregion

    #region Include

    /// <summary>
    /// Resolved version of IncludeInfo.
    /// Contains the resolved collection path and optional document ID for GetDocumentAsync optimization.
    /// The Executor just executes - no logic needed.
    /// </summary>
    public record ResolvedInclude(
        string NavigationName,
        bool IsCollection,
        Type TargetEntityType,

        // Path resolved by Resolver - relative to parent document
        // e.g., "categories" for menus/{id}/categories
        string CollectionPath,

        // If set, use GetDocumentAsync instead of query (Id optimization)
        // e.g., "cat-456" for menus/{id}/categories/cat-456
        string? DocumentId,

        // Filters (only applied if DocumentId is null)
        IReadOnlyList<ResolvedFilterResult> FilterResults,
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,
        ResolvedPaginationInfo Pagination,

        // Nested includes
        IReadOnlyList<ResolvedInclude> NestedIncludes)
    {
        /// <summary>
        /// Whether this is a document query (GetDocumentAsync) vs collection query.
        /// </summary>
        public bool IsDocumentQuery => DocumentId != null;

        /// <summary>
        /// Whether this include has any filter/ordering/limit operations.
        /// </summary>
        public bool HasOperations =>
            FilterResults.Count > 0 ||
            OrderByClauses.Count > 0 ||
            Pagination.HasPagination;

        /// <summary>
        /// Total count of filter clauses across all FilterResults.
        /// </summary>
        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString()
        {
            var parts = new List<string> { NavigationName };
            if (IsCollection) parts.Add("[Collection]");
            if (DocumentId != null)
                parts.Add($"Doc({DocumentId})");
            if (FilterResults.Count > 0)
                parts.Add($"Where({TotalFilterCount})");
            if (OrderByClauses.Count > 0)
                parts.Add($"OrderBy({OrderByClauses.Count})");
            if (Pagination.HasLimit)
                parts.Add($"Take({Pagination.Limit})");
            if (Pagination.HasSkip)
                parts.Add($"Skip({Pagination.Skip})");
            return string.Join(".", parts);
        }
    }

    #endregion

    #region Projection

    /// <summary>
    /// Resolved version of FirestoreProjectionDefinition.
    /// Note: Projection doesn't have Expressions directly, but subcollections do.
    /// </summary>
    public record ResolvedProjectionDefinition(
        ProjectionResultType ResultType,
        Type ClrType,
        IReadOnlyList<FirestoreProjectedField>? Fields,
        IReadOnlyList<ResolvedSubcollectionProjection> Subcollections)
    {
        /// <summary>
        /// Whether this projection has fields to select (not Entity projection).
        /// </summary>
        public bool HasFields => Fields != null && Fields.Count > 0;

        /// <summary>
        /// Whether this projection includes subcollections.
        /// </summary>
        public bool HasSubcollections => Subcollections.Count > 0;
    }

    /// <summary>
    /// Resolved version of FirestoreSubcollectionProjection.
    /// Contains the resolved collection path and optional document ID for GetDocumentAsync optimization.
    /// </summary>
    public record ResolvedSubcollectionProjection(
        string NavigationName,
        string ResultName,
        Type TargetEntityType,

        // Path resolved by Resolver - relative to parent document
        string CollectionPath,

        // If set, use GetDocumentAsync instead of query (Id optimization)
        string? DocumentId,

        // Filters (only applied if DocumentId is null)
        IReadOnlyList<ResolvedFilterResult> FilterResults,
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,
        ResolvedPaginationInfo Pagination,

        IReadOnlyList<FirestoreProjectedField>? Fields,
        FirestoreAggregationType? Aggregation,
        string? AggregationPropertyName,
        IReadOnlyList<ResolvedSubcollectionProjection> NestedSubcollections)
    {
        /// <summary>
        /// Whether this is a document query (GetDocumentAsync) vs collection query.
        /// </summary>
        public bool IsDocumentQuery => DocumentId != null;

        /// <summary>
        /// Indicates if this is an aggregation projection.
        /// </summary>
        public bool IsAggregation => Aggregation.HasValue && Aggregation != FirestoreAggregationType.None;

        /// <summary>
        /// Total count of filter clauses across all FilterResults.
        /// </summary>
        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString()
        {
            var parts = new List<string> { NavigationName };
            if (DocumentId != null)
                parts.Add($"Doc({DocumentId})");
            if (FilterResults.Count > 0)
                parts.Add($"Where({TotalFilterCount})");
            if (OrderByClauses.Count > 0)
                parts.Add($"OrderBy({OrderByClauses.Count})");
            if (Pagination.HasLimit)
                parts.Add($"Take({Pagination.Limit})");
            if (Aggregation.HasValue)
                parts.Add($"{Aggregation}");
            return string.Join(".", parts);
        }
    }

    #endregion

    #region Main Query

    /// <summary>
    /// Resolved version of FirestoreQueryExpression.
    /// Contains all resolved values ready for execution by FirestoreQueryExecutor.
    /// No Expressions, no EF Core types - pure data ready for SDK calls.
    ///
    /// The Executor is now "dumb" - it just builds SDK calls from this data.
    /// All logic (path resolution, Id optimization, PK detection) is done by the Resolver.
    /// </summary>
    public record ResolvedFirestoreQuery(
        // Path resolved by Resolver - e.g., "menus" or for nested queries
        string CollectionPath,
        Type EntityClrType,

        // If set, use GetDocumentAsync instead of query (Id optimization)
        // e.g., "menu-123" for menus/menu-123
        string? DocumentId,

        // Filters (only applied if DocumentId is null)
        IReadOnlyList<ResolvedFilterResult> FilterResults,

        // OrderBy (already pure)
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,

        // Pagination (resolved)
        ResolvedPaginationInfo Pagination,

        // Cursor (already pure)
        ResolvedCursor? StartAfterCursor,

        // Includes (resolved with paths)
        IReadOnlyList<ResolvedInclude> Includes,

        // Aggregation (already pure)
        FirestoreAggregationType AggregationType,
        string? AggregationPropertyName,
        Type? AggregationResultType,

        // Projection (resolved)
        ResolvedProjectionDefinition? Projection,

        // Behavior flags
        bool ReturnDefault,
        Type? ReturnType)
    {
        /// <summary>
        /// Whether this is a document query (GetDocumentAsync) vs collection query.
        /// </summary>
        public bool IsDocumentQuery => DocumentId != null;

        /// <summary>
        /// Whether this is an aggregation query.
        /// </summary>
        public bool IsAggregation => AggregationType != FirestoreAggregationType.None;

        /// <summary>
        /// Whether this query has a projection.
        /// </summary>
        public bool HasProjection => Projection != null;

        /// <summary>
        /// Whether this is a Count query.
        /// </summary>
        public bool IsCountQuery => AggregationType == FirestoreAggregationType.Count;

        /// <summary>
        /// Whether this is an Any query.
        /// </summary>
        public bool IsAnyQuery => AggregationType == FirestoreAggregationType.Any;

        /// <summary>
        /// Total count of filter clauses across all FilterResults.
        /// </summary>
        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString()
        {
            var parts = new List<string>();

            if (DocumentId != null)
                parts.Add($"Document: {CollectionPath}/{DocumentId}");
            else
                parts.Add($"Collection: {CollectionPath}");

            if (FilterResults.Count > 0)
                parts.Add($"Filters: {TotalFilterCount} clauses in {FilterResults.Count} results");

            if (OrderByClauses.Count > 0)
                parts.Add($"OrderBy: [{string.Join(", ", OrderByClauses)}]");

            if (Pagination.HasLimit)
                parts.Add($"Limit: {Pagination.Limit}");

            if (Pagination.HasSkip)
                parts.Add($"Skip: {Pagination.Skip}");

            if (StartAfterCursor != null)
                parts.Add($"StartAfter: {StartAfterCursor}");

            if (IsAggregation)
                parts.Add($"Aggregation: {AggregationType}");

            return string.Join(" | ", parts);
        }
    }

    #endregion
}
