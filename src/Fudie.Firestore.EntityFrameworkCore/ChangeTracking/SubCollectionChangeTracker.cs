using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Provides change tracking fixes for SubCollection entities.
/// Corrects the state of SubCollection entities that EF Core incorrectly marks as Modified
/// when they should be Deleted (when removed from parent's collection).
/// </summary>
public static class SubCollectionChangeTracker
{
    /// <summary>
    /// Fixes the state of SubCollection entities that were removed from their parent's collection.
    /// EF Core marks them as Modified with FK=null, but they should be Deleted.
    /// Call this before base.SaveChanges() or base.SaveChangesAsync().
    /// </summary>
    /// <param name="context">The DbContext to fix.</param>
    public static void FixSubCollectionDeleteState(DbContext context)
    {
        var model = context.Model;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Only process Modified entries
            if (entry.State != EntityState.Modified)
                continue;

            // Check if this entity type is a SubCollection
            var parentNavigation = FindParentNavigationForSubCollection(entry.Metadata, model);
            if (parentNavigation == null)
                continue;

            // Get the FK property name (e.g., "MenuId" for Menu -> MenuCategory)
            var fkPropertyName = ConventionHelpers.GetForeignKeyPropertyName(
                parentNavigation.DeclaringEntityType.ClrType);

            // Check if FK changed from a value to null (indicates removal from collection)
            var fkProperty = entry.Property(fkPropertyName);
            if (fkProperty == null)
                continue;

            var originalValue = fkProperty.OriginalValue;
            var currentValue = fkProperty.CurrentValue;

            // If FK was set (not null/default) and now is null → entity was removed from collection
            if (originalValue != null && !IsDefaultValue(originalValue) && currentValue == null)
            {
                entry.State = EntityState.Deleted;
            }
        }
    }

    /// <summary>
    /// Finds the navigation that marks this entity type as a subcollection.
    /// </summary>
    private static INavigation? FindParentNavigationForSubCollection(IEntityType entityType, IModel model)
    {
        foreach (var parentEntityType in model.GetEntityTypes())
        {
            foreach (var navigation in parentEntityType.GetNavigations())
            {
                if (navigation.TargetEntityType == entityType && navigation.IsSubCollection())
                {
                    return navigation;
                }
            }
        }

        return null;
    }

    private static bool IsDefaultValue(object value)
    {
        var type = value.GetType();
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }
        return false;
    }
}
