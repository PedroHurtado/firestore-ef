using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

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
    /// Enables logging of executed queries.
    /// Default is true.
    /// </summary>
    public bool EnableQueryLogging { get; set; } = true;

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
