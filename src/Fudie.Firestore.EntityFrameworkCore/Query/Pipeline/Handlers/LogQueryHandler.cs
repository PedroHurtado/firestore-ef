using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that logs Firestore query execution with configurable detail level.
/// Should be placed after ResolverHandler and before ExecutionHandler.
/// </summary>
/// <remarks>
/// Configure logging level via FirestoreDbContextOptionsBuilder:
/// <code>
/// options.UseFirestore("project-id", firestore =>
/// {
///     firestore.QueryLogLevel(QueryLogLevel.Ids); // None, Count, Ids, Full
/// });
/// </code>
/// </remarks>
public class LogQueryHandler : IQueryPipelineHandler
{
    private readonly FirestorePipelineOptions _options;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _logger;

    public LogQueryHandler(
        FirestorePipelineOptions options,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
        {
            return await next(context, cancellationToken);
        }

        var resolved = context.ResolvedQuery;
        if (resolved == null)
        {
            return await next(context, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await next(context, cancellationToken);
        stopwatch.Stop();

        // Get AllSnapshots from result context
        var allSnapshots = result.Context.GetMetadata(PipelineMetadataKeys.AllSnapshots);

        LogQueryResult(resolved, allSnapshots, stopwatch.Elapsed, result);

        return result;
    }

    private void LogQueryResult(
        Query.Resolved.ResolvedFirestoreQuery resolved,
        Dictionary<string, DocumentSnapshot>? allSnapshots,
        TimeSpan elapsed,
        PipelineResult result)
    {
        var sb = new StringBuilder();

        // Query description (from ResolvedFirestoreQuery.ToString())
        sb.AppendLine();
        sb.Append(resolved.ToString().TrimEnd());

        // Result info
        if (result is PipelineResult.Scalar scalar)
        {
            sb.AppendLine();
            sb.Append($"  Result: {scalar.Value} ({elapsed.TotalMilliseconds:0.0}ms)");
        }
        else
        {
            var docCount = allSnapshots?.Count ?? 0;
            sb.AppendLine();
            sb.Append($"  {docCount} doc(s) ({elapsed.TotalMilliseconds:0.0}ms)");

            // IDs (if level >= Ids)
            if (_options.QueryLogLevel >= QueryLogLevel.Ids && allSnapshots != null && allSnapshots.Count > 0)
            {
                var ids = allSnapshots.Values.Select(d => d.Id);
                sb.AppendLine();
                sb.Append($"  [{string.Join(", ", ids)}]");
            }
        }

        _logger.FirestoreQuery(sb.ToString());

        // Full data (if level == Full)
        if (_options.QueryLogLevel == QueryLogLevel.Full && allSnapshots != null)
        {
            foreach (var doc in allSnapshots.Values)
            {
                LogDocumentData(doc);
            }
        }
    }

    private void LogDocumentData(DocumentSnapshot doc)
    {
        try
        {
            var data = doc.ToDictionary();
            var converted = ConvertForSerialization(data);
            var json = JsonSerializer.Serialize(converted, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var sb = new StringBuilder();
            sb.AppendLine($"  {doc.Id}:");
            foreach (var line in json.Split('\n'))
            {
                sb.AppendLine($"    {line}");
            }
            _logger.FirestoreQuery(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.FirestoreQuery($"  {doc.Id}: [Error: {ex.Message}]");
        }
    }

    private static object? ConvertForSerialization(object? value)
    {
        return value switch
        {
            null => null,
            DocumentReference docRef => $"ref:{docRef.Path}",
            Timestamp ts => ts.ToDateTime().ToString("O"),
            GeoPoint geo => $"geo:{geo.Latitude},{geo.Longitude}",
            IDictionary<string, object> dict => dict.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertForSerialization(kvp.Value)),
            IEnumerable<object> list => list.Select(ConvertForSerialization).ToList(),
            _ => value
        };
    }
}
