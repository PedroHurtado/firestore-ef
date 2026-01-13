using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Provides change tracking support for ArrayOf properties.
/// ArrayOf properties are ignored by EF Core (to prevent navigation detection),
/// so this helper uses shadow properties with JSON serialization to detect changes.
/// </summary>
public static class ArrayOfChangeTracker
{
    /// <summary>
    /// JSON serialization options that match the Firestore provider's serialization conventions.
    /// Uses JsonStringEnumConverter to serialize enums as strings (e.g., "Monday" instead of 1),
    /// consistent with IFirestoreValueConverter.ToFirestore() behavior.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Synchronizes ArrayOf shadow properties and marks entities as Modified if changes are detected.
    /// Call this before base.SaveChanges() or base.SaveChangesAsync().
    /// </summary>
    /// <param name="context">The DbContext to synchronize.</param>
    public static void SyncArrayOfChanges(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            SyncEntityArrays(entry);
        }
    }

    /// <summary>
    /// Synchronizes ArrayOf shadow properties for a single entity entry.
    /// </summary>
    internal static void SyncEntityArrays(EntityEntry entry)
    {
        var entityType = entry.Metadata;

        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor == null)
                continue;

            var shadowProp = entry.Property(property.Name);
            var currentJson = SerializeArrayProperty(entry.Entity, trackerFor);
            var originalJson = shadowProp.OriginalValue as string;

            if (!string.Equals(originalJson, currentJson, StringComparison.Ordinal))
            {
                shadowProp.CurrentValue = currentJson;

                // If entity was Unchanged, mark it as Modified
                if (entry.State == EntityState.Unchanged)
                {
                    entry.State = EntityState.Modified;
                }
            }
        }
    }

    /// <summary>
    /// Initializes the shadow property with the current JSON value of the array.
    /// Called when an entity is first tracked (after materialization).
    /// </summary>
    /// <param name="entry">The entity entry to initialize.</param>
    /// <param name="entityType">The entity type metadata.</param>
    internal static void InitializeShadowProperties(EntityEntry entry, IEntityType entityType)
    {
        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor == null)
                continue;

            var json = SerializeArrayProperty(entry.Entity, trackerFor);
            var shadowProp = entry.Property(property.Name);

            shadowProp.CurrentValue = json;
            shadowProp.OriginalValue = json;
        }
    }

    /// <summary>
    /// Initializes shadow properties using the internal state manager entry.
    /// Used by TrackingHandler which works with InternalEntityEntry.
    /// </summary>
    internal static void InitializeShadowProperties(
        object entity,
        IEntityType entityType,
        Microsoft.EntityFrameworkCore.ChangeTracking.Internal.InternalEntityEntry internalEntry)
    {
        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor == null)
                continue;

            var json = SerializeArrayProperty(entity, trackerFor);

            // Set both current and original value via InternalEntityEntry
            internalEntry[property] = json;
            internalEntry.SetOriginalValue(property, json);
        }
    }

    /// <summary>
    /// Serializes an array property to JSON for comparison.
    /// </summary>
    private static string? SerializeArrayProperty(object entity, string propertyName)
    {
        var propertyInfo = entity.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo == null)
            return null;

        var value = propertyInfo.GetValue(entity);
        if (value == null)
            return null;

        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
