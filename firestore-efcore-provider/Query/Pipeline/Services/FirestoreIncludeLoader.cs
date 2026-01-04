using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Executes resolved includes for eager loading (Include/ThenInclude).
/// Uses ResolvedInclude (already resolved by Resolver) - converts to ResolvedFirestoreQuery
/// and executes via sub-pipeline for tracking and proxying.
/// Registered as Singleton - dependencies passed at runtime to avoid circular DI.
/// </summary>
public class FirestoreIncludeLoader : IIncludeLoader
{
    private readonly IFirestoreCollectionManager _collectionManager;

    public FirestoreIncludeLoader(IFirestoreCollectionManager collectionManager)
    {
        _collectionManager = collectionManager;
    }

    public async Task LoadIncludeAsync(
        object entity,
        IEntityType entityType,
        ResolvedInclude resolvedInclude,
        IQueryPipelineMediator mediator,
        IFirestoreQueryContext queryContext,
        CancellationToken cancellationToken)
    {
        // Get the parent document path (may be null for top-level entities)
        var parentDocPath = GetParentDocumentPath(entity, entityType);

        await LoadIncludeWithPathAsync(
            entity, entityType, resolvedInclude, mediator, queryContext, parentDocPath, cancellationToken);
    }

    private async Task LoadIncludeWithPathAsync(
        object entity,
        IEntityType entityType,
        ResolvedInclude resolvedInclude,
        IQueryPipelineMediator mediator,
        IFirestoreQueryContext queryContext,
        string? parentDocPath,
        CancellationToken cancellationToken)
    {
        var navigation = entityType.FindNavigation(resolvedInclude.NavigationName);
        if (navigation == null)
            return;

        // Build the collection path for this include
        var collectionPath = BuildCollectionPath(entity, entityType, navigation, resolvedInclude, parentDocPath);

        // Convert ResolvedInclude → ResolvedFirestoreQuery with proper collection path
        var resolvedQuery = ToResolvedQuery(entity, collectionPath, navigation, resolvedInclude);

        // Execute sub-pipeline
        var context = new PipelineContext
        {
            Ast = null!,
            QueryContext = queryContext,
            IsTracking = true,
            ResultType = resolvedInclude.TargetEntityType,
            Kind = QueryKind.Entity,
            EntityType = resolvedInclude.TargetEntityType,
            ResolvedQuery = resolvedQuery
        };

        var results = new List<object>();
        await foreach (var item in mediator.ExecuteAsync<object>(context, cancellationToken))
        {
            // Apply fixup - establish the inverse relationship
            ApplyFixup(entity, item, navigation);
            results.Add(item);
        }

        AssignNavigation(entity, navigation, results);

        // Nested includes - pass the document path (collection + item ID) as parent for nested
        if (resolvedInclude.NestedIncludes.Count > 0)
        {
            foreach (var item in results)
            {
                // Build the document path for this item: collection/itemId
                var itemId = GetEntityId(item, navigation.TargetEntityType);
                var itemDocPath = itemId != null ? $"{collectionPath}/{itemId}" : collectionPath;

                foreach (var nested in resolvedInclude.NestedIncludes)
                {
                    await LoadIncludeWithPathAsync(
                        item, navigation.TargetEntityType, nested, mediator, queryContext, itemDocPath, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Gets the parent document path for an entity (e.g., "Clientes/cli-001").
    /// Returns null for top-level entities that don't have a parent path tracked.
    /// </summary>
    private string? GetParentDocumentPath(object entity, IEntityType entityType)
    {
        var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);
        var entityId = GetEntityId(entity, entityType);
        if (entityId == null)
            return null;

        return $"{collectionName}/{entityId}";
    }

    /// <summary>
    /// Builds the collection path for a navigation.
    /// For SubCollections: uses parent doc path + subcollection name
    /// For References: uses target collection name
    /// </summary>
    private string BuildCollectionPath(
        object entity,
        IEntityType entityType,
        INavigation navigation,
        ResolvedInclude include,
        string? parentDocPath)
    {
        if (include.IsCollection && navigation.IsSubCollection())
        {
            // SubCollection: path is {parentDocPath}/{subCollectionName}
            // e.g., "Clientes/cli-001/Pedidos" or "Clientes/cli-001/Pedidos/ped-001/Lineas"
            if (parentDocPath != null)
            {
                return $"{parentDocPath}/{include.CollectionPath}";
            }
            else
            {
                // Fallback: build from entity type
                var parentCollectionName = _collectionManager.GetCollectionName(entityType.ClrType);
                var parentDocId = GetEntityId(entity, entityType);
                return $"{parentCollectionName}/{parentDocId}/{include.CollectionPath}";
            }
        }
        else
        {
            // DocumentReference or other: use target collection directly
            return include.CollectionPath;
        }
    }

    /// <summary>
    /// Converts ResolvedInclude to ResolvedFirestoreQuery with the given collection path.
    /// </summary>
    private static ResolvedFirestoreQuery ToResolvedQuery(
        object entity,
        string collectionPath,
        INavigation navigation,
        ResolvedInclude include)
    {
        // For References, get DocumentId from FK; for SubCollections, use null (path is already scoped)
        string? documentId = null;
        if (!include.IsCollection && navigation.IsDocumentReference())
        {
            // Get the FK value from the entity to determine which document to load
            documentId = include.DocumentId ?? GetDocumentIdForReference(entity, navigation);
        }

        // No FK filter needed for SubCollections - the path already scopes to parent
        var filters = new List<ResolvedFilterResult>();
        filters.AddRange(include.FilterResults);

        return new ResolvedFirestoreQuery(
            CollectionPath: collectionPath,
            EntityClrType: include.TargetEntityType,
            DocumentId: documentId,
            FilterResults: filters,
            OrderByClauses: include.OrderByClauses,
            Pagination: include.Pagination,
            StartAfterCursor: null,
            Includes: [],
            AggregationType: FirestoreAggregationType.None,
            AggregationPropertyName: null,
            AggregationResultType: null,
            Projection: null,
            ReturnDefault: false,
            ReturnType: null);
    }

    /// <summary>
    /// Gets the document ID from an entity.
    /// </summary>
    private static string? GetEntityId(object entity, IEntityType entityType)
    {
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey == null || primaryKey.Properties.Count == 0)
            return null;

        var pkProperty = primaryKey.Properties[0];
        var pkValue = pkProperty.GetGetter().GetClrValue(entity);
        return pkValue?.ToString();
    }

    private static string? GetDocumentIdForReference(object entity, INavigation navigation)
    {
        // For DocumentReferences, the related entity's ID is stored in Firestore
        // as a DocumentReference in a field with the navigation name.
        // The deserializer extracts this and sets the FK value.

        // First, try to get the ID from the navigation property itself
        // (if the related entity is already loaded)
        var navPropertyInfo = navigation.PropertyInfo;
        if (navPropertyInfo != null)
        {
            var relatedEntity = navPropertyInfo.GetValue(entity);
            if (relatedEntity != null)
            {
                // Get the ID from the related entity
                var targetEntityType = navigation.TargetEntityType;
                var pk = targetEntityType.FindPrimaryKey();
                if (pk?.Properties.Count > 0)
                {
                    var pkProperty = pk.Properties[0];
                    if (pkProperty.PropertyInfo != null)
                    {
                        var idValue = pkProperty.PropertyInfo.GetValue(relatedEntity);
                        return idValue?.ToString();
                    }
                }
            }
        }

        // If navigation is not loaded, try FK property
        var fkProperty = navigation.ForeignKey.Properties.FirstOrDefault();
        if (fkProperty == null)
            return null;

        try
        {
            // Check if the FK property exists in the CLR type
            if (fkProperty.PropertyInfo?.GetMethod != null)
            {
                var fkValue = fkProperty.PropertyInfo.GetValue(entity);
                return fkValue?.ToString();
            }

            // For backing fields
            var fieldInfo = fkProperty.FieldInfo;
            if (fieldInfo != null)
            {
                var fkValue = fieldInfo.GetValue(entity);
                return fkValue?.ToString();
            }
        }
        catch
        {
            // Ignore - FK not accessible
        }

        return null;
    }

    private static void AssignNavigation(object entity, INavigation navigation, IReadOnlyList<object> items)
    {
        var prop = navigation.PropertyInfo;
        if (prop == null) return;

        if (navigation.IsCollection)
        {
            var propertyType = prop.PropertyType;
            var elementType = navigation.ClrType.IsGenericType
                ? navigation.ClrType.GetGenericArguments()[0]
                : typeof(object);

            // Create the appropriate collection type based on property type
            object collection;
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                // HashSet<T>
                var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
                collection = Activator.CreateInstance(hashSetType)!;
                var addMethod = hashSetType.GetMethod("Add")!;
                foreach (var item in items)
                {
                    addMethod.Invoke(collection, new[] { item });
                }
            }
            else
            {
                // List<T> or ICollection<T> - use List<T>
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var item in items) list.Add(item);
                collection = list;
            }

            prop.SetValue(entity, collection);
        }
        else
        {
            prop.SetValue(entity, items.Count > 0 ? items[0] : null);
        }
    }

    /// <summary>
    /// Establishes the inverse relationship between parent and child entities.
    /// This is required for EF Core to properly track the relationship and build
    /// the correct document path for SubCollections during delete operations.
    /// </summary>
    private static void ApplyFixup(object parent, object child, INavigation navigation)
    {
        if (navigation.Inverse == null) return;

        var inverseProperty = navigation.Inverse.PropertyInfo;
        if (inverseProperty == null) return;

        if (navigation.IsCollection)
        {
            // Parent has collection, child has single reference back to parent
            // e.g., Cliente.Pedidos -> Pedido.Cliente
            inverseProperty.SetValue(child, parent);
        }
        else
        {
            // Parent has single reference, check inverse type
            if (navigation.Inverse.IsCollection)
            {
                var collection = inverseProperty.GetValue(parent) as System.Collections.IList;
                if (collection != null && !collection.Contains(child))
                {
                    collection.Add(child);
                }
            }
            else
            {
                inverseProperty.SetValue(parent, child);
            }
        }
    }
}
