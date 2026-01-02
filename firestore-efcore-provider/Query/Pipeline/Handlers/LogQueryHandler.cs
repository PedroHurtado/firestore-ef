using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that logs the resolved Firestore query before execution.
/// Allows developers to see what queries are being sent to Firestore.
/// Should be placed after ResolverHandler and before ExecutionHandler.
/// </summary>
public class LogQueryHandler : IQueryPipelineHandler
{
    private readonly ILogger<LogQueryHandler> _logger;

    /// <summary>
    /// Creates a new log query handler.
    /// </summary>
    /// <param name="logger">Logger for query logging.</param>
    public LogQueryHandler(ILogger<LogQueryHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var resolved = context.ResolvedQuery;

        if (resolved != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Executing Firestore query: {Query}",
                resolved.ToString());
        }

        return await next(context, cancellationToken);
    }
}
