using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Implementation of IFirestoreCommandLogger using EF Core diagnostics pattern.
/// Logs Insert, Update, Delete operations to the configured log output.
/// Data is always logged regardless of QueryLogLevel setting.
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
    public void LogInsert(string collectionPath, string documentId, Type entityType, TimeSpan elapsed, Dictionary<string, object>? data = null)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("INSERT", collectionPath, documentId, entityType, elapsed, data);
        _logger.FirestoreCommand(message);
    }

    /// <inheritdoc />
    public void LogUpdate(string collectionPath, string documentId, Type entityType, TimeSpan elapsed, Dictionary<string, object>? data = null)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("UPDATE", collectionPath, documentId, entityType, elapsed, data);
        _logger.FirestoreCommand(message);
    }

    /// <inheritdoc />
    public void LogDelete(string collectionPath, string documentId, Type entityType, TimeSpan elapsed)
    {
        if (_options.QueryLogLevel == QueryLogLevel.None)
            return;

        var message = FormatCommandMessage("DELETE", collectionPath, documentId, entityType, elapsed, null);
        _logger.FirestoreCommand(message);
    }

    private static string FormatCommandMessage(
        string operation,
        string collectionPath,
        string documentId,
        Type entityType,
        TimeSpan elapsed,
        Dictionary<string, object>? data)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append($"Firestore {operation}: {collectionPath}/{documentId}");
        sb.AppendLine();
        sb.Append($"  Entity: {entityType.Name} ({elapsed.TotalMilliseconds:0.0}ms)");

        if (data != null && data.Count > 0)
        {
            sb.AppendLine();
            sb.Append("  Data:");
            sb.Append(FormatData(data));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats dictionary data as indented JSON for readability.
    /// Handles Firestore-specific types like DocumentReference and GeoPoint.
    /// </summary>
    private static string FormatData(Dictionary<string, object> data)
    {
        var convertedData = ConvertForSerialization(data);

        try
        {
            var json = JsonSerializer.Serialize(convertedData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Indent each line with 4 spaces for alignment under "Data:"
            var lines = json.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.AppendLine();
                sb.Append("    ");
                sb.Append(line.TrimEnd('\r'));
            }
            return sb.ToString();
        }
        catch
        {
            // Fallback if serialization fails
            return " " + FormatDataFallback(data);
        }
    }

    /// <summary>
    /// Converts Firestore-specific types to serializable representations.
    /// </summary>
    private static object? ConvertForSerialization(object? value)
    {
        if (value == null)
            return null;

        // Handle FieldValue sentinels (ServerTimestamp, Delete, ArrayUnion, ArrayRemove)
        // FieldValue is a static class, so we detect its instances by type name
        var valueType = value.GetType();
        if (valueType.FullName?.StartsWith("Google.Cloud.Firestore.FieldValue") == true ||
            valueType.Name.Contains("SentinelValue"))
        {
            return ConvertFieldValueForSerialization(value);
        }

        // Handle DocumentReference
        if (value is DocumentReference docRef)
        {
            return $"ref:{GetRelativeDocumentPath(docRef.Path)}";
        }

        // Handle GeoPoint
        if (value is GeoPoint geoPoint)
        {
            return new { lat = geoPoint.Latitude, lng = geoPoint.Longitude };
        }

        // Handle Timestamp
        if (value is Timestamp timestamp)
        {
            return timestamp.ToDateTime().ToString("O");
        }

        // Handle DateTime
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("O");
        }

        // Handle Dictionary
        if (value is IDictionary<string, object> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kvp in dict)
            {
                result[kvp.Key] = ConvertForSerialization(kvp.Value);
            }
            return result;
        }

        // Handle arrays/lists (but not strings)
        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ConvertForSerialization(item));
            }
            return list;
        }

        // Return primitive types as-is
        return value;
    }

    /// <summary>
    /// Converts FieldValue sentinels to readable string representations.
    /// Uses reflection since FieldValue internals are not public.
    /// </summary>
    private static object ConvertFieldValueForSerialization(object fieldValueObj)
    {
        var type = fieldValueObj.GetType();
        var typeName = type.Name;

        // Try to get the Kind property first (available on internal types)
        var kindProperty = type.GetProperty("Kind", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var kindValue = kindProperty?.GetValue(fieldValueObj)?.ToString() ?? "";

        // Check by Kind enum value or type name
        if (kindValue.Contains("ServerTimestamp") || typeName.Contains("ServerTimestamp"))
        {
            return "FieldValue.ServerTimestamp()";
        }

        if (kindValue.Contains("Delete") || typeName.Contains("Delete"))
        {
            return "FieldValue.Delete()";
        }

        // For ArrayUnion and ArrayRemove, try to extract the values
        var valuesProperty = type.GetProperty("Values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var values = valuesProperty?.GetValue(fieldValueObj) as IEnumerable;

        if (kindValue.Contains("ArrayUnion") || typeName.Contains("ArrayUnion"))
        {
            if (values != null)
            {
                var convertedValues = new List<object?>();
                foreach (var item in values)
                {
                    convertedValues.Add(ConvertForSerialization(item));
                }
                return new Dictionary<string, object?>
                {
                    ["FieldValue.ArrayUnion"] = convertedValues
                };
            }
            return "FieldValue.ArrayUnion([...])";
        }

        if (kindValue.Contains("ArrayRemove") || typeName.Contains("ArrayRemove"))
        {
            if (values != null)
            {
                var convertedValues = new List<object?>();
                foreach (var item in values)
                {
                    convertedValues.Add(ConvertForSerialization(item));
                }
                return new Dictionary<string, object?>
                {
                    ["FieldValue.ArrayRemove"] = convertedValues
                };
            }
            return "FieldValue.ArrayRemove([...])";
        }

        // Fallback: return type name for debugging
        return $"FieldValue.{typeName}()";
    }

    /// <summary>
    /// Extracts relative path from full Firestore document path.
    /// </summary>
    private static string GetRelativeDocumentPath(string fullPath)
    {
        const string documentsMarker = "/documents/";
        var index = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            return fullPath.Substring(index + documentsMarker.Length);
        }
        return fullPath;
    }

    /// <summary>
    /// Fallback formatting when JSON serialization fails.
    /// </summary>
    private static string FormatDataFallback(Dictionary<string, object> data)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kvp in data)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append($"\"{kvp.Key}\": ");
            sb.Append(FormatValueFallback(kvp.Value));
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string FormatValueFallback(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return $"\"{s}\"";
        if (value is bool b) return b ? "true" : "false";

        // Handle FieldValue sentinels
        var valueType = value.GetType();
        if (valueType.FullName?.StartsWith("Google.Cloud.Firestore.FieldValue") == true ||
            valueType.Name.Contains("SentinelValue"))
        {
            return $"\"{ConvertFieldValueForSerialization(value)}\"";
        }

        if (value is DocumentReference docRef) return $"\"ref:{GetRelativeDocumentPath(docRef.Path)}\"";
        if (value is GeoPoint geoPoint) return $"{{\"lat\":{geoPoint.Latitude},\"lng\":{geoPoint.Longitude}}}";
        if (value is DateTime dt) return $"\"{dt:O}\"";
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(FormatValueFallback(item));
            }
            return $"[{string.Join(",", items)}]";
        }
        return value.ToString() ?? "null";
    }
}
