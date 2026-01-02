using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Base class for handlers that only apply to specific query kinds.
/// Automatically skips to next handler when query kind is not applicable.
/// </summary>
public abstract class QueryPipelineHandlerBase : IQueryPipelineHandler
{
    /// <summary>
    /// The query kinds this handler processes.
    /// Override to specify which kinds this handler should handle.
    /// </summary>
    protected abstract QueryKind[] ApplicableKinds { get; }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        if (!ApplicableKinds.Contains(context.Kind))
        {
            return await next(context, cancellationToken);
        }

        return await HandleCoreAsync(context, next, cancellationToken);
    }

    /// <summary>
    /// Handles the pipeline context when the query kind is applicable.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline result.</returns>
    protected abstract Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken);
}
