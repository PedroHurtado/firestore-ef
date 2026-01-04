using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that converts Firestore results to CLR types.
/// - Streaming of DocumentSnapshots → Streaming of entities (via IFirestoreDocumentDeserializer)
/// - Scalar aggregation values → converted CLR types (via ITypeConverter)
/// - Min/Max Streaming → Scalar with field value extraction and empty handling
/// - Handles Include by assembling navigation hierarchies from AllSnapshots (down→top)
/// </summary>
public class ConvertHandler : IQueryPipelineHandler
{
    private readonly IFirestoreDocumentDeserializer _deserializer;
    private readonly ITypeConverter _typeConverter;

    /// <summary>
    /// Creates a new convert handler.
    /// </summary>
    /// <param name="deserializer">The document deserializer.</param>
    /// <param name="typeConverter">The type converter.</param>
    public ConvertHandler(IFirestoreDocumentDeserializer deserializer, ITypeConverter typeConverter)
    {
        _deserializer = deserializer;
        _typeConverter = typeConverter;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);
        var resolved = context.ResolvedQuery;

        // Min/Max: Streaming → Scalar (special case)
        if (resolved != null && IsMinMaxAggregation(resolved.AggregationType) &&
            result is PipelineResult.Streaming minMaxStreaming)
        {
            return await ConvertMinMaxStreamingToScalarAsync(
                minMaxStreaming, resolved, context, cancellationToken);
        }

        // Native aggregations: Scalar → converted Scalar
        if (result is PipelineResult.Scalar scalar)
        {
            var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
            return new PipelineResult.Scalar(converted!, context);
        }

        // Entity queries: Streaming of DocumentSnapshots → Streaming of entities
        if (result is PipelineResult.Streaming streaming && context.EntityType != null)
        {
            // Use the context from the result (ExecutionHandler may have added metadata like AllSnapshots)
            var resultContext = streaming.Context;

            // Check if AllSnapshots is available (includes were loaded)
            var allSnapshots = resultContext.GetMetadata(PipelineMetadataKeys.AllSnapshots);

            if (allSnapshots != null && allSnapshots.Count > 0)
            {
                // With includes: deserialize down→top, assembling navigations
                var entitiesWithIncludes = DeserializeWithIncludes(streaming.Items, resultContext, allSnapshots);
                return new PipelineResult.Streaming(entitiesWithIncludes, resultContext);
            }

            // Check if proxy factory is available in metadata
            var proxyFactory = resultContext.GetMetadata(PipelineMetadataKeys.ProxyFactory);

            var entities = proxyFactory != null
                ? DeserializeDocumentsAsProxies(streaming.Items, resultContext, proxyFactory)
                : DeserializeDocuments(streaming.Items, context.EntityType);

            return new PipelineResult.Streaming(entities, resultContext);
        }

