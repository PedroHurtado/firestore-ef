using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that configures proxy creation for entity queries.
/// Runs BEFORE SnapshotShapingHandler to set up proxy factory in metadata.
/// SnapshotShapingHandler then uses the proxy factory to create proxy instances during materialization.
/// Only applies to Entity queries when proxy factory is available.
/// </summary>
public class ProxyHandler : QueryPipelineHandlerBase
{
    private readonly IProxyFactory? _proxyFactory;

    /// <summary>
    /// Creates a new proxy handler.
    /// </summary>
    /// <param name="proxyFactory">
    /// The proxy factory for creating lazy-loading proxies.
    /// Can be null when proxies are not configured.
    /// </param>
    public ProxyHandler(IProxyFactory? proxyFactory)
    {
        _proxyFactory = proxyFactory;
    }

    /// <inheritdoc />
    protected override QueryKind[] ApplicableKinds => new[] { QueryKind.Entity };

    /// <inheritdoc />
    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        // Skip if proxy factory is not available (proxies not configured)
        if (_proxyFactory == null)
        {
            return await next(context, cancellationToken);
        }

        // Add proxy factory to metadata so SnapshotShapingHandler can use it
        var newContext = context.WithMetadata(PipelineMetadataKeys.ProxyFactory, _proxyFactory);

        return await next(newContext, cancellationToken);
    }
}
