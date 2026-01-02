using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that executes the resolved query against Firestore.
/// Returns DocumentSnapshots as a streaming result for further processing.
/// </summary>
public class ExecutionHandler : IQueryPipelineHandler
{
    private readonly IFirestoreClientWrapper _client;
    private readonly IQueryBuilder _queryBuilder;

    /// <summary>
    /// Creates a new execution handler.
    /// </summary>
    /// <param name="client">The Firestore client wrapper.</param>
    /// <param name="queryBuilder">The query builder.</param>
    public ExecutionHandler(IFirestoreClientWrapper client, IQueryBuilder queryBuilder)
    {
        _client = client;
        _queryBuilder = queryBuilder;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var resolved = context.ResolvedQuery!;

        // Document query optimization
        if (resolved.IsDocumentQuery)
        {
            return await ExecuteDocumentQueryAsync(context, resolved, cancellationToken);
        }

        // Min/Max are NOT native Firestore aggregations - they use OrderBy + Limit(1)
        if (resolved.AggregationType == FirestoreAggregationType.Min ||
            resolved.AggregationType == FirestoreAggregationType.Max)
        {
            return await ExecuteMinMaxQueryAsync(context, resolved, cancellationToken);
        }

        // Aggregation query (Count, Any, Sum, Average)
        if (resolved.IsAggregation)
        {
            return await ExecuteAggregationQueryAsync(context, resolved, cancellationToken);
        }

        // Collection query
        return await ExecuteCollectionQueryAsync(context, resolved, cancellationToken);
    }

    private async Task<PipelineResult> ExecuteDocumentQueryAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var doc = await _client.GetDocumentAsync(
            resolved.CollectionPath,
            resolved.DocumentId!,
            cancellationToken);

        if (!doc.Exists)
        {
            return new PipelineResult.Empty(context);
        }

        var items = SingleDocumentAsyncEnumerable(doc);
        return new PipelineResult.Streaming(items, context);
    }

    private async Task<PipelineResult> ExecuteAggregationQueryAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.Build(resolved);
        var aggregateQuery = BuildAggregateQuery(query, resolved);
        var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

        var value = ExtractAggregationValue(snapshot, resolved);
        return new PipelineResult.Scalar(value, context);
    }

    private async Task<PipelineResult> ExecuteMinMaxQueryAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        // Min/Max are NOT native Firestore aggregations.
        // Executed as: SELECT field FROM collection ORDER BY field [ASC|DESC] LIMIT 1
        var query = _queryBuilder.Build(resolved);

        var orderedQuery = resolved.AggregationType == FirestoreAggregationType.Min
            ? query.Select(resolved.AggregationPropertyName!).OrderBy(resolved.AggregationPropertyName!).Limit(1)
            : query.Select(resolved.AggregationPropertyName!).OrderByDescending(resolved.AggregationPropertyName!).Limit(1);

        var snapshot = await _client.ExecuteQueryAsync(orderedQuery, cancellationToken);

        // Return Streaming - ConvertHandler will extract the field value and handle empty sequence
        var items = DocumentsAsyncEnumerable(snapshot);
        return new PipelineResult.Streaming(items, context);
    }

    private async Task<PipelineResult> ExecuteCollectionQueryAsync(
        PipelineContext context,
        ResolvedFirestoreQuery resolved,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.Build(resolved);
        var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

        var items = DocumentsAsyncEnumerable(snapshot);
        return new PipelineResult.Streaming(items, context);
    }

    private static AggregateQuery BuildAggregateQuery(Google.Cloud.Firestore.Query query, ResolvedFirestoreQuery resolved)
    {
        // Note: Min/Max are handled separately in ExecuteMinMaxQueryAsync
        return resolved.AggregationType switch
        {
            FirestoreAggregationType.Count => query.Count(),
            FirestoreAggregationType.Any => query.Count(), // Any uses Count > 0
            FirestoreAggregationType.Sum => query.Aggregate(AggregateField.Sum(resolved.AggregationPropertyName!)),
            FirestoreAggregationType.Average => query.Aggregate(AggregateField.Average(resolved.AggregationPropertyName!)),
            _ => throw new System.NotSupportedException($"Aggregation type {resolved.AggregationType} is not supported as a native Firestore aggregation")
        };
    }

    private static object ExtractAggregationValue(AggregateQuerySnapshot snapshot, ResolvedFirestoreQuery resolved)
    {
        return resolved.AggregationType switch
        {
            FirestoreAggregationType.Count => snapshot.Count ?? 0L,
            FirestoreAggregationType.Any => (snapshot.Count ?? 0L) > 0,
            FirestoreAggregationType.Sum => snapshot.GetValue<double?>(resolved.AggregationPropertyName!) ?? 0.0,
            FirestoreAggregationType.Average => snapshot.GetValue<double?>(resolved.AggregationPropertyName!) ?? 0.0,
            _ => snapshot.Count ?? 0L
        };
    }

    private static async IAsyncEnumerable<object> SingleDocumentAsyncEnumerable(DocumentSnapshot doc)
    {
        yield return doc;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<object> DocumentsAsyncEnumerable(QuerySnapshot snapshot)
    {
        foreach (var doc in snapshot.Documents)
        {
            yield return doc;
        }
        await Task.CompletedTask;
    }
}
