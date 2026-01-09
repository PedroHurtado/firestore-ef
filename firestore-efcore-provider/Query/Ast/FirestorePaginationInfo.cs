using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// DTO that encapsulates pagination/limit properties for Firestore queries.
    /// Used by FirestoreQueryExpression, IncludeInfo, and FirestoreSubcollectionProjection
    /// to ensure consistent handling of Take/Skip/TakeLast operations.
    ///
    /// For each operation, there's a constant value (int?) and an Expression for
    /// parameterized queries that are resolved at execution time by AstResolver.
    /// </summary>
    public class FirestorePaginationInfo
    {
        #region Limit (Take)

        /// <summary>
        /// Maximum number of documents to return (Take).
        /// Null means no limit.
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>
        /// Expression for the limit (for EF Core parameterized queries).
        /// Evaluated at execution time by AstResolver.
        /// </summary>
        public Expression? LimitExpression { get; set; }

        #endregion

        #region LimitToLast (TakeLast)

        /// <summary>
        /// Maximum number of documents to return from the end (TakeLast).
        /// Firestore uses LimitToLast() which requires a prior OrderBy.
        /// Null means no limit from last.
        /// </summary>
        public int? LimitToLast { get; set; }

        /// <summary>
        /// Expression for LimitToLast (for EF Core parameterized queries).
        /// Evaluated at execution time by AstResolver.
        /// </summary>
        public Expression? LimitToLastExpression { get; set; }

        #endregion

        #region Skip (Offset)

        /// <summary>
        /// Number of documents to skip (Skip/Offset).
        /// NOTE: Firestore doesn't support native offset. Skip is applied
        /// in memory after retrieving results.
        /// Null means no skip.
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// Expression for skip (for EF Core parameterized queries).
        /// Evaluated at execution time by AstResolver.
        /// </summary>
        public Expression? SkipExpression { get; set; }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Whether any pagination is configured.
        /// </summary>
        public bool HasPagination =>
            Limit.HasValue ||
            LimitExpression != null ||
            LimitToLast.HasValue ||
            LimitToLastExpression != null ||
            Skip.HasValue ||
            SkipExpression != null;

        /// <summary>
        /// Whether Limit is set (either as constant or expression).
        /// </summary>
        public bool HasLimit => Limit.HasValue || LimitExpression != null;

        /// <summary>
        /// Whether LimitToLast is set (either as constant or expression).
        /// </summary>
        public bool HasLimitToLast => LimitToLast.HasValue || LimitToLastExpression != null;

        /// <summary>
        /// Whether Skip is set (either as constant or expression).
        /// </summary>
        public bool HasSkip => Skip.HasValue || SkipExpression != null;

        #endregion

        #region Commands (Fluent API)

        /// <summary>
        /// Sets the limit (constant value).
        /// </summary>
        public FirestorePaginationInfo WithLimit(int limit)
        {
            Limit = limit;
            LimitExpression = null;
            return this;
        }

        /// <summary>
        /// Sets the limit (expression for parameterized queries).
        /// </summary>
        public FirestorePaginationInfo WithLimitExpression(Expression limitExpression)
        {
            Limit = null;
            LimitExpression = limitExpression;
            return this;
        }

        /// <summary>
        /// Sets the limit to last (constant value).
        /// </summary>
        public FirestorePaginationInfo WithLimitToLast(int limitToLast)
        {
            LimitToLast = limitToLast;
            LimitToLastExpression = null;
            return this;
        }

        /// <summary>
        /// Sets the limit to last (expression for parameterized queries).
        /// </summary>
        public FirestorePaginationInfo WithLimitToLastExpression(Expression limitToLastExpression)
        {
            LimitToLast = null;
            LimitToLastExpression = limitToLastExpression;
            return this;
        }

        /// <summary>
        /// Sets the skip count (constant value).
        /// </summary>
        public FirestorePaginationInfo WithSkip(int skip)
        {
            Skip = skip;
            SkipExpression = null;
            return this;
        }

        /// <summary>
        /// Sets the skip count (expression for parameterized queries).
        /// </summary>
        public FirestorePaginationInfo WithSkipExpression(Expression skipExpression)
        {
            Skip = null;
            SkipExpression = skipExpression;
            return this;
        }

        #endregion

        #region ToString

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (Limit.HasValue)
                parts.Add($"Limit={Limit.Value}");
            else if (LimitExpression != null)
                parts.Add("Limit=<expr>");

            if (LimitToLast.HasValue)
                parts.Add($"LimitToLast={LimitToLast.Value}");
            else if (LimitToLastExpression != null)
                parts.Add("LimitToLast=<expr>");

            if (Skip.HasValue)
                parts.Add($"Offset={Skip.Value}");
            else if (SkipExpression != null)
                parts.Add("Offset=<expr>");

            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }

        #endregion
    }
}