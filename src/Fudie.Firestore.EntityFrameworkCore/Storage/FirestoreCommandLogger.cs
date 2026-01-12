using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Text;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Implementation of IFirestoreCommandLogger using EF Core diagnostics pattern.
/// Logs Insert, Update, Delete operations to the configured log output.
/// </summary>
public class FirestoreCommandLogger : IFirestoreCommandLogger
{
    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _logger;
    private readonly FirestorePipelineOptions _options;

    public FirestoreCommandLogger(
        IDiagnosticsLogger<DbLoggerCategory.Database.Command> logger,
        FirestorePipelineOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public void LogInsert(string collectionPath, string documentId, Type entityType, TimeSpan elapsed)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("INSERT", collectionPath, documentId, entityType, elapsed);
        _logger.FirestoreCommand(message);
    }

    /// <inheritdoc />
    public void LogUpdate(string collectionPath, string documentId, Type entityType, TimeSpan elapsed)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("UPDATE", collectionPath, documentId, entityType, elapsed);
        _logger.FirestoreCommand(message);
    }

    /// <inheritdoc />
    public void LogDelete(string collectionPath, string documentId, Type entityType, TimeSpan elapsed)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("DELETE", collectionPath, documentId, entityType, elapsed);
        _logger.FirestoreCommand(message);
    }

    private static string FormatCommandMessage(
        string operation,
        string collectionPath,
        string documentId,
        Type entityType,
        TimeSpan elapsed)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append($"Firestore {operation}: {collectionPath}/{documentId}");
        sb.AppendLine();
        sb.Append($"  Entity: {entityType.Name} ({elapsed.TotalMilliseconds:0.0}ms)");
        return sb.ToString();
    }
}
