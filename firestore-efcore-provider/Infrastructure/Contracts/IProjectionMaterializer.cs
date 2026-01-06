using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Materializes projection results from Firestore documents.
/// Creates anonymous types or DTOs from DocumentSnapshots and aggregation results.
/// </summary>
public interface IProjectionMaterializer
{
    /// <summary>
    /// Materializes a projection result from document snapshots and aggregation values.
    /// </summary>
    /// <param name="projection">The resolved projection definition with field mappings and subcollections.</param>
    /// <param name="rootSnapshot">The root document snapshot for this projection instance.</param>
    /// <param name="allSnapshots">All loaded document snapshots, keyed by document path.</param>
    /// <param name="aggregations">Subcollection aggregation results, keyed by "{parentPath}:{resultName}".</param>
    /// <returns>The materialized projection object (anonymous type or DTO).</returns>
    object Materialize(
        ResolvedProjectionDefinition projection,
        DocumentSnapshot rootSnapshot,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations);

    /// <summary>
    /// Materializes a list of projection results from multiple root documents.
    /// </summary>
    /// <param name="projection">The resolved projection definition.</param>
    /// <param name="rootSnapshots">The root document snapshots to materialize.</param>
    /// <param name="allSnapshots">All loaded document snapshots.</param>
    /// <param name="aggregations">Subcollection aggregation results.</param>
    /// <returns>List of materialized projection objects.</returns>
    IReadOnlyList<object> MaterializeMany(
        ResolvedProjectionDefinition projection,
        IEnumerable<DocumentSnapshot> rootSnapshots,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations);
}
