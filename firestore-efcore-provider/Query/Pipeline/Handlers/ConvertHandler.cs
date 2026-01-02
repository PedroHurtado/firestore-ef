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
/// Handler that converts Firestore results to CLR types.
/// - Streaming of DocumentSnapshots → Streaming of entities (via IDocumentDeserializer)
/// - Scalar aggregation values → converted CLR types (via ITypeConverter)
/// - Min/Max Streaming → Scalar with field value extraction and empty handling
/// </summary>
public class ConvertHandler : IQueryPipelineHandler
{
    private readonly IDocumentDeserializer _deserializer;
    private readonly ITypeConverter _typeConverter;

    /// <summary>
    /// Creates a new convert handler.
    /// </summary>
    /// <param name="deserializer">The document deserializer.</param>
    /// <param name="typeConverter">The type converter.</param>
    public ConvertHandler(IDocumentDeserializer deserializer, ITypeConverter typeConverter)
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
            var entities = DeserializeDocuments(streaming.Items, context.EntityType);
            return new PipelineResult.Streaming(entities, context);
        }

        return result;
    }

    private static bool IsMinMaxAggregation(FirestoreAggregationType type)
    {
        return type == FirestoreAggregationType.Min || type == FirestoreAggregationType.Max;
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
            yield return _deserializer.Deserialize((DocumentSnapshot)doc, entityType);
        }
    }
}
