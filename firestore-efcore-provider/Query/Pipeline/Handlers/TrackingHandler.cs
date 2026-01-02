using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that tracks entities via IStateManager.
/// Only applies to Entity queries when tracking is enabled.
/// </summary>
public class TrackingHandler : QueryPipelineHandlerBase
{
    private readonly IStateManager _stateManager;

    /// <summary>
    /// Creates a new tracking handler.
    /// </summary>
    /// <param name="stateManager">The EF Core state manager for entity tracking.</param>
    public TrackingHandler(IStateManager stateManager)
    {
        _stateManager = stateManager;
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

    private async IAsyncEnumerable<object> TrackEntities(
        IAsyncEnumerable<object> entities,
        PipelineContext context)
    {
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
            var trackedEntity = TryGetTrackedEntity(entityType, key, entity);

            if (trackedEntity != null)
            {
                // Return already tracked instance (identity resolution)
                yield return trackedEntity;
            }
            else
            {
                // Track the new entity
                var entry = _stateManager.GetOrCreateEntry(entity, entityType);
                entry.SetEntityState(Microsoft.EntityFrameworkCore.EntityState.Unchanged);

                yield return entity;
            }
        }
    }

    private object? TryGetTrackedEntity(IEntityType entityType, IKey? key, object entity)
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
        var existingEntry = _stateManager.TryGetEntry(key, keyValues);
        return existingEntry?.Entity;
    }
}
