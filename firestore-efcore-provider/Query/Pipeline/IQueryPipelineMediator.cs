using System.Collections.Generic;
using System.Threading;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Mediator that orchestrates the execution of the query pipeline.
/// Entry point for executing queries through the handler chain.
/// </summary>
public interface IQueryPipelineMediator
{
    /// <summary>
    /// Executes the query pipeline and returns results as an async enumerable.
    /// </summary>
    /// <typeparam name="T">The type of items to return.</typeparam>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of results.</returns>
    IAsyncEnumerable<T> ExecuteAsync<T>(
        PipelineContext context,
        CancellationToken cancellationToken);
}
