using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that converts Firestore results to CLR types.
/// - Materialized DocumentSnapshots → Materialized entities or projections
/// - Scalar values → converted CLR types
/// - Handles Include by assembling navigation hierarchies (bottom→top)
/// </summary>
public class ConvertHandler : IQueryPipelineHandler
{
    private readonly IFirestoreDocumentDeserializer _deserializer;
    private readonly IProjectionMaterializer _projectionMaterializer;
    private readonly ITypeConverter _typeConverter;
    private readonly IFirestoreCollectionManager _collectionManager;

    public ConvertHandler(
        IFirestoreDocumentDeserializer deserializer,
        IProjectionMaterializer projectionMaterializer,
        ITypeConverter typeConverter,
        IFirestoreCollectionManager collectionManager)
    {
        _deserializer = deserializer;
        _projectionMaterializer = projectionMaterializer;
        _typeConverter = typeConverter;
        _collectionManager = collectionManager;
    }

    public Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        return HandleAsyncInternal(context, next, cancellationToken);
    }

    private async Task<PipelineResult> HandleAsyncInternal(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        // Scalar: convert type
        if (result is PipelineResult.Scalar scalar)
        {
            var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
            return new PipelineResult.Scalar(converted!, context);
        }

        // Materialized: deserialize entities or projections
        if (result is PipelineResult.Materialized materialized)
        {
            // Check if this is a projection query
            var projection = materialized.Context.ResolvedQuery?.Projection;
            if (projection != null)
            {
                var projectedItems = MaterializeProjections(materialized.Context, projection);
                return new PipelineResult.Materialized(projectedItems, materialized.Context);
            }

            // Entity deserialization
            if (context.EntityType != null)
            {
                var entities = DeserializeEntities(materialized.Context, context.EntityType);
                return new PipelineResult.Materialized(entities, materialized.Context);
            }
        }

        return result;
    }

    /// <summary>
    /// Materializes projection results using IProjectionMaterializer.
    /// </summary>
    private IReadOnlyList<object> MaterializeProjections(
        PipelineContext context,
        ResolvedProjectionDefinition projection)
    {
        var allSnapshots = context.GetMetadata(PipelineMetadataKeys.AllSnapshots);
        if (allSnapshots == null || allSnapshots.Count == 0)
            return Array.Empty<object>();

        var aggregations = context.GetMetadata(PipelineMetadataKeys.SubcollectionAggregations)
            ?? new Dictionary<string, object>();

        // Find root documents (entities being projected)
        var rootCollectionName = GetRootCollectionFromSnapshots(allSnapshots);
        var rootSnapshots = allSnapshots
            .Where(kv => IsRootDocument(kv.Key, rootCollectionName))
            .Select(kv => kv.Value)
            .ToList();

        return _projectionMaterializer.MaterializeMany(projection, rootSnapshots, allSnapshots, aggregations);
    }

    private static string GetRootCollectionFromSnapshots(IReadOnlyDictionary<string, DocumentSnapshot> snapshots)
    {
        // Find the shallowest document path to determine root collection
        var minDepth = int.MaxValue;
        string rootCollection = string.Empty;

        foreach (var path in snapshots.Keys)
        {
            var relativePath = ExtractRelativePath(path);
            var depth = GetPathDepth(relativePath);
            if (depth < minDepth)
            {
                minDepth = depth;
                var segments = relativePath.Split('/');
                rootCollection = segments[0];
            }
        }

        return rootCollection;
    }

    private static bool IsRootDocument(string path, string rootCollectionName)
    {
        var relativePath = ExtractRelativePath(path);
        var depth = GetPathDepth(relativePath);
        var segments = relativePath.Split('/');
        return depth == 1 && segments[0].Equals(rootCollectionName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deserializes all entities from AllSnapshots.
    /// If there are includes, deserializes bottom→top to assemble navigations.
    /// </summary>
    private IReadOnlyList<object> DeserializeEntities(PipelineContext context, Type entityType)
    {
        var allSnapshots = context.GetMetadata(PipelineMetadataKeys.AllSnapshots);
        if (allSnapshots == null || allSnapshots.Count == 0)
            return Array.Empty<object>();

        var model = context.QueryContext.Model;
        var rootEntityType = model.FindEntityType(entityType);
        if (rootEntityType == null)
            return Array.Empty<object>();

        var includes = context.ResolvedQuery?.Includes ?? Array.Empty<ResolvedInclude>();
        var rootCollectionName = _collectionManager.GetCollectionName(rootEntityType.ClrType);

        // Separate roots from related entities
        var roots = new List<DocumentSnapshot>();
        var related = new List<(string Path, string RelativePath, DocumentSnapshot Snapshot, int Depth)>();

        foreach (var kv in allSnapshots)
        {
            var relativePath = ExtractRelativePath(kv.Key);
            var depth = GetPathDepth(relativePath);
            var segments = relativePath.Split('/');

            var isRoot = depth == 1 && segments[0].Equals(rootCollectionName, StringComparison.OrdinalIgnoreCase);

            if (isRoot)
                roots.Add(kv.Value);
            else
                related.Add((kv.Key, relativePath, kv.Value, depth));
        }

        // Bottom→top: deserialize deepest first
        var relatedEntities = new Dictionary<string, object>();

        // If no related entities, simple deserialization
        if (related.Count == 0 || includes.Count == 0)
        {
            return roots.Select(doc => DeserializeEntity(doc, entityType, relatedEntities)).ToList();
        }

        foreach (var item in related.OrderByDescending(x => x.Depth))
        {
            var itemEntityType = FindEntityTypeForPath(item.RelativePath, includes, model);
            if (itemEntityType == null)
                continue;

            var entity = DeserializeEntity(item.Snapshot, itemEntityType.ClrType, relatedEntities);
            relatedEntities[item.Path] = entity;
        }

        // Deserialize roots with related entities
        return roots.Select(doc => DeserializeEntity(doc, entityType, relatedEntities)).ToList();
    }

    private object DeserializeEntity(
        DocumentSnapshot document,
        Type entityType,
        IReadOnlyDictionary<string, object> relatedEntities)
    {
        var method = typeof(IFirestoreDocumentDeserializer)
            .GetMethod(nameof(IFirestoreDocumentDeserializer.DeserializeEntity))!
            .MakeGenericMethod(entityType);

        return method.Invoke(_deserializer, new object[] { document, relatedEntities })!;
    }

    private static IEntityType? FindEntityTypeForPath(
        string path,
        IReadOnlyList<ResolvedInclude> includes,
        IModel model)
    {
        var segments = path.Split('/');
        if (segments.Length < 2)
            return null;

        var collectionName = segments[^2];
        return FindEntityTypeInIncludes(collectionName, includes, model);
    }

    private static IEntityType? FindEntityTypeInIncludes(
        string collectionName,
        IReadOnlyList<ResolvedInclude> includes,
        IModel model)
    {
        foreach (var include in includes)
        {
            if (include.CollectionPath.Equals(collectionName, StringComparison.OrdinalIgnoreCase))
                return model.FindEntityType(include.TargetEntityType);

            if (include.NestedIncludes.Count > 0)
            {
                var nested = FindEntityTypeInIncludes(collectionName, include.NestedIncludes, model);
                if (nested != null)
                    return nested;
            }
        }
        return null;
    }

    private static int GetPathDepth(string path) => path.Count(c => c == '/') / 2 + 1;

    private static string ExtractRelativePath(string fullPath)
    {
        const string marker = "/documents/";
        var index = fullPath.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? fullPath.Substring(index + marker.Length) : fullPath;
    }
}
