using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using FirestoreQuery = Google.Cloud.Firestore.Query;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Builds Firestore SDK Query and AggregateQuery from a ResolvedFirestoreQuery.
/// Translates resolved filters, ordering, pagination, and aggregations into SDK calls.
/// </summary>
public interface IQueryBuilder
{
    /// <summary>
    /// Builds a Firestore SDK Query from the resolved query.
    /// Used for collection queries and Min/Max (which use OrderBy + Limit).
    /// </summary>
    /// <param name="resolvedQuery">The resolved query with all values evaluated.</param>
    /// <returns>A Firestore SDK Query ready for execution.</returns>
    FirestoreQuery Build(ResolvedFirestoreQuery resolvedQuery);

    /// <summary>
    /// Builds a Firestore SDK AggregateQuery from the resolved query.
    /// Used for native aggregations: Count, Sum, Average, Any.
    /// </summary>
    /// <param name="resolvedQuery">The resolved query with aggregation type.</param>
    /// <returns>An AggregateQuery ready for execution.</returns>
    AggregateQuery BuildAggregate(ResolvedFirestoreQuery resolvedQuery);
}
