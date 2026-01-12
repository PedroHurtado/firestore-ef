using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;

namespace Fudie.Firestore.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Extension methods for logging Firestore events using EF Core diagnostics.
/// </summary>
public static class FirestoreLoggerExtensions
{
    /// <summary>
    /// Logs that a Firestore query is about to be executed.
    /// </summary>
    public static void QueryExecuting(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        ResolvedFirestoreQuery query)
    {
        var definition = FirestoreResources.LogQueryExecuting(diagnostics);

        // ShouldLog checks ConfigureWarnings settings
        // For custom events, we need explicit configuration or use Logger directly
        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, query.ToString());
        }
        else if (diagnostics.Logger.IsEnabled(LogLevel.Debug))
        {
            // Fallback: log directly if Debug level is enabled
            diagnostics.Logger.Log(
                LogLevel.Debug,
                FirestoreEventId.QueryExecuting,
                "Executing Firestore query: {Query}",
                query.ToString());
        }
    }

    /// <summary>
    /// Logs that a Firestore query has been executed.
    /// </summary>
    public static void QueryExecuted(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        ResolvedFirestoreQuery query,
        TimeSpan duration,
        int documentCount)
    {
        var definition = FirestoreResources.LogQueryExecuted(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, query.CollectionPath, duration.TotalMilliseconds, documentCount);
        }
        else if (diagnostics.Logger.IsEnabled(LogLevel.Information))
        {
            // Fallback: log directly if Information level is enabled
            diagnostics.Logger.Log(
                LogLevel.Information,
                FirestoreEventId.QueryExecuted,
                "Executed Firestore query on '{CollectionPath}' in {Elapsed:0.0}ms, returned {Count} document(s)",
                query.CollectionPath,
                duration.TotalMilliseconds,
                documentCount);
        }
    }
}
