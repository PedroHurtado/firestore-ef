using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that shapes snapshots into hierarchical structure for debugging.
/// Executes after ExecutionHandler and passes results through unchanged.
/// </summary>
public class SnapshotShapingHandler : IQueryPipelineHandler
{
    private readonly ISnapshotShaper _snapshotShaper;

    public SnapshotShapingHandler(ISnapshotShaper snapshotShaper)
    {
        _snapshotShaper = snapshotShaper;
    }

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        // Only shape for materialized results (entity queries)
        if (result is PipelineResult.Materialized materialized)
        {
            var resolved = context.ResolvedQuery;
            if (resolved != null)
            {
                var allSnapshots = materialized.Context.GetMetadata<Dictionary<string, DocumentSnapshot>>(
                    PipelineMetadataKeys.AllSnapshots);
                var subcollectionAggregations = materialized.Context.GetMetadata<Dictionary<string, object>>(
                    PipelineMetadataKeys.SubcollectionAggregations);

                if (allSnapshots != null)
                {
                    // Shape snapshots into hierarchical structure for debugging
                    var debugSnapshots = allSnapshots.Values
                        .OfType<DocumentSnapshot>()
                        .ToList();

                    var shapedResult = _snapshotShaper.Shape(resolved, debugSnapshots, subcollectionAggregations);
                    // shapedResult.ToString() returns formatted output for debugging
                    // Set breakpoint here to inspect shapedResult
                }
            }
        }

        return result;
    }
}