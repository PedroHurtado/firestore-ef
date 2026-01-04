using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Default implementation of the query pipeline mediator.
/// Builds and executes the handler chain.
/// </summary>
public class QueryPipelineMediator : IQueryPipelineMediator
{
    private readonly IReadOnlyList<IQueryPipelineHandler> _handlers;

    /// <summary>
    /// Creates a new mediator with the specified handlers.
    /// Handlers are executed in the order they are provided.
    /// </summary>
    /// <param name="handlers">The handlers to execute in order.</param>
    public QueryPipelineMediator(IEnumerable<IQueryPipelineHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ExecuteAsync<T>(
        PipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Set mediator in context for sub-queries (e.g., Include)
        var contextWithMediator = context.Mediator == null
            ? context with { Mediator = this }
            : context;

        var pipeline = BuildPipeline();
        var result = await pipeline(contextWithMediator, cancellationToken);

        await foreach (var item in UnwrapResult<T>(result, cancellationToken))
        {
            yield return item;
        }
    }

    private PipelineDelegate BuildPipeline()
    {
        // Start with a terminal delegate that returns Empty
        PipelineDelegate pipeline = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Build the pipeline in reverse order so handlers execute in registration order
        for (var i = _handlers.Count - 1; i >= 0; i--)
        {
            var handler = _handlers[i];
            var next = pipeline;

            pipeline = (ctx, ct) => handler.HandleAsync(ctx, next, ct);
        }

        return pipeline;
    }

    private static async IAsyncEnumerable<T> UnwrapResult<T>(
        PipelineResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        switch (result)
        {
            case PipelineResult.Streaming streaming:
                await foreach (var item in streaming.Items.WithCancellation(cancellationToken))
                {
                    yield return (T)item;
                }
                break;

            case PipelineResult.Materialized materialized:
                foreach (var item in materialized.Items)
                {
                    yield return (T)item;
                }
                break;

            case PipelineResult.Scalar scalar:
                yield return (T)scalar.Value;
                break;

            case PipelineResult.Empty:
                yield break;
        }
    }
}
