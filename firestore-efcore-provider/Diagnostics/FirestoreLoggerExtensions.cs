using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;

namespace Firestore.EntityFrameworkCore.Diagnostics;

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
    }
}
