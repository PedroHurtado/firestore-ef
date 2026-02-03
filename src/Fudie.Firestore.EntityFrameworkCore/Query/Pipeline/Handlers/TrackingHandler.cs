using Fudie.Firestore.EntityFrameworkCore.ChangeTracking;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that tracks entities via IStateManager.
/// Only applies to Entity queries when tracking is enabled.
/// Gets IStateManager from QueryContext at runtime to avoid circular DI dependencies.
/// Tracks both root entities and related entities from Includes.
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

        if (!context.IsTracking)
        {
            return result;
        }

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
        var includes = context.ResolvedQuery?.Includes ?? [];

        foreach (var entity in entities)
        {
            var trackedEntity = TryGetTrackedEntity(stateManager, entityType, key, entity);

            if (trackedEntity != null)
            {
                // Copiar navegaciones del entity fresco al trackeado
                CopyNavigations(entity, trackedEntity, entityType, includes);
                result.Add(trackedEntity);
            }
            else
            {
                var entry = stateManager.GetOrCreateEntry(entity, entityType);

                // Initialize ArrayOf and MapOf shadow properties BEFORE setting state to Unchanged
                // This ensures the original values are set correctly
                ArrayOfChangeTracker.InitializeShadowProperties(entity, entityType, entry);
                MapOfChangeTracker.InitializeShadowProperties(entity, entityType, entry);

                entry.SetEntityState(Microsoft.EntityFrameworkCore.EntityState.Unchanged);

                result.Add(entity);
            }

            // Siempre usar entity (tiene los datos frescos) para trackear relacionadas
            TrackRelatedEntities(entity, entityType, includes, stateManager, model);
        }

        return result;
    }

    /// <summary>
    /// Copies navigation properties from a freshly loaded entity to an already tracked entity.
    /// Only copies if the target navigation is null (doesn't overwrite existing values).
    /// </summary>
    private static void CopyNavigations(
        object source,
        object target,
        IEntityType entityType,
        IReadOnlyList<ResolvedInclude> includes)
    {
        foreach (var include in includes)
        {
            var navigation = entityType.FindNavigation(include.NavigationName);
            if (navigation == null)
                continue;

            var sourceValue = navigation.GetGetter().GetClrValue(source);
            if (sourceValue == null)
                continue;

            // Solo copiar si el target no tiene valor
            var targetValue = navigation.GetGetter().GetClrValue(target);
            if (targetValue != null)
                continue;

            var setter = navigation.PropertyInfo?.SetMethod;
            if (setter != null)
            {
                setter.Invoke(target, new[] { sourceValue });
            }
        }
    }

    /// <summary>
    /// Recursively tracks related entities from navigation properties.
    /// </summary>
    private static void TrackRelatedEntities(
        object entity,
        IEntityType entityType,
        IReadOnlyList<ResolvedInclude> includes,
        IStateManager stateManager,
        IModel model)
    {
        foreach (var include in includes)
        {
            var navigation = entityType.FindNavigation(include.NavigationName);
            if (navigation == null)
                continue;

            var relatedEntityType = navigation.TargetEntityType;
            var relatedKey = relatedEntityType.FindPrimaryKey();
            var navigationValue = navigation.GetGetter().GetClrValue(entity);

            if (navigationValue == null)
                continue;

            if (include.IsCollection && navigationValue is IEnumerable collection)
            {
                foreach (var relatedEntity in collection)
                {
                    if (relatedEntity == null)
                        continue;

                    TrackSingleEntity(relatedEntity, relatedEntityType, relatedKey, stateManager);

                    if (include.NestedIncludes.Count > 0)
                    {
                        TrackRelatedEntities(relatedEntity, relatedEntityType, include.NestedIncludes, stateManager, model);
                    }
                }
            }
            else if (!include.IsCollection)
            {
                TrackSingleEntity(navigationValue, relatedEntityType, relatedKey, stateManager);

                if (include.NestedIncludes.Count > 0)
                {
                    TrackRelatedEntities(navigationValue, relatedEntityType, include.NestedIncludes, stateManager, model);
                }
            }
        }
    }

    /// <summary>
    /// Tracks a single entity if not already tracked.
    /// </summary>
    private static void TrackSingleEntity(
        object entity,
        IEntityType entityType,
        IKey? key,
        IStateManager stateManager)
    {
        var existingEntry = TryGetTrackedEntity(stateManager, entityType, key, entity);
        if (existingEntry != null)
            return;

        var entry = stateManager.GetOrCreateEntry(entity, entityType);

        // Initialize ArrayOf and MapOf shadow properties BEFORE setting state to Unchanged
        ArrayOfChangeTracker.InitializeShadowProperties(entity, entityType, entry);
        MapOfChangeTracker.InitializeShadowProperties(entity, entityType, entry);

        entry.SetEntityState(Microsoft.EntityFrameworkCore.EntityState.Unchanged);
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

        var keyValues = new object?[key.Properties.Count];
        for (var i = 0; i < key.Properties.Count; i++)
        {
            var property = key.Properties[i];
            keyValues[i] = property.GetGetter().GetClrValue(entity);
        }

        var existingEntry = stateManager.TryGetEntry(key, keyValues);
        return existingEntry?.Entity;
    }
}