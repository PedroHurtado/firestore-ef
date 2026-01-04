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

        // Document query (by ID)
        if (resolved.IsDocumentQuery)
        {
            var doc = await _client.GetDocumentAsync(
                resolved.CollectionPath, resolved.DocumentId!, cancellationToken);

            if (!doc.Exists)
                return new PipelineResult.Empty(context);

            allSnapshots[doc.Reference.Path] = doc;
            await LoadIncludesAsync(doc, resolved.Includes, allSnapshots, cancellationToken);
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
            }
        }

        var contextWithSnapshots = context.WithMetadata(PipelineMetadataKeys.AllSnapshots, allSnapshots);
        var items = allSnapshots.Values.Cast<object>().ToList();
        return new PipelineResult.Materialized(items, contextWithSnapshots);
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
    /// </summary>
    private async Task LoadReferenceIncludeAsync(
        DocumentSnapshot parentDoc,
        ResolvedInclude include,
        Dictionary<string, DocumentSnapshot> allSnapshots,
        CancellationToken cancellationToken)
    {
        var data = parentDoc.ToDictionary();
        if (!data.TryGetValue(include.NavigationName, out var value) || value is not DocumentReference docRef)
            return;

        if (allSnapshots.ContainsKey(docRef.Path))
            return; // Already loaded

        var doc = await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);
        if (!doc.Exists)
            return;

        allSnapshots[doc.Reference.Path] = doc;
        await LoadIncludesAsync(doc, include.NestedIncludes, allSnapshots, cancellationToken);
    }
}
