using Firestore.EntityFrameworkCore.Query.Resolved;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Builds a Firestore SDK Query from a ResolvedFirestoreQuery.
/// Translates resolved filters, ordering, pagination into SDK calls.
/// </summary>
public interface IQueryBuilder
{
    /// <summary>
    /// Builds a Firestore SDK Query from the resolved query.
    /// </summary>
    /// <param name="resolvedQuery">The resolved query with all values evaluated.</param>
    /// <returns>A Firestore SDK Query ready for execution.</returns>
    Google.Cloud.Firestore.Query Build(ResolvedFirestoreQuery resolvedQuery);
}
