using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using FirestoreQuery = Google.Cloud.Firestore.Query;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

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

    /// <summary>
    /// Builds a Firestore SDK Query for a subcollection include.
    /// Applies filters, ordering, and pagination from the ResolvedInclude.
    /// </summary>
    /// <param name="parentDocPath">The full path of the parent document (e.g., "Clientes/cli-001").</param>
    /// <param name="include">The resolved include with filters, ordering, and pagination.</param>
    /// <returns>A Firestore SDK Query ready for execution.</returns>
    FirestoreQuery BuildInclude(string parentDocPath, ResolvedInclude include);

    /// <summary>
    /// Builds a Firestore SDK Query for a subcollection projection.
    /// Applies filters, ordering, pagination, and field selection from the ResolvedSubcollectionProjection.
    /// </summary>
    /// <param name="parentDocPath">The full path of the parent document (e.g., "Clientes/cli-001").</param>
    /// <param name="subcollection">The resolved subcollection projection.</param>
    /// <returns>A Firestore SDK Query ready for execution.</returns>
    FirestoreQuery BuildSubcollectionQuery(string parentDocPath, ResolvedSubcollectionProjection subcollection);

    /// <summary>
    /// Builds a Firestore SDK AggregateQuery for a subcollection aggregation.
    /// Used for Sum, Count, Average on subcollections within projections.
    /// </summary>
    /// <param name="parentDocPath">The full path of the parent document (e.g., "Clientes/cli-001").</param>
    /// <param name="subcollection">The resolved subcollection projection with aggregation info.</param>
    /// <returns>An AggregateQuery ready for execution.</returns>
    AggregateQuery BuildSubcollectionAggregate(string parentDocPath, ResolvedSubcollectionProjection subcollection);
}
