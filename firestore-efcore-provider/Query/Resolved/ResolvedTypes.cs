using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            var op = Operator switch
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

            var valueStr = FormatValue(Value);
            var pk = IsPrimaryKey ? " [PK]" : "";
            return $"{PropertyName} {op} {valueStr}{pk}";
        }

        private static string FormatValue(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            IEnumerable<object> list => $"[{string.Join(", ", list.Select(FormatValue))}]",
            _ => value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Resolved version of FirestoreOrFilterGroup.
    /// Contains resolved clauses instead of Expression-based clauses.
    /// </summary>
    public record ResolvedOrFilterGroup(IReadOnlyList<ResolvedWhereClause> Clauses)
    {
        public override string ToString() => Clauses.Count switch
        {
            0 => "OR()",
            1 => Clauses[0].ToString(),
            _ => $"({string.Join(" || ", Clauses)})"
        };
    }

    /// <summary>
    /// Resolved version of FirestoreFilterResult.
    /// Contains resolved clauses and groups.
    /// </summary>
    public record ResolvedFilterResult(
        IReadOnlyList<ResolvedWhereClause> AndClauses,
        ResolvedOrFilterGroup? OrGroup = null,
        IReadOnlyList<ResolvedOrFilterGroup>? NestedOrGroups = null)
    {
        public bool IsOrGroup => OrGroup != null && AndClauses.Count == 0 && (NestedOrGroups == null || NestedOrGroups.Count == 0);
        public bool HasNestedOrGroups => NestedOrGroups != null && NestedOrGroups.Count > 0;

        public static ResolvedFilterResult Empty => new(Array.Empty<ResolvedWhereClause>());
        public static ResolvedFilterResult FromClause(ResolvedWhereClause clause) => new(new[] { clause });
        public static ResolvedFilterResult FromOrGroup(ResolvedOrFilterGroup orGroup) => new(Array.Empty<ResolvedWhereClause>(), orGroup);

        public override string ToString()
        {
            var parts = new List<string>();

            if (AndClauses.Count > 0)
                parts.Add(string.Join(" && ", AndClauses));

            if (OrGroup != null)
                parts.Add(OrGroup.ToString());

            if (NestedOrGroups != null && NestedOrGroups.Count > 0)
                parts.AddRange(NestedOrGroups.Select(g => g.ToString()));

            return parts.Count == 0 ? "(empty)" : string.Join(" && ", parts);
        }
    }

    #endregion

    #region OrderBy

    /// <summary>
    /// Resolved version of FirestoreOrderByClause.
    /// </summary>
    public record ResolvedOrderByClause(string PropertyName, bool Descending = false)
    {
        public override string ToString() => Descending ? $"{PropertyName} DESC" : PropertyName;
    }

    #endregion

    #region Pagination

    /// <summary>
    /// Resolved version of FirestorePaginationInfo.
    /// </summary>
    public record ResolvedPaginationInfo(
        int? Limit = null,
        int? LimitToLast = null,
        int? Skip = null)
    {
        public bool HasPagination => Limit.HasValue || LimitToLast.HasValue || Skip.HasValue;
        public bool HasLimit => Limit.HasValue;
        public bool HasLimitToLast => LimitToLast.HasValue;
        public bool HasSkip => Skip.HasValue;

        public static ResolvedPaginationInfo None => new();

        public override string ToString()
        {
            if (!HasPagination) return "";

            var parts = new List<string>();
            if (Skip.HasValue) parts.Add($".Skip({Skip.Value})");
            if (Limit.HasValue) parts.Add($".Take({Limit.Value})");
            if (LimitToLast.HasValue) parts.Add($".TakeLast({LimitToLast.Value})");
            return string.Join("", parts);
        }
    }

    #endregion

    #region Cursor

    /// <summary>
    /// Resolved version of FirestoreCursor.
    /// </summary>
    public record ResolvedCursor(string DocumentId, IReadOnlyList<object?>? OrderByValues = null)
    {
        public override string ToString()
        {
            if (OrderByValues == null || OrderByValues.Count == 0)
                return $".StartAfter(\"{DocumentId}\")";
            return $".StartAfter(\"{DocumentId}\", [{string.Join(", ", OrderByValues)}])";
        }
    }

    #endregion

    #region Include

    /// <summary>
    /// Resolved version of IncludeInfo.
    /// </summary>
    public record ResolvedInclude(
        string NavigationName,
        bool IsCollection,
        Type TargetEntityType,
        string CollectionPath,
        string? DocumentId,
        IReadOnlyList<ResolvedFilterResult> FilterResults,
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,
        ResolvedPaginationInfo Pagination,
        IReadOnlyList<ResolvedInclude> NestedIncludes)
    {
        public bool IsDocumentQuery => DocumentId != null;
        public bool HasOperations => FilterResults.Count > 0 || OrderByClauses.Count > 0 || Pagination.HasPagination;
        public bool IsReference => !IsCollection;

        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString() => ToString(0);

        public string ToString(int indent)
        {
            var sb = new StringBuilder();
            var prefix = new string(' ', indent * 2);

            // Include header con tipo de navegación
            var navType = IsCollection ? "SubCollection" : "Reference";
            sb.AppendLine($"{prefix}.Include({NavigationName}) [{navType}]");

            // Execution path
            if (DocumentId != null)
            {
                sb.AppendLine($"{prefix}  → GetDocument: {CollectionPath}/{DocumentId}");
            }
            else
            {
                sb.AppendLine($"{prefix}  → Query: {CollectionPath}");

                if (FilterResults.Count > 0)
                {
                    foreach (var filter in FilterResults)
                        sb.AppendLine($"{prefix}    .Where({filter})");
                }

                if (OrderByClauses.Count > 0)
                    sb.AppendLine($"{prefix}    .OrderBy({string.Join(", ", OrderByClauses)})");

                if (Pagination.HasPagination)
                    sb.AppendLine($"{prefix}    {Pagination}");
            }

            // Nested includes
            foreach (var nested in NestedIncludes)
                sb.Append(nested.ToString(indent + 1));

            return sb.ToString();
        }
    }

    #endregion

    #region Projection

    /// <summary>
    /// Resolved version of FirestoreProjectionDefinition.
    /// </summary>
    public record ResolvedProjectionDefinition(
        ProjectionResultType ResultType,
        Type ClrType,
        IReadOnlyList<FirestoreProjectedField>? Fields,
        IReadOnlyList<ResolvedSubcollectionProjection> Subcollections)
    {
        public bool HasFields => Fields != null && Fields.Count > 0;
        public bool HasSubcollections => Subcollections.Count > 0;

        private static string GetCleanTypeName(Type type)
        {
            // Si es tipo anónimo generado por el compilador, simplificarlo
            if (type.Name.StartsWith("<>") || type.Name.Contains("AnonymousType"))
                return "AnonymousType";
            return type.Name;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            // Solo mostrar el tipo si no es anónimo (aporta información)
            var typeName = GetCleanTypeName(ClrType);
            if (typeName == "AnonymousType")
                sb.Append("Select");
            else
                sb.Append($"Select → {typeName}");

            if (HasFields)
                sb.Append($" [{string.Join(", ", Fields!.Select(f => f.FieldPath))}]");

            if (HasSubcollections)
            {
                sb.AppendLine();
                foreach (var sub in Subcollections)
                    sb.AppendLine($"  {sub}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Resolved version of FirestoreSubcollectionProjection.
    /// </summary>
    public record ResolvedSubcollectionProjection(
        string NavigationName,
        string ResultName,
        Type TargetEntityType,
        string CollectionPath,
        string? DocumentId,
        IReadOnlyList<ResolvedFilterResult> FilterResults,
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,
        ResolvedPaginationInfo Pagination,
        IReadOnlyList<FirestoreProjectedField>? Fields,
        FirestoreAggregationType? Aggregation,
        string? AggregationPropertyName,
        IReadOnlyList<ResolvedSubcollectionProjection> NestedSubcollections)
    {
        public bool IsDocumentQuery => DocumentId != null;
        public bool IsAggregation => Aggregation.HasValue && Aggregation != FirestoreAggregationType.None;

        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($".{ResultName}");

            if (IsAggregation)
            {
                sb.Append($" = {Aggregation}({AggregationPropertyName ?? "*"})");
            }
            else if (DocumentId != null)
            {
                sb.Append($" → GetDocument: {CollectionPath}/{DocumentId}");
            }
            else
            {
                sb.Append($" → Query: {CollectionPath}");
                if (FilterResults.Count > 0)
                    sb.Append($".Where({string.Join(" && ", FilterResults)})");
                if (OrderByClauses.Count > 0)
                    sb.Append($".OrderBy({string.Join(", ", OrderByClauses)})");
                sb.Append(Pagination);
            }

            return sb.ToString();
        }
    }

    #endregion

    #region Main Query

    /// <summary>
    /// Resolved version of FirestoreQueryExpression.
    /// Contains all resolved values ready for execution.
    /// </summary>
    public record ResolvedFirestoreQuery(
        string CollectionPath,
        Type EntityClrType,
        string? DocumentId,
        IReadOnlyList<ResolvedFilterResult> FilterResults,
        IReadOnlyList<ResolvedOrderByClause> OrderByClauses,
        ResolvedPaginationInfo Pagination,
        ResolvedCursor? StartAfterCursor,
        IReadOnlyList<ResolvedInclude> Includes,
        FirestoreAggregationType AggregationType,
        string? AggregationPropertyName,
        Type? AggregationResultType,
        ResolvedProjectionDefinition? Projection,
        bool ReturnDefault,
        Type? ReturnType)
    {
        public bool IsDocumentQuery => DocumentId != null;
        public bool IsAggregation => AggregationType != FirestoreAggregationType.None;
        public bool HasProjection => Projection != null;
        public bool IsCountQuery => AggregationType == FirestoreAggregationType.Count;
        public bool IsAnyQuery => AggregationType == FirestoreAggregationType.Any;

        public int TotalFilterCount => FilterResults.Sum(fr =>
            fr.AndClauses.Count +
            (fr.OrGroup?.Clauses.Count ?? 0) +
            (fr.NestedOrGroups?.Sum(og => og.Clauses.Count) ?? 0));

        public override string ToString()
        {
            var sb = new StringBuilder();

            // Execution path - línea principal
            if (IsAggregation)
            {
                var prop = AggregationPropertyName != null ? $"({AggregationPropertyName})" : "";
                sb.AppendLine($"{CollectionPath}.{AggregationType}{prop}");
            }
            else if (DocumentId != null)
            {
                sb.AppendLine($"GetDocument: {CollectionPath}/{DocumentId}");
            }
            else
            {
                sb.AppendLine($"Query: {CollectionPath}");
            }

            // Filters
            if (FilterResults.Count > 0)
            {
                foreach (var filter in FilterResults)
                    sb.AppendLine($"  .Where({filter})");
            }

            // OrderBy
            if (OrderByClauses.Count > 0)
                sb.AppendLine($"  .OrderBy({string.Join(", ", OrderByClauses)})");

            // Pagination
            if (Pagination.HasPagination)
                sb.AppendLine($"  {Pagination}");

            // Cursor
            if (StartAfterCursor != null)
                sb.AppendLine($"  {StartAfterCursor}");

            // Projection
            if (Projection != null)
                sb.AppendLine($"  .{Projection}");

            // Includes
            if (Includes.Count > 0)
            {
                foreach (var include in Includes)
                    sb.Append(include.ToString(1));
            }

            return sb.ToString();
        }
    }

    #endregion
}
