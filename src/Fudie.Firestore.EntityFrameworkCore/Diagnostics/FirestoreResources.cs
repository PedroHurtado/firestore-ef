using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fudie.Firestore.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Resources for Firestore logging definitions.
/// Creates and caches EventDefinition instances for each Firestore event.
/// </summary>
public static class FirestoreResources
{
    /// <summary>
    /// Executing Firestore query: {query}
    /// </summary>
    public static EventDefinition<string> LogQueryExecuting(IDiagnosticsLogger logger)
    {
        var definition = ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryExecuting;

        if (definition == null)
        {
            definition = new EventDefinition<string>(
                logger.Options,
                FirestoreEventId.QueryExecuting,
                LogLevel.Debug,
                "FirestoreEventId.QueryExecuting",
                level => LoggerMessage.Define<string>(
                    level,
                    FirestoreEventId.QueryExecuting,
                    "Executing Firestore query: {query}"));

            ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryExecuting = definition;
        }

        return (EventDefinition<string>)definition;
    }

    /// <summary>
    /// Executed Firestore query on '{collectionPath}' in {elapsed}ms, returned {count} documents.
    /// </summary>
    public static EventDefinition<string, double, int> LogQueryExecuted(IDiagnosticsLogger logger)
    {
        var definition = ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryExecuted;

        if (definition == null)
        {
            definition = new EventDefinition<string, double, int>(
                logger.Options,
                FirestoreEventId.QueryExecuted,
                LogLevel.Information,
                "FirestoreEventId.QueryExecuted",
                level => LoggerMessage.Define<string, double, int>(
                    level,
                    FirestoreEventId.QueryExecuted,
                    "Executed Firestore query on '{collectionPath}' in {elapsed:0.0}ms, returned {count} document(s)"));

            ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryExecuted = definition;
        }

        return (EventDefinition<string, double, int>)definition;
    }

    /// <summary>
    /// Logs a formatted Firestore query message.
    /// </summary>
    public static EventDefinition<string> LogFirestoreQuery(IDiagnosticsLogger logger)
    {
        var definition = ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryMessage;

        if (definition == null)
        {
            definition = new EventDefinition<string>(
                logger.Options,
                FirestoreEventId.QueryExecuted,
                LogLevel.Information,
                "FirestoreEventId.QueryExecuted",
                level => LoggerMessage.Define<string>(
                    level,
                    FirestoreEventId.QueryExecuted,
                    "{Message}"));

            ((FirestoreLoggingDefinitions)logger.Definitions).LogQueryMessage = definition;
        }

        return (EventDefinition<string>)definition;
    }

    /// <summary>
    /// Logs a Firestore command execution (Insert, Update, Delete).
    /// </summary>
    public static EventDefinition<string> LogFirestoreCommand(IDiagnosticsLogger logger)
    {
        var definition = ((FirestoreLoggingDefinitions)logger.Definitions).LogCommandExecuted;

        if (definition == null)
        {
            definition = new EventDefinition<string>(
                logger.Options,
                FirestoreEventId.CommandExecuted,
                LogLevel.Information,
                "FirestoreEventId.CommandExecuted",
                level => LoggerMessage.Define<string>(
                    level,
                    FirestoreEventId.CommandExecuted,
                    "{Message}"));

            ((FirestoreLoggingDefinitions)logger.Definitions).LogCommandExecuted = definition;
        }

        return (EventDefinition<string>)definition;
    }
}
