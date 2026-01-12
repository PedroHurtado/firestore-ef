using System;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Typed key for pipeline metadata.
/// Provides type safety when getting/setting metadata values.
/// </summary>
/// <typeparam name="T">The type of the metadata value.</typeparam>
public readonly record struct MetadataKey<T>(string Name);

/// <summary>
/// Well-known metadata keys used by pipeline handlers.
/// </summary>
public static class PipelineMetadataKeys
{
    /// <summary>
    /// Indicates if lazy loading is required for this query.
    /// </summary>
    public static readonly MetadataKey<bool> RequiresLazyLoader = new("RequiresLazyLoader");

    /// <summary>
    /// Cache key for the query result.
    /// </summary>
    public static readonly MetadataKey<string> CacheKey = new("CacheKey");

    /// <summary>
    /// Set of entities tracked during query execution.
    /// </summary>
    public static readonly MetadataKey<HashSet<object>> TrackedEntities = new("TrackedEntities");

    /// <summary>
    /// Time taken to execute the query.
    /// </summary>
    public static readonly MetadataKey<TimeSpan> ExecutionTime = new("ExecutionTime");

    /// <summary>
    /// The proxy factory to use for creating entity instances.
    /// When set, ConvertHandler will use this to create proxy instances instead of plain entities.
    /// </summary>
    public static readonly MetadataKey<IProxyFactory> ProxyFactory = new("ProxyFactory");

    /// <summary>
    /// All document snapshots loaded by ExecutionHandler, including includes.
    /// Key is the full document path (e.g., "Clientes/cli-001", "Clientes/cli-001/Pedidos/ped-001").
    /// Used by ConvertHandler for bottom-up deserialization with includes.
    /// </summary>
    public static readonly MetadataKey<Dictionary<string, Google.Cloud.Firestore.DocumentSnapshot>> AllSnapshots = new("AllSnapshots");

    /// <summary>
    /// Subcollection aggregation results loaded by ExecutionHandler.
    /// Key format: "{parentDocPath}:{resultName}" (e.g., "Clientes/cli-001:Totales").
    /// Value is the aggregation result (long for Count, double for Sum/Average).
    /// Used for materializing projections with subcollection aggregations.
    /// </summary>
    public static readonly MetadataKey<Dictionary<string, object>> SubcollectionAggregations = new("SubcollectionAggregations");
}
