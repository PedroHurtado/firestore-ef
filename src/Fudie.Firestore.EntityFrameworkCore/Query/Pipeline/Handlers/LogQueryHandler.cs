using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that logs the resolved Firestore query using EF Core diagnostics.
/// Allows developers to see what queries are being sent to Firestore.
/// Should be placed after ResolverHandler and before ExecutionHandler.
/// </summary>
/// <remarks>
/// Configure logging via DbContextOptionsBuilder:
/// <code>
/// optionsBuilder.ConfigureWarnings(w => w
///     .Log((FirestoreEventId.QueryExecuting, LogLevel.Debug)));
/// </code>
/// </remarks>
public class LogQueryHandler : IQueryPipelineHandler
{
    private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _logger;

    /// <summary>
    /// Creates a new log query handler.
    /// </summary>
    /// <param name="logger">EF Core diagnostics logger.</param>
    public LogQueryHandler(IDiagnosticsLogger<DbLoggerCategory.Query> logger)
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

        if (resolved != null)
        {
            _logger.QueryExecuting(resolved);
        }

        return await next(context, cancellationToken);
    }
}
