using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, query.ToString());
        }

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new EventData(
                definition,
                (d, _) => ((EventDefinition<string>)d).GenerateMessage(query.ToString()));

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
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

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new EventData(
                definition,
                (d, _) => ((EventDefinition<string, double, int>)d).GenerateMessage(
                    query.CollectionPath, duration.TotalMilliseconds, documentCount));

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    /// <summary>
    /// Logs a formatted Firestore query message.
    /// Used by LogQueryHandler for detailed query logging.
    /// </summary>
    public static void FirestoreQuery(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        string message)
    {
        var definition = FirestoreResources.LogFirestoreQuery(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, message);
        }

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new EventData(
                definition,
                (d, _) => message);

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    /// <summary>
    /// Logs a formatted Firestore command message (Insert, Update, Delete).
    /// </summary>
    public static void FirestoreCommand(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string message)
    {
        var definition = FirestoreResources.LogFirestoreCommand(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, message);
        }

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new EventData(
                definition,
                (d, _) => message);

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }
}