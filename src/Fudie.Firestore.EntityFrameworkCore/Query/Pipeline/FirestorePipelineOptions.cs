using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Defines the level of detail for Firestore query logging.
/// </summary>
public enum QueryLogLevel
{
    /// <summary>
    /// No logging.
    /// </summary>
    None = 0,

    /// <summary>
    /// Logs query and document count.
    /// Example: "[Firestore] Stores: 3 docs, 45.2ms"
    /// </summary>
    Count = 1,

    /// <summary>
    /// Logs query, document count, and IDs.
    /// Example: "[Firestore] Stores: 3 docs [id1, id2, id3], 45.2ms"
    /// </summary>
    Ids = 2,

    /// <summary>
    /// Logs query, document count, IDs, and full document data.
    /// Useful during development to see exactly what Firestore returns.
    /// </summary>
    Full = 3
}

/// <summary>
/// Configuration options for the query pipeline.
/// </summary>
public class FirestorePipelineOptions
{
    /// <summary>
    /// Enables logging of the AST (Abstract Syntax Tree) before query execution.
    /// Default is false.
    /// </summary>
    public bool EnableAstLogging { get; set; } = false;

    /// <summary>
    /// Level of detail for query logging.
    /// Default is Count (shows query and document count).
    /// </summary>
    public QueryLogLevel QueryLogLevel { get; set; } = QueryLogLevel.Count;

    /// <summary>
    /// Enables query result caching.
    /// Default is false.
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Maximum number of retry attempts for transient errors.
    /// Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay before the first retry.
    /// Subsequent retries use exponential backoff.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
