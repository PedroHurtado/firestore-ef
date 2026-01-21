using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that shapes snapshots and materializes results.
/// - Scalar values → converted CLR types using ITypeConverter
/// - Materialized DocumentSnapshots → shaped and materialized entities/projections
/// </summary>
public class SnapshotShapingHandler : IQueryPipelineHandler
{
    private readonly ISnapshotShaper _snapshotShaper;
    private readonly IMaterializer _materializer;
    private readonly ITypeConverter _typeConverter;

    public SnapshotShapingHandler(
        ISnapshotShaper snapshotShaper,
        IMaterializer materializer,
        ITypeConverter typeConverter)
    {
        _snapshotShaper = snapshotShaper;
        _materializer = materializer;
        _typeConverter = typeConverter;
    }

    public async Task<PipelineResult> HandleAsync(
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

        // Materialized: shape + materialize
        if (result is PipelineResult.Materialized materialized)
        {
            var resolved = context.ResolvedQuery;
            if (resolved == null)
                return result;

            var allSnapshots = materialized.Context.GetMetadata<Dictionary<string, DocumentSnapshot>>(
                PipelineMetadataKeys.AllSnapshots);

            if (allSnapshots == null || allSnapshots.Count == 0)
                return new PipelineResult.Materialized(Array.Empty<object>(), context);

            var subcollectionAggregations = materialized.Context.GetMetadata<Dictionary<string, object>>(
                PipelineMetadataKeys.SubcollectionAggregations);

            // Shape snapshots into hierarchical structure
            var debugSnapshots = allSnapshots.Values
                .OfType<DocumentSnapshot>()
                .ToList();

            var shapedResult = _snapshotShaper.Shape(resolved, debugSnapshots, subcollectionAggregations);
            // shapedResult.ToString() returns formatted output for debugging
            // Set breakpoint here to inspect shapedResult

            // Materialize shaped dictionaries into typed CLR instances
            var projectedFields = resolved.Projection?.Fields;
            var subcollections = resolved.Projection?.Subcollections;
            var materializedItems = _materializer.Materialize(shapedResult, context.ResultType, projectedFields, subcollections);
            // Set breakpoint here to inspect materializedItems

            return new PipelineResult.Materialized(materializedItems, context);
        }

        return result;
    }
}
