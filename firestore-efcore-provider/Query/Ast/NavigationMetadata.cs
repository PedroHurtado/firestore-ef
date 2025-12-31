using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Contains all metadata needed for a navigation property.
    /// Extracted once in translators, stored in AST, used by Resolver.
    /// </summary>
    public record NavigationMetadata(
        string CollectionName,
        bool IsCollection,
        Type TargetClrType,
        string? PrimaryKeyPropertyName);

    /// <summary>
    /// Helper to extract navigation metadata from EF Core metadata.
    /// Used in translators to populate AST with all necessary information.
    /// </summary>
    public static class NavigationMetadataHelper
    {
        /// <summary>
        /// Extracts all navigation metadata from an INavigation.
        /// </summary>
        public static NavigationMetadata GetMetadata(
            INavigation navigation,
            IFirestoreCollectionManager collectionManager)
        {
            var targetEntityType = navigation.TargetEntityType;
            var pk = targetEntityType.FindPrimaryKey();
            string? pkPropertyName = pk?.Properties.Count == 1
                ? pk.Properties[0].Name
                : null;

            return new NavigationMetadata(
                collectionManager.GetCollectionName(targetEntityType.ClrType),
                navigation.IsCollection,
                targetEntityType.ClrType,
                pkPropertyName);
        }

        /// <summary>
        /// Extracts primary key property name from an entity type.
        /// </summary>
        public static string? GetPrimaryKeyPropertyName(IEntityType entityType)
        {
            var pk = entityType.FindPrimaryKey();
            return pk?.Properties.Count == 1
                ? pk.Properties[0].Name
                : null;
        }
    }
}
