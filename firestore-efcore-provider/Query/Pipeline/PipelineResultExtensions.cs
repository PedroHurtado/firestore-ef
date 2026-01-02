using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Extension methods for converting between pipeline result types.
/// </summary>
public static class PipelineResultExtensions
{
    /// <summary>
    /// Materializes a streaming result into a materialized result.
    /// Loads all items into memory.
    /// </summary>
    /// <param name="streaming">The streaming result to materialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A materialized result with all items loaded.</returns>
    public static async Task<PipelineResult.Materialized> MaterializeAsync(
        this PipelineResult.Streaming streaming,
        CancellationToken cancellationToken)
    {
        var items = await streaming.Items.ToListAsync(cancellationToken);
        return new PipelineResult.Materialized(items, streaming.Context);
    }

    /// <summary>
    /// Converts a materialized result to a streaming result.
    /// Useful when a handler needs streaming input but has materialized data.
    /// </summary>
    /// <param name="materialized">The materialized result to convert.</param>
    /// <returns>A streaming result backed by the materialized items.</returns>
    public static PipelineResult.Streaming ToStreaming(this PipelineResult.Materialized materialized)
    {
        return new PipelineResult.Streaming(
            materialized.Items.ToAsyncEnumerable(),
            materialized.Context);
    }

    /// <summary>
    /// Converts a list to an async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask; // Ensure async method
    }

    /// <summary>
    /// Converts an async enumerable to a list.
    /// </summary>
    private static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken)
    {
        var list = new List<T>();

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }

        return list;
    }
}
