using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
    /// Cache for JsonSerializerOptions with ignored properties.
    /// Key is the set of ignored property names (sorted and joined).
    /// </summary>
    private static readonly ConcurrentDictionary<string, JsonSerializerOptions> OptionsCache = new();

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
            var ignoredProps = entityType.GetArrayOfIgnoredProperties(trackerFor);
            var currentJson = SerializeArrayProperty(entry.Entity, trackerFor, ignoredProps);
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

            var ignoredProps = entityType.GetArrayOfIgnoredProperties(trackerFor);
            var json = SerializeArrayProperty(entry.Entity, trackerFor, ignoredProps);
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

            var ignoredProps = entityType.GetArrayOfIgnoredProperties(trackerFor);
            var json = SerializeArrayProperty(entity, trackerFor, ignoredProps);

            // Set both current and original value via InternalEntityEntry
            internalEntry[property] = json;
            internalEntry.SetOriginalValue(property, json);
        }
    }

    /// <summary>
    /// Serializes an array property to JSON for comparison.
    /// </summary>
    /// <param name="entity">The entity containing the array property.</param>
    /// <param name="propertyName">The name of the array property.</param>
    /// <param name="ignoredProperties">Properties to exclude from serialization (configured via Ignore() in OnModelCreating).</param>
    private static string? SerializeArrayProperty(object entity, string propertyName, HashSet<string>? ignoredProperties)
    {
        var propertyInfo = entity.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo == null)
            return null;

        var value = propertyInfo.GetValue(entity);
        if (value == null)
            return null;

        var options = GetOrCreateOptions(ignoredProperties);
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Gets or creates JsonSerializerOptions with the specified ignored properties.
    /// Options are cached for performance.
    /// </summary>
    private static JsonSerializerOptions GetOrCreateOptions(HashSet<string>? ignoredProperties)
    {
        if (ignoredProperties == null || ignoredProperties.Count == 0)
            return JsonOptions;

        // Create a cache key from sorted property names
        var sortedProps = ignoredProperties.OrderBy(p => p);
        var cacheKey = string.Join(",", sortedProps);

        return OptionsCache.GetOrAdd(cacheKey, _ => CreateOptionsWithIgnoredProperties(ignoredProperties));
    }

    /// <summary>
    /// Creates JsonSerializerOptions that exclude the specified properties.
    /// </summary>
    private static JsonSerializerOptions CreateOptionsWithIgnoredProperties(HashSet<string> ignoredProperties)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;

            for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                if (ignoredProperties.Contains(typeInfo.Properties[i].Name))
                {
                    typeInfo.Properties.RemoveAt(i);
                }
            }
        });

        return new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
            TypeInfoResolver = resolver,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
