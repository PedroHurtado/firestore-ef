using System.Collections.Generic;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Transforms a flat list of DocumentSnapshots into a hierarchical dictionary structure
/// based on the query's AST (ResolvedFirestoreQuery).
/// </summary>
/// <remarks>
/// When executing a query with Includes (e.g., Menu with Include(Categories).ThenInclude(Items)),
/// Firestore returns multiple flat snapshots. This shaper reorganizes them into a proper
/// hierarchy matching the navigation structure defined in the query.
/// </remarks>
public interface ISnapshotShaper
{
    /// <summary>
    /// Shapes flat snapshots into a hierarchical structure based on the query's includes.
    /// </summary>
    /// <param name="query">The resolved query AST containing the include structure.</param>
    /// <param name="snapshots">Flat list of all document snapshots retrieved from Firestore.</param>
    /// <param name="aggregations">Dictionary of subcollection aggregations (key: "parentPath:resultName", value: aggregation result).</param>
    /// <returns>
    /// ShapedResult with hierarchical structure (one dictionary per root document).
    /// Returns empty ShapedResult if no matching root documents are found.
    /// The ShapedResult.ToString() returns JSON for debugging.
    /// </returns>
    ShapedResult Shape(
        ResolvedFirestoreQuery query,
        IReadOnlyList<DocumentSnapshot> snapshots,
        Dictionary<string, object>? aggregations = null);
}