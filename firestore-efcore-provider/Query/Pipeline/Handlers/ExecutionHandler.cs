using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that executes the resolved query against Firestore.
/// Returns AllSnapshots dictionary with all documents (roots + includes).
/// Aggregations return Scalar directly.
/// </summary>
public class ExecutionHandler : IQueryPipelineHandler
{
    private readonly IFirestoreClientWrapper _client;
    private readonly IQueryBuilder _queryBuilder;

    public ExecutionHandler(IFirestoreClientWrapper client, IQueryBuilder queryBuilder)
    {
        _client = client;
        _queryBuilder = queryBuilder;
    }

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var resolved = context.ResolvedQuery!;

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

        var value = resolved.AggregationType switch
        {
            FirestoreAggregationType.Count => snapshot.Count ?? 0L,
            FirestoreAggregationType.Any => (snapshot.Count ?? 0L) > 0,
            FirestoreAggregationType.Sum => snapshot.GetValue<double?>(
                AggregateField.Sum(resolved.AggregationPropertyName!)) ?? 0.0,
            FirestoreAggregationType.Average => ExtractAverage(snapshot, resolved.AggregationPropertyName!),
            _ => snapshot.Count ?? 0L
        };

        return new PipelineResult.Scalar(value, context);
    }

    private static object ExtractAverage(AggregateQuerySnapshot snapshot, string propertyName)
    {
        var value = snapshot.GetValue<double?>(AggregateField.Average(propertyName));
        if (value == null)
            throw new InvalidOperationException("Sequence contains no elements");
        return value.Value;
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
            // Empty sequence handling
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

        // Document query (by ID)
        if (resolved.IsDocumentQuery)
        {
            var doc = await _client.GetDocumentAsync(
                resolved.CollectionPath, resolved.DocumentId!, cancellationToken);

            if (!doc.Exists)
                return new PipelineResult.Empty(context);

            allSnapshots[doc.Reference.Path] = doc;
            await LoadIncludesAsync(doc, resolved.Includes, allSnapshots, cancellationToken);
            await LoadProjectionSubcollectionsAsync(doc, resolved.Projection?.Subcollections, allSnapshots, subcollectionAggregations, cancellationToken);
        }
        // Collection query
        else
        {
            var query = _queryBuilder.Build(resolved);
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;
                allSnapshots[doc.Reference.Path] = doc;
                await LoadIncludesAsync(doc, resolved.Includes, allSnapshots, cancellationToken);
                await LoadProjectionSubcollectionsAsync(doc, resolved.Projection?.Subcollections, allSnapshots, subcollectionAggregations, cancellationToken);
            }
        }

        var contextWithData = context
            .WithMetadata(PipelineMetadataKeys.AllSnapshots, allSnapshots)
            .WithMetadata(PipelineMetadataKeys.SubcollectionAggregations, subcollectionAggregations);
        var items = allSnapshots.Values.Cast<object>().ToList();
        return new PipelineResult.Materialized(items, contextWithData);
    }

    /// <summary>
    /// Loads all includes recursively for a parent document.
    /// </summary>
    private async Task LoadIncludesAsync(
        DocumentSnapshot parentDoc,
        IReadOnlyList<ResolvedInclude> includes,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        if (includes.Count == 0)
            return;

        foreach (var include in includes)
        {
            await LoadIncludeAsync(parentDoc, include, allSnapshots, cancellationToken);
        }
    }

    /// <summary>
    /// Loads a single include (SubCollection or Reference).
    /// </summary>
    private async Task LoadIncludeAsync(
        DocumentSnapshot parentDoc,
        ResolvedInclude include,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        if (include.IsCollection)
        {
            await LoadSubCollectionIncludeAsync(parentDoc, include, allSnapshots, cancellationToken);
        }
        else
        {
            await LoadReferenceIncludeAsync(parentDoc, include, allSnapshots, cancellationToken);
        }
    }

    /// <summary>
    /// Loads a SubCollection include (query N documents).
    /// </summary>
    private async Task LoadSubCollectionIncludeAsync(
        DocumentSnapshot parentDoc,
        ResolvedInclude include,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildInclude(parentDoc.Reference.Path, include);
        var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

        foreach (var doc in snapshot.Documents)
        {
            if (!doc.Exists) continue;
            allSnapshots[doc.Reference.Path] = doc;
            await LoadIncludesAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
        }
    }

    /// <summary>
    /// Loads a Reference include (FK → single document).
    /// Supports nested paths like "DireccionPrincipal.SucursalCercana" for ComplexType References.
    /// </summary>
    private async Task LoadReferenceIncludeAsync(
        DocumentSnapshot parentDoc,
        ResolvedInclude include,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        var data = parentDoc.ToDictionary();
        var docRef = GetNestedDocumentReference(data, include.NavigationName);
        if (docRef == null)
            return;

        if (allSnapshots.ContainsKey(docRef.Path))
            return; // Already loaded

        var doc = await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);
        if (!doc.Exists)
            return;

        allSnapshots[doc.Reference.Path] = doc;
        await LoadIncludesAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
    }

    /// <summary>
    /// Gets a DocumentReference from a path in the document data.
    /// Supports nested paths like "ComplexTypeProp.ReferenceProp".
    /// </summary>
    private static DocumentReference? GetNestedDocumentReference(
        IDictionary<string, object> data,
        string navigationPath)
    {
        var parts = navigationPath.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is IDictionary<string, object> dict)
            {
                if (!dict.TryGetValue(part, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current as DocumentReference;
    }

    /// <summary>
    /// Loads all projection subcollections for a parent document.
    /// </summary>
    private async Task LoadProjectionSubcollectionsAsync(
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
            await LoadSubcollectionProjectionAsync(parentDoc, subcollection, allSnapshots, aggregations, cancellationToken);
        }
    }

    /// <summary>
    /// Loads a single subcollection projection.
    /// For aggregations, executes aggregate query and stores result.
    /// For regular queries, loads documents.
    /// </summary>
    private async Task LoadSubcollectionProjectionAsync(
        DocumentSnapshot parentDoc,
        ResolvedSubcollectionProjection subcollection,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        Dictionary<string, object> aggregations,
        CancellationToken cancellationToken)
    {
        if (subcollection.IsAggregation)
        {
            await LoadSubcollectionAggregationAsync(parentDoc, subcollection, aggregations, cancellationToken);
        }
        else
        {
            var query = _queryBuilder.BuildSubcollectionQuery(parentDoc.Reference.Path, subcollection);
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;
                allSnapshots[doc.Reference.Path] = doc;
                await LoadProjectionSubcollectionsAsync(doc, subcollection.NestedSubcollections, allSnapshots, aggregations, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes a subcollection aggregation query (Sum, Count, Average).
    /// Results are stored in the aggregations dictionary for later materialization.
    /// </summary>
    private async Task LoadSubcollectionAggregationAsync(
        DocumentSnapshot parentDoc,
        ResolvedSubcollectionProjection subcollection,
        Dictionary<string, object> aggregations,
        CancellationToken cancellationToken)
    {
        var aggregateQuery = _queryBuilder.BuildSubcollectionAggregate(parentDoc.Reference.Path, subcollection);
        var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

        // Key format: "{parentDocPath}:{subcollection.ResultName}"
        var key = $"{parentDoc.Reference.Path}:{subcollection.ResultName}";

        object value = subcollection.Aggregation switch
        {
            Ast.FirestoreAggregationType.Count => snapshot.Count ?? 0L,
            Ast.FirestoreAggregationType.Sum => snapshot.GetValue<double?>(
                AggregateField.Sum(subcollection.AggregationPropertyName!)) ?? 0.0,
            Ast.FirestoreAggregationType.Average => snapshot.GetValue<double?>(
                AggregateField.Average(subcollection.AggregationPropertyName!)) ?? 0.0,
            _ => throw new NotSupportedException($"Subcollection aggregation {subcollection.Aggregation} not supported")
        };

        aggregations[key] = value;
    }
}
