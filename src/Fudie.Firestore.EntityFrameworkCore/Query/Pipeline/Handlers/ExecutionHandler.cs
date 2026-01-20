using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that executes the resolved query against Firestore.
/// Returns AllSnapshots dictionary with all documents (roots + includes).
/// Aggregations return Scalar directly.
/// </summary>
public class ExecutionHandler : IQueryPipelineHandler
{
    private readonly IFirestoreClientWrapper _client;
    private readonly IQueryBuilder _queryBuilder;
    private readonly ISnapshotShaper _snapshotShaper;
    
    private ResolvedFirestoreQuery _resolved;

    public ExecutionHandler(
        IFirestoreClientWrapper client,
        IQueryBuilder queryBuilder,
        ISnapshotShaper snapshotShaper)
    {
        _client = client;
        _queryBuilder = queryBuilder;
        _snapshotShaper = snapshotShaper;
    }

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var resolved = context.ResolvedQuery!;        
        _resolved = resolved;

        // Aggregations: Count, Any, Sum, Average → Scalar
        if (resolved.IsAggregation && resolved.AggregationType != FirestoreAggregationType.Min
                                   && resolved.AggregationType != FirestoreAggregationType.Max)
        {
            return await ExecuteAggregationAsync(context, resolved, cancellationToken);
        }

        // Min/Max: Query with OrderBy + Limit(1) → Scalar
        if (resolved.AggregationType == FirestoreAggregationType.Min ||
            resolved.AggregationType == FirestoreAggregationType.Max)
        {
            return await ExecuteMinMaxAsync(context, resolved, cancellationToken);
        }

