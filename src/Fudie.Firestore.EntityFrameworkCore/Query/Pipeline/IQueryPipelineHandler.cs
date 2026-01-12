using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler in the query pipeline.
/// Each handler processes the context and either:
/// - Modifies it and calls next
/// - Returns a result directly
/// - Skips processing and calls next
/// </summary>
public interface IQueryPipelineHandler
{
    /// <summary>
    /// Handles the pipeline context.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline result.</returns>
    Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken);
}
