using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that provides error handling with retry logic for transient errors.
/// Should be registered first in the pipeline to wrap all other handlers.
/// </summary>
public class ErrorHandlingHandler : IQueryPipelineHandler
{
    private readonly ILogger<ErrorHandlingHandler> _logger;
    private readonly FirestoreErrorHandlingOptions _options;

    /// <summary>
    /// Creates a new error handling handler.
    /// </summary>
    /// <param name="logger">Logger for error logging.</param>
    /// <param name="options">Error handling configuration options.</param>
    public ErrorHandlingHandler(
        ILogger<ErrorHandlingHandler> logger,
        FirestoreErrorHandlingOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (FirestoreQueryExecutionException ex) when (ex.IsTransient && attempt < _options.MaxRetries)
            {
                attempt++;
                _logger.LogWarning(ex,
                    "Transient error on attempt {Attempt}/{MaxRetries}. Retrying...",
                    attempt, _options.MaxRetries);

                var delay = _options.GetDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
