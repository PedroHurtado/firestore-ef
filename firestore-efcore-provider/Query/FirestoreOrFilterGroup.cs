using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Represents an OR group of filters for Firestore queries.
    /// All clauses within this group are combined using OR logic.
    /// </summary>
    public class FirestoreOrFilterGroup
    {
        /// <summary>
        /// The clauses to be combined with OR logic
        /// </summary>
        public List<FirestoreWhereClause> Clauses { get; } = new();

        public FirestoreOrFilterGroup()
        {
        }

        public FirestoreOrFilterGroup(IEnumerable<FirestoreWhereClause> clauses)
        {
            Clauses.AddRange(clauses);
        }

        public override string ToString()
        {
            return $"OR({string.Join(", ", Clauses)})";
        }
    }
}
