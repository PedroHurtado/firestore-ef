using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that tracks entities via IStateManager.
/// Only applies to Entity queries when tracking is enabled.
/// Gets IStateManager from QueryContext at runtime to avoid circular DI dependencies.
/// </summary>
public class TrackingHandler : QueryPipelineHandlerBase
{
    /// <inheritdoc />
    protected override QueryKind[] ApplicableKinds => new[] { QueryKind.Entity };

    /// <inheritdoc />
    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        // Skip if tracking is disabled
        if (!context.IsTracking)
        {
            return result;
        }

        // Only process streaming results
        if (result is not PipelineResult.Streaming streaming)
        {
            return result;
        }

        // Track entities as they stream through
        var tracked = TrackEntities(streaming.Items, context);
        return new PipelineResult.Streaming(tracked, context);
    }

    private static async IAsyncEnumerable<object> TrackEntities(
        IAsyncEnumerable<object> entities,
        PipelineContext context)
    {
        // Get StateManager from context at runtime (avoids circular DI)
        var stateManager = context.QueryContext.StateManager;
        var model = context.QueryContext.Model;
        var entityType = model.FindEntityType(context.EntityType!);

        if (entityType == null)
        {
            // No entity type metadata, pass through without tracking
            await foreach (var entity in entities)
            {
                yield return entity;
            }
            yield break;
        }

        var key = entityType.FindPrimaryKey();

        await foreach (var entity in entities)
        {
            // Try identity resolution first - check if already tracked
            var trackedEntity = TryGetTrackedEntity(stateManager, entityType, key, entity);

            if (trackedEntity != null)
            {
                // Return already tracked instance (identity resolution)
                yield return trackedEntity;
            }
            else
            {
                // Track the new entity
                var entry = stateManager.GetOrCreateEntry(entity, entityType);
                entry.SetEntityState(Microsoft.EntityFrameworkCore.EntityState.Unchanged);

                yield return entity;
            }
        }
    }

    private static object? TryGetTrackedEntity(
        IStateManager stateManager,
        IEntityType entityType,
        IKey? key,
        object entity)
    {
        if (key == null || key.Properties.Count == 0)
        {
            return null;
        }

        // Get key values from the entity
        var keyValues = new object?[key.Properties.Count];
        for (var i = 0; i < key.Properties.Count; i++)
        {
            var property = key.Properties[i];
            keyValues[i] = property.GetGetter().GetClrValue(entity);
        }

        // Look up in state manager
        var existingEntry = stateManager.TryGetEntry(key, keyValues);
        return existingEntry?.Entity;
    }
}
