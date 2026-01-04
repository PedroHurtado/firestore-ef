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
        var result = await next(context, cancellationToken).ConfigureAwait(false);

        // Skip if tracking is disabled
        if (!context.IsTracking)
        {
            return result;
        }

        // Process Materialized results
        if (result is PipelineResult.Materialized materialized)
        {
            var tracked = TrackEntities(materialized.Items, context);
            return new PipelineResult.Materialized(tracked, materialized.Context);
        }

        return result;
    }

    private static IReadOnlyList<object> TrackEntities(
        IReadOnlyList<object> entities,
        PipelineContext context)
    {
        var stateManager = context.QueryContext.StateManager;
        var model = context.QueryContext.Model;
        var entityType = model.FindEntityType(context.EntityType!);

        if (entityType == null)
            return entities;

        var key = entityType.FindPrimaryKey();
        var result = new List<object>(entities.Count);

        foreach (var entity in entities)
        {
            // Try identity resolution first
            var trackedEntity = TryGetTrackedEntity(stateManager, entityType, key, entity);

            if (trackedEntity != null)
            {
                result.Add(trackedEntity);
            }
            else
            {
                var entry = stateManager.GetOrCreateEntry(entity, entityType);
                entry.SetEntityState(Microsoft.EntityFrameworkCore.EntityState.Unchanged);
                result.Add(entity);
            }
        }

        return result;
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