        return result;
    }

    private static bool IsMinMaxAggregation(FirestoreAggregationType type)
    {
        return type == FirestoreAggregationType.Min || type == FirestoreAggregationType.Max;
    }

    private async IAsyncEnumerable<object> DeserializeDocumentsAsProxies(
        IAsyncEnumerable<object> documents,
        PipelineContext context,
        IProxyFactory proxyFactory)
    {
        var model = context.QueryContext.Model;
        var entityType = model.FindEntityType(context.EntityType!);

        if (entityType == null)
        {
            // Fallback to normal deserialization if entity type not found
            await foreach (var doc in documents)
            {
                yield return DeserializeEntity((DocumentSnapshot)doc, context.EntityType!);
            }
            yield break;
        }

        // Get DeserializeIntoEntity<T> method for proxy population
        var deserializeIntoMethod = typeof(IFirestoreDocumentDeserializer)
            .GetMethod(nameof(IFirestoreDocumentDeserializer.DeserializeIntoEntity))!
            .MakeGenericMethod(context.EntityType!);

        await foreach (var doc in documents)
        {
            // 1. Create empty proxy instance
            var proxy = proxyFactory.CreateProxy(entityType);

            // 2. Deserialize INTO the proxy
            deserializeIntoMethod.Invoke(_deserializer, new object[] { (DocumentSnapshot)doc, proxy });

            yield return proxy;
        }
    }

    private async Task<PipelineResult> ConvertMinMaxStreamingToScalarAsync(
        PipelineResult.Streaming streaming,
        ResolvedFirestoreQuery resolved,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        // Materialize to check if empty
        var documents = new List<object>();
        await foreach (var doc in streaming.Items.WithCancellation(cancellationToken))
        {
            documents.Add(doc);
        }

        if (documents.Count == 0)
        {
            return HandleEmptyMinMax(context);
        }

        // Extract field value from first (and only) document
        var document = (DocumentSnapshot)documents[0];
        var fieldValue = document.GetValue<object>(resolved.AggregationPropertyName!);

        // Convert to target type
        var converted = _typeConverter.Convert(fieldValue, context.ResultType);
        return new PipelineResult.Scalar(converted!, context);
    }

    private static PipelineResult HandleEmptyMinMax(PipelineContext context)
    {
        // Check if result type is nullable
        var isNullable = !context.ResultType.IsValueType ||
                         Nullable.GetUnderlyingType(context.ResultType) != null;

        if (isNullable)
        {
            // Nullable type: return null (matches EF Core behavior)
            return new PipelineResult.Scalar(null!, context);
        }

        // Non-nullable type: throw (matches EF Core behavior)
        throw new InvalidOperationException("Sequence contains no elements");
    }

    private async IAsyncEnumerable<object> DeserializeDocuments(
        IAsyncEnumerable<object> documents,
        Type entityType)
    {
        await foreach (var doc in documents)
        {
            yield return DeserializeEntity((DocumentSnapshot)doc, entityType);
        }
    }

    /// <summary>
    /// Deserializes a document to an entity using the generic DeserializeEntity method via reflection.
    /// </summary>
    private object DeserializeEntity(DocumentSnapshot document, Type entityType)
    {
        var method = typeof(IFirestoreDocumentDeserializer)
            .GetMethod(nameof(IFirestoreDocumentDeserializer.DeserializeEntity), new[] { typeof(DocumentSnapshot) })!
            .MakeGenericMethod(entityType);

        return method.Invoke(_deserializer, new object[] { document })!;
    }

    #region Include Deserialization

    /// <summary>
    /// Deserializes documents with includes.
    /// First deserializes all related entities from AllSnapshots (down→top),
    /// then deserializes root entities with navigation assignment.
    /// </summary>
    private async IAsyncEnumerable<object> DeserializeWithIncludes(
        IAsyncEnumerable<object> rootDocuments,
        PipelineContext context,
        Dictionary<string, DocumentSnapshot> allSnapshots)
    {
        var model = context.QueryContext.Model;
        var rootEntityType = model.FindEntityType(context.EntityType!);

        if (rootEntityType == null)
        {
            // Fallback to normal deserialization
            await foreach (var doc in rootDocuments)
            {
                yield return DeserializeEntity((DocumentSnapshot)doc, context.EntityType!);
            }
            yield break;
        }

        // Get includes from resolved query
        var includes = context.ResolvedQuery?.Includes ?? Array.Empty<ResolvedInclude>();
        if (includes.Count == 0)
        {
            // No includes, normal deserialization
            await foreach (var doc in rootDocuments)
            {
                yield return DeserializeEntity((DocumentSnapshot)doc, context.EntityType!);
            }
            yield break;
        }

        // First: Deserialize all related entities (down→top) into relatedEntities dictionary
        var relatedEntities = DeserializeRelatedEntities(allSnapshots, includes, model, rootEntityType);

        // Then: Deserialize each root document with relatedEntities
        await foreach (var doc in rootDocuments)
        {
            var snapshot = (DocumentSnapshot)doc;
            yield return DeserializeEntityWithRelated(snapshot, context.EntityType!, relatedEntities);
        }
    }

    /// <summary>
    /// Deserializes all related entities from AllSnapshots down→top.
    /// Groups snapshots by depth and deserializes from deepest to shallowest.
    /// </summary>
    private Dictionary<string, object> DeserializeRelatedEntities(
        Dictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyList<ResolvedInclude> includes,
        IModel model,
        IEntityType rootEntityType)
    {
        var relatedEntities = new Dictionary<string, object>();

        // Build list of all include types and their depths
        var includesByDepth = new List<(int Depth, ResolvedInclude Include)>();
        CollectIncludesWithDepth(includes, 0, includesByDepth);

        // Group snapshots by their path depth (number of '/' segments in relative path)
        // Path format: "Collection/DocId" or "Collection/DocId/SubCollection/SubDocId"
        // Full path may be: "projects/xxx/databases/xxx/documents/Collection/DocId"
        var snapshotsByDepth = allSnapshots
            .Select(kv => new { Path = kv.Key, RelativePath = ExtractRelativePath(kv.Key), Snapshot = kv.Value })
            .Select(x => new { x.Path, x.RelativePath, x.Snapshot, Depth = GetPathDepth(x.RelativePath) })
            .GroupBy(x => x.Depth)
            .OrderByDescending(g => g.Key) // Deepest first
            .ToList();

        // Deserialize from deepest to shallowest (down→top)
        foreach (var depthGroup in snapshotsByDepth)
        {
            foreach (var item in depthGroup)
            {
                // Skip if already deserialized
                if (relatedEntities.ContainsKey(item.Path))
                    continue;

                // Skip root entity documents (they'll be deserialized in the main loop)
                if (IsRootDocument(item.RelativePath, rootEntityType))
                    continue;

                // Find the entity type for this document based on its path
                var entityType = FindEntityTypeForPath(item.RelativePath, includes, model);
                if (entityType == null)
                    continue;

                // Deserialize with relatedEntities (for nested navigations)
                var entity = DeserializeEntityWithRelated(item.Snapshot, entityType.ClrType, relatedEntities);
                relatedEntities[item.Path] = entity;
            }
        }

        return relatedEntities;
    }

    /// <summary>
    /// Recursively collects includes with their depth level.
    /// </summary>
    private static void CollectIncludesWithDepth(
        IReadOnlyList<ResolvedInclude> includes,
        int depth,
        List<(int Depth, ResolvedInclude Include)> result)
    {
        foreach (var include in includes)
        {
            result.Add((depth, include));
            if (include.NestedIncludes.Count > 0)
            {
                CollectIncludesWithDepth(include.NestedIncludes, depth + 1, result);
            }
        }
    }

    /// <summary>
    /// Gets the depth of a document path (number of path segments / 2).
    /// "Collection/DocId" → depth 1
    /// "Collection/DocId/SubCollection/SubDocId" → depth 2
    /// </summary>
    private static int GetPathDepth(string path)
    {
        return path.Count(c => c == '/') / 2 + 1;
    }

    /// <summary>
    /// Checks if a document path is a root entity document.
    /// Root documents are in the root entity's collection (not FK references).
    /// </summary>
    private static bool IsRootDocument(string path, IEntityType rootEntityType)
    {
        // Root document path format: "CollectionName/DocId"
        if (GetPathDepth(path) != 1)
            return false;

        // Check if this document is in the root entity's collection
        var segments = path.Split('/');
        if (segments.Length < 2)
            return false;

        var collectionName = segments[0];
        var rootCollectionName = GetCollectionName(rootEntityType);

        // Only root if collection matches root entity's collection
        return collectionName.Equals(rootCollectionName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the collection name for an entity type from Firestore annotations or conventions.
    /// </summary>
    private static string GetCollectionName(IEntityType entityType)
    {
        // Check for FirestoreCollection annotation
        var collectionAnnotation = entityType.FindAnnotation("Firestore:Collection");
        if (collectionAnnotation?.Value is string annotationValue)
            return annotationValue;

        // Check for Table annotation
        var tableAnnotation = entityType.FindAnnotation("Relational:TableName");
        if (tableAnnotation?.Value is string tableName)
            return tableName;

        // Default: pluralize entity name
        var name = entityType.ClrType.Name;
        return Pluralize(name);
    }

    /// <summary>
    /// Simple pluralization for collection names.
    /// </summary>
    private static string Pluralize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            name.Length > 1 &&
            !IsVowel(name[name.Length - 2]))
        {
            return name.Substring(0, name.Length - 1) + "ies";
        }

        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return name + "es";

        return name + "s";
    }

    private static bool IsVowel(char c)
    {
        c = char.ToLowerInvariant(c);
        return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
    }

    /// <summary>
    /// Finds the entity type for a document based on its path and includes.
    /// </summary>
    private static IEntityType? FindEntityTypeForPath(
        string path,
        IReadOnlyList<ResolvedInclude> includes,
        IModel model)
    {
        // Extract collection name from path
        // Path format: "Collection/DocId" or "Parent/ParentId/SubCollection/DocId"
        var segments = path.Split('/');
        if (segments.Length < 2)
            return null;

        // Get the last collection name (the one containing this document)
        var collectionName = segments[^2]; // Second to last segment

        // Search for matching include by collection path
        return FindEntityTypeInIncludes(collectionName, includes, model);
    }

    /// <summary>
    /// Recursively searches includes for entity type matching collection name.
    /// </summary>
    private static IEntityType? FindEntityTypeInIncludes(
        string collectionName,
        IReadOnlyList<ResolvedInclude> includes,
        IModel model)
    {
        foreach (var include in includes)
        {
            // Check if this include's collection path matches
            if (include.CollectionPath.Equals(collectionName, StringComparison.OrdinalIgnoreCase))
            {
                return model.FindEntityType(include.TargetEntityType);
            }

            // Search nested includes
            if (include.NestedIncludes.Count > 0)
            {
                var nested = FindEntityTypeInIncludes(collectionName, include.NestedIncludes, model);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// Deserializes a document with related entities via reflection.
    /// </summary>
    private object DeserializeEntityWithRelated(
        DocumentSnapshot document,
        Type entityType,
        IReadOnlyDictionary<string, object> relatedEntities)
    {
        // Find the overload that takes relatedEntities
        var method = typeof(IFirestoreDocumentDeserializer)
            .GetMethods()
            .First(m =>
                m.Name == nameof(IFirestoreDocumentDeserializer.DeserializeEntity) &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType == typeof(IReadOnlyDictionary<string, object>))
            .MakeGenericMethod(entityType);

        return method.Invoke(_deserializer, new object[] { document, relatedEntities })!;
    }

    /// <summary>
    /// Extracts the relative path from a full Firestore document path.
    /// Converts "projects/{project}/databases/{db}/documents/Collection/DocId" to "Collection/DocId".
    /// If already a relative path, returns it as-is.
    /// </summary>
    private static string ExtractRelativePath(string fullPath)
    {
        const string documentsMarker = "/documents/";
        var index = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);
        return index >= 0 ? fullPath.Substring(index + documentsMarker.Length) : fullPath;
    }

    #endregion
}
