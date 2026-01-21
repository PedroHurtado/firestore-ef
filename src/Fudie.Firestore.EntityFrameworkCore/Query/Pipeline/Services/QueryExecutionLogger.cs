using System;
using System.IO;
using System.Linq;
using System.Text;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Logger for query execution debugging.
/// Writes ResolvedQuery and ShapedResult to a file for analysis.
/// The log file is cleared on first write of each test run.
/// </summary>
public static class QueryExecutionLogger
{
    private static readonly object _lock = new();
    private static bool _initialized = false;
    private static readonly string LogFilePath;

    static QueryExecutionLogger()
    {
        // Log file in the solution root
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Navigate up from bin/Debug/net8.0 to solution root
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        LogFilePath = Path.Combine(solutionRoot, "query-execution.log");
    }

    /// <summary>
    /// Logs query execution details including ResolvedQuery and ShapedResult.
    /// </summary>
    public static void Log(
        ResolvedFirestoreQuery resolved,
        ShapedResult shapedResult,
        Type resultType,
        int materializedCount)
    {
        lock (_lock)
        {
            try
            {
                // Clear file on first write of this run
                if (!_initialized)
                {
                    File.WriteAllText(LogFilePath, $"=== Query Execution Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
                    _initialized = true;
                }

                var sb = new StringBuilder();
                sb.AppendLine(new string('=', 80));
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] Query Execution");
                sb.AppendLine(new string('-', 80));

                // Result type
                sb.AppendLine($"ResultType: {GetTypeName(resultType)}");
                sb.AppendLine();

                // ResolvedQuery (AST)
                sb.AppendLine(">>> RESOLVED QUERY (AST):");
                sb.AppendLine(resolved.ToString());

                // ShapedResult
                sb.AppendLine(">>> SHAPED RESULT:");
                sb.AppendLine(shapedResult.ToString());

                // Materialized count
                sb.AppendLine($">>> MATERIALIZED: {materializedCount} items");
                sb.AppendLine();

                File.AppendAllText(LogFilePath, sb.ToString());
            }
            catch
            {
                // Silently ignore logging errors to not affect tests
            }
        }
    }

    /// <summary>
    /// Logs an error during materialization.
    /// </summary>
    public static void LogError(
        ResolvedFirestoreQuery resolved,
        ShapedResult shapedResult,
        Type resultType,
        Exception ex)
    {
        lock (_lock)
        {
            try
            {
                if (!_initialized)
                {
                    File.WriteAllText(LogFilePath, $"=== Query Execution Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
                    _initialized = true;
                }

                var sb = new StringBuilder();
                sb.AppendLine(new string('=', 80));
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] !!! ERROR !!!");
                sb.AppendLine(new string('-', 80));

                sb.AppendLine($"ResultType: {GetTypeName(resultType)}");
                sb.AppendLine();

                sb.AppendLine(">>> RESOLVED QUERY (AST):");
                sb.AppendLine(resolved.ToString());

                sb.AppendLine(">>> SHAPED RESULT:");
                sb.AppendLine(shapedResult.ToString());

                sb.AppendLine(">>> EXCEPTION:");
                sb.AppendLine($"Type: {ex.GetType().Name}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
                sb.AppendLine();

                File.AppendAllText(LogFilePath, sb.ToString());
            }
            catch
            {
                // Silently ignore
            }
        }
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            var baseName = genericDef.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
                baseName = baseName.Substring(0, tickIndex);
            return $"{baseName}<{genericArgs}>";
        }

        if (type.Name.StartsWith("<>") || type.Name.Contains("AnonymousType"))
            return "AnonymousType";

        return type.Name;
    }

    /// <summary>
    /// Resets the logger state (for testing purposes).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _initialized = false;
        }
    }
}
