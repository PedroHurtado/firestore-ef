using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that loads navigation properties for Include/ThenInclude.
/// Runs after ProxyHandler to populate navigations on entities.
/// Uses ResolvedInclude from ResolvedFirestoreQuery - all resolution done by Resolver.
/// Only applies to Entity queries with resolved includes.
/// </summary>
public class IncludeHandler : QueryPipelineHandlerBase
{
    private readonly IIncludeLoader _includeLoader;

    /// <summary>
    /// Creates a new include handler.
    /// </summary>
    /// <param name="includeLoader">The include loader for executing resolved includes.</param>
    public IncludeHandler(IIncludeLoader includeLoader)
    {
        _includeLoader = includeLoader;
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

        // Get resolved includes from ResolvedFirestoreQuery
        var resolvedQuery = context.ResolvedQuery;
        if (resolvedQuery == null)
        {
            return result;
        }

        var includes = resolvedQuery.Includes;

        // Skip if no includes
        if (includes.Count == 0)
        {
            return result;
        }

        // Only process streaming results
        if (result is not PipelineResult.Streaming streaming)
        {
            return result;
        }

        // Load includes for each entity
        var withIncludes = LoadIncludesForEntities(
            streaming.Items,
            includes,
            context,
            cancellationToken);

        return new PipelineResult.Streaming(withIncludes, context);
    }

    private async IAsyncEnumerable<object> LoadIncludesForEntities(
        IAsyncEnumerable<object> entities,
        IReadOnlyList<ResolvedInclude> includes,
        PipelineContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = context.QueryContext.Model;

        await foreach (var entity in entities.WithCancellation(cancellationToken))
        {
            // Get entity type - for proxies, check BaseType
            var entityClrType = entity.GetType();
            var entityType = model.FindEntityType(entityClrType)
                ?? model.FindEntityType(entityClrType.BaseType!);

            if (entityType != null)
            {
                // Load each resolved include
                foreach (var include in includes)
                {
                    await _includeLoader.LoadIncludeAsync(
                        entity,
                        entityType,
                        include,
                        cancellationToken);
                }
            }

            yield return entity;
        }
    }
}