        // Entity queries: Document or Collection → Materialized with AllSnapshots
        return await ExecuteEntityQueryAsync(context, resolved, cancellationToken);
    }

    private async Task<PipelineResult> ExecuteAggregationAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var aggregateQuery = _queryBuilder.BuildAggregate(resolved);
        var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

        var value = ExtractAggregationValue(snapshot, resolved.AggregationType, resolved.AggregationPropertyName);
        return new PipelineResult.Scalar(value, context);
    }

    private async Task<PipelineResult> ExecuteMinMaxAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.Build(resolved);
        var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

        if (snapshot.Count == 0)
        {
            var isNullable = !context.ResultType.IsValueType ||
                             Nullable.GetUnderlyingType(context.ResultType) != null;
            if (isNullable)
                return new PipelineResult.Scalar(null!, context);
            throw new InvalidOperationException("Sequence contains no elements");
        }

        var doc = snapshot.Documents[0];
        var fieldValue = doc.GetValue<object>(resolved.AggregationPropertyName!);
        return new PipelineResult.Scalar(fieldValue, context);
    }

    private async Task<PipelineResult> ExecuteEntityQueryAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var allSnapshots = new Dictionary<string, DocumentSnapshot>();
        var subcollectionAggregations = new Dictionary<string, object>();

        if (resolved.IsDocumentQuery)
        {
            var doc = await _client.GetDocumentAsync(
                resolved.CollectionPath, resolved.DocumentId!, cancellationToken);

            if (!doc.Exists)
                return new PipelineResult.Empty(context);

            allSnapshots[doc.Reference.Path] = doc;
            await LoadIncludesRecursiveAsync(doc, resolved.Includes, allSnapshots, cancellationToken);
            await LoadSubcollectionProjectionsAsync(doc, resolved.Projection?.Subcollections, allSnapshots, subcollectionAggregations, cancellationToken);
        }
        else
        {
            var query = _queryBuilder.Build(resolved);
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;
                allSnapshots[doc.Reference.Path] = doc;
                await LoadIncludesRecursiveAsync(doc, resolved.Includes, allSnapshots, cancellationToken);
                await LoadSubcollectionProjectionsAsync(doc, resolved.Projection?.Subcollections, allSnapshots, subcollectionAggregations, cancellationToken);
            }
        }

        var contextWithData = context
            .WithMetadata(PipelineMetadataKeys.AllSnapshots, allSnapshots)
            .WithMetadata(PipelineMetadataKeys.SubcollectionAggregations, subcollectionAggregations);
        var items = allSnapshots.Values.Cast<object>().ToList();

        // Shape snapshots into hierarchical structure for debugging
        var debugSnapshots = allSnapshots.Values
            .OfType<Google.Cloud.Firestore.DocumentSnapshot>()
            .ToList();

        var shapedResult = _snapshotShaper.Shape(_resolved, debugSnapshots, subcollectionAggregations);
        // shapedResult.ToString() returns formatted output for debugging
        // Set breakpoint here to inspect shapedResult

        return new PipelineResult.Materialized(items, contextWithData);
    }

    /// <summary>
    /// Loads all includes recursively for a parent document.
    /// Handles both SubCollection and Reference includes inline.
    /// </summary>
    private async Task LoadIncludesRecursiveAsync(
        DocumentSnapshot parentDoc,
        IReadOnlyList<ResolvedInclude> includes,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        if (includes.Count == 0)
            return;

        foreach (var include in includes)
        {
            if (include.IsCollection)
            {
                // Optimization: If filtering by PK only, use GetDocumentAsync instead of query
                if (include.IsDocumentQuery)
                {
                    var subCollectionRef = parentDoc.Reference.Collection(include.CollectionPath);
                    var docRef = subCollectionRef.Document(include.DocumentId!);
                    var doc = await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);

                    if (doc.Exists)
                    {
                        allSnapshots[doc.Reference.Path] = doc;
                        await LoadIncludesRecursiveAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
                    }
                }
                else
                {
                    // SubCollection: query N documents
                    var query = _queryBuilder.BuildInclude(parentDoc.Reference.Path, include);
                    var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

                    foreach (var doc in snapshot.Documents)
                    {
                        if (!doc.Exists) continue;
                        allSnapshots[doc.Reference.Path] = doc;
                        await LoadIncludesRecursiveAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
                    }
                }
            }
            else
            {
                // Reference: FK → single document or ArrayOf References → multiple documents
                var data = parentDoc.ToDictionary();

                // Check if it's an array of DocumentReferences (ArrayOf Reference)
                var docRefs = GetNestedDocumentReferences(data, include.NavigationName);
                if (docRefs != null && docRefs.Count > 0)
                {
                    // ArrayOf References: load each referenced document
                    foreach (var docRef in docRefs)
                    {
                        if (allSnapshots.ContainsKey(docRef.Path))
                            continue;

                        var doc = await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);
                        if (!doc.Exists)
                            continue;

                        allSnapshots[doc.Reference.Path] = doc;
                        await LoadIncludesRecursiveAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
                    }
                }
                else
                {
                    // Single reference: FK → single document
                    var docRef = GetNestedDocumentReference(data, include.NavigationName);
                    if (docRef == null || allSnapshots.ContainsKey(docRef.Path))
                        continue;

                    var doc = await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);
                    if (!doc.Exists)
                        continue;

                    allSnapshots[doc.Reference.Path] = doc;
                    await LoadIncludesRecursiveAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Gets a DocumentReference from a nested path in the document data.
    /// Supports paths like "ComplexTypeProp.ReferenceProp".
    /// </summary>
    private static DocumentReference? GetNestedDocumentReference(
        IDictionary<string, object> data,
        string navigationPath)
    {
        object? current = data;

        foreach (var part in navigationPath.Split('.'))
        {
            if (current is IDictionary<string, object> dict && dict.TryGetValue(part, out current))
                continue;
            return null;
        }

        return current as DocumentReference;
    }

    /// <summary>
    /// Gets an array of DocumentReferences from a nested path in the document data.
    /// Used for ArrayOf Reference properties (e.g., HashSet&lt;Proveedor&gt;).
    /// Also handles paths through arrays (e.g., "Secciones.EtiquetasDestacadas" where Secciones is an array).
    /// </summary>
    private static List<DocumentReference>? GetNestedDocumentReferences(
        IDictionary<string, object> data,
        string navigationPath)
    {
        var parts = navigationPath.Split('.');
        return GetNestedDocumentReferencesRecursive(data, parts, 0);
    }

    /// <summary>
    /// Recursively navigates the path, handling both dictionaries and arrays.
    /// Collects DocumentReferences from the final path segment, whether single or array.
    /// </summary>
    private static List<DocumentReference>? GetNestedDocumentReferencesRecursive(
        object? current,
        string[] pathParts,
        int partIndex)
    {
        if (current == null)
            return null;

        // End of path: check what we have
        if (partIndex >= pathParts.Length)
        {
            // Single DocumentReference
            if (current is DocumentReference singleRef)
            {
                return [singleRef];
            }

            // Array of DocumentReferences
            if (current is IEnumerable<object> enumerable && current is not string)
            {
                var refs = new List<DocumentReference>();
                foreach (var item in enumerable)
                {
                    if (item is DocumentReference docRef)
                        refs.Add(docRef);
                }
                return refs.Count > 0 ? refs : null;
            }
            return null;
        }

        var part = pathParts[partIndex];

        // Case 1: Current is a dictionary - navigate into it
        if (current is IDictionary<string, object> dict)
        {
            if (dict.TryGetValue(part, out var next))
            {
                return GetNestedDocumentReferencesRecursive(next, pathParts, partIndex + 1);
            }
            return null;
        }

        // Case 2: Current is an array - iterate and collect from each element
        if (current is IEnumerable<object> array && current is not string)
        {
            var allRefs = new List<DocumentReference>();
            foreach (var item in array)
            {
                // Each item should be a dictionary (map/embedded object)
                if (item is IDictionary<string, object> itemDict)
                {
                    if (itemDict.TryGetValue(part, out var next))
                    {
                        var refs = GetNestedDocumentReferencesRecursive(next, pathParts, partIndex + 1);
                        if (refs != null)
                        {
                            allRefs.AddRange(refs);
                        }
                    }
                }
            }
            return allRefs.Count > 0 ? allRefs : null;
        }

        return null;
    }

    /// <summary>
    /// Extracts the aggregation value from an AggregateQuerySnapshot.
    /// For main queries, Average throws on empty. For subcollections, returns 0.0.
    /// </summary>
    private static object ExtractAggregationValue(
        AggregateQuerySnapshot snapshot,
        FirestoreAggregationType aggregationType,
        string? propertyName,
        bool throwOnEmptyAverage = true)
    {
        return aggregationType switch
        {
            FirestoreAggregationType.Count => snapshot.Count ?? 0L,
            FirestoreAggregationType.Any => (snapshot.Count ?? 0L) > 0,
            FirestoreAggregationType.Sum => snapshot.GetValue<double?>(
                AggregateField.Sum(propertyName!)) ?? 0.0,
            FirestoreAggregationType.Average => ExtractAverage(snapshot, propertyName!, throwOnEmptyAverage),
            _ => throw new NotSupportedException($"Aggregation {aggregationType} not supported")
        };
    }

    private static object ExtractAverage(AggregateQuerySnapshot snapshot, string propertyName, bool throwOnEmpty)
    {
        var value = snapshot.GetValue<double?>(AggregateField.Average(propertyName));
        if (value == null)
        {
            if (throwOnEmpty)
                throw new InvalidOperationException("Sequence contains no elements");
            return 0.0;
        }
        return value.Value;
    }

    /// <summary>
    /// Loads all projection subcollections for a parent document.
    /// For aggregations, executes aggregate query and stores result.
    /// For regular queries, loads documents recursively.
    /// </summary>
    private async Task LoadSubcollectionProjectionsAsync(
        DocumentSnapshot parentDoc,
        IReadOnlyList<ResolvedSubcollectionProjection>? subcollections,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        Dictionary<string, object> aggregations,
        CancellationToken cancellationToken)
    {
        if (subcollections == null || subcollections.Count == 0)
            return;

        foreach (var subcollection in subcollections)
        {
            if (subcollection.IsAggregation)
            {
                // Aggregation: Sum, Count, Average
                var aggregateQuery = _queryBuilder.BuildSubcollectionAggregate(parentDoc.Reference.Path, subcollection);
                var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

                var key = $"{parentDoc.Reference.Path}:{subcollection.ResultName}";
                aggregations[key] = ExtractAggregationValue(
                    snapshot,
                    subcollection.Aggregation ?? FirestoreAggregationType.Count,
                    subcollection.AggregationPropertyName,
                    throwOnEmptyAverage: false);
            }
            else
            {
                // Regular query: load documents
                var query = _queryBuilder.BuildSubcollectionQuery(parentDoc.Reference.Path, subcollection);
                var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

                foreach (var doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    allSnapshots[doc.Reference.Path] = doc;

                    // Load FK references within subcollection (e.g., Ejemplar.Libro)
                    await LoadIncludesRecursiveAsync(doc, subcollection.Includes, allSnapshots, cancellationToken);

                    await LoadSubcollectionProjectionsAsync(doc, subcollection.NestedSubcollections, allSnapshots, aggregations, cancellationToken);
                }
            }
        }
    }
}