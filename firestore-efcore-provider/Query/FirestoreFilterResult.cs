using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Result of translating a LINQ expression to Firestore filters.
    /// Supports AND (multiple clauses), OR (grouped clauses), and mixed AND+OR.
    /// </summary>
    public class FirestoreFilterResult
    {
        /// <summary>
        /// Clauses to be combined with AND logic (implicit in Firestore).
        /// Multiple Where() calls are automatically ANDed.
        /// </summary>
        public List<FirestoreWhereClause> AndClauses { get; } = new();

        /// <summary>
        /// OR group if the expression uses || operator at the top level.
        /// When set alone (without AndClauses), this is a pure OR expression.
        /// </summary>
        public FirestoreOrFilterGroup? OrGroup { get; set; }

        /// <summary>
        /// Nested OR groups found within AND expressions.
        /// Used for patterns like: A && (B || C)
        /// </summary>
        public List<FirestoreOrFilterGroup> NestedOrGroups { get; } = new();

        /// <summary>
        /// Indicates if this result contains a top-level OR group (pure OR expression)
        /// </summary>
        public bool IsOrGroup => OrGroup != null && AndClauses.Count == 0 && NestedOrGroups.Count == 0;

        /// <summary>
        /// Indicates if this result has nested OR groups (AND with OR pattern)
        /// </summary>
        public bool HasNestedOrGroups => NestedOrGroups.Count > 0;

        /// <summary>
        /// Creates an empty result
        /// </summary>
        public FirestoreFilterResult()
        {
        }

        /// <summary>
        /// Creates a result with a single AND clause
        /// </summary>
        public static FirestoreFilterResult FromClause(FirestoreWhereClause clause)
        {
            var result = new FirestoreFilterResult();
            result.AndClauses.Add(clause);
            return result;
        }

        /// <summary>
        /// Creates a result with multiple AND clauses
        /// </summary>
        public static FirestoreFilterResult FromAndClauses(IEnumerable<FirestoreWhereClause> clauses)
        {
            var result = new FirestoreFilterResult();
            result.AndClauses.AddRange(clauses);
            return result;
        }

        /// <summary>
        /// Creates a result with an OR group
        /// </summary>
        public static FirestoreFilterResult FromOrGroup(FirestoreOrFilterGroup orGroup)
        {
            return new FirestoreFilterResult { OrGroup = orGroup };
        }
    }
}
