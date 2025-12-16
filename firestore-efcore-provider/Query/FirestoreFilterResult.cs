using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Result of translating a LINQ expression to Firestore filters.
    /// Supports AND (multiple clauses) and OR (grouped clauses).
    /// </summary>
    public class FirestoreFilterResult
    {
        /// <summary>
        /// Clauses to be combined with AND logic (implicit in Firestore).
        /// Multiple Where() calls are automatically ANDed.
        /// </summary>
        public List<FirestoreWhereClause> AndClauses { get; } = new();

        /// <summary>
        /// OR group if the expression uses || operator.
        /// When set, this takes precedence and AndClauses should be empty.
        /// </summary>
        public FirestoreOrFilterGroup? OrGroup { get; set; }

        /// <summary>
        /// Indicates if this result contains an OR group
        /// </summary>
        public bool IsOrGroup => OrGroup != null;

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
