using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that wraps entities in lazy-loading proxies.
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
        var result = await next(context, cancellationToken);

        // Skip if proxy factory is not available (proxies not configured)
        if (_proxyFactory == null)
        {
            return result;
        }

        // Only process streaming results
        if (result is not PipelineResult.Streaming streaming)
        {
            return result;
        }

        // Wrap entities in proxies as they stream through
        var proxied = CreateProxies(streaming.Items, context);
        return new PipelineResult.Streaming(proxied, context);
    }

    private async IAsyncEnumerable<object> CreateProxies(
        IAsyncEnumerable<object> entities,
        PipelineContext context)
    {
        var model = context.QueryContext.Model;
        var entityType = model.FindEntityType(context.EntityType!);

        if (entityType == null)
        {
            // No entity type metadata, pass through without proxying
            await foreach (var entity in entities)
            {
                yield return entity;
            }
            yield break;
        }

        await foreach (var entity in entities)
        {
            // Create lazy-loading proxy for the entity
            var proxy = _proxyFactory!.CreateLazyLoadingProxy(entityType, entity);
            yield return proxy;
        }
    }
}
