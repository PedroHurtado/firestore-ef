using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Base record for pipeline results.
/// Each variant represents a different type of query result.
/// </summary>
public abstract record PipelineResult(PipelineContext Context)
{
    /// <summary>
    /// Streaming result - allows processing entity by entity.
    /// Used for queries that return multiple items.
    /// </summary>
    public sealed record Streaming(
        IAsyncEnumerable<object> Items,
        PipelineContext Context
    ) : PipelineResult(Context);

    /// <summary>
    /// Materialized result - complete collection in memory.
    /// Used when all items need to be loaded before processing.
    /// </summary>
    public sealed record Materialized(
        IReadOnlyList<object> Items,
        PipelineContext Context
    ) : PipelineResult(Context);

    /// <summary>
    /// Scalar result - single value.
    /// Used for aggregations: Count, Sum, Any, etc.
    /// </summary>
    public sealed record Scalar(
        object Value,
        PipelineContext Context
    ) : PipelineResult(Context);

    /// <summary>
    /// Empty result - query returned no results.
    /// </summary>
    public sealed record Empty(
        PipelineContext Context
    ) : PipelineResult(Context);
}
