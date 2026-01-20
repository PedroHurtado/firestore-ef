using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Google.Cloud.Firestore;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Wrapper class for shaped query results that provides formatted ToString() for debugging.
/// </summary>
public class ShapedResult(List<Dictionary<string, object?>> items)
{
    public List<Dictionary<string, object?>> Items { get; init; } = items;

    public override string ToString()
    {
        if (Items.Count == 0)
            return "[]";

        var sb = new StringBuilder();
        for (var i = 0; i < Items.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.AppendLine($"[{i}]");
            FormatDictionary(sb, Items[i], 1);
        }
        return sb.ToString();
    }

    private static void FormatDictionary(StringBuilder sb, Dictionary<string, object?> dict, int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var kvp in dict)
        {
            FormatValue(sb, kvp.Key, kvp.Value, prefix, indent);
        }
    }

    private static void FormatValue(StringBuilder sb, string key, object? value, string prefix, int indent)
    {
        switch (value)
        {
            case null:
                sb.AppendLine($"{prefix}{key}: null");
                break;

            case Dictionary<string, object?> nested:
                sb.AppendLine($"{prefix}{key}:");
                FormatDictionary(sb, nested, indent + 1);
                break;

            case IList<Dictionary<string, object?>> listOfDicts:
                sb.AppendLine($"{prefix}{key}: [{listOfDicts.Count}]");
                for (var i = 0; i < listOfDicts.Count; i++)
                {
                    sb.AppendLine($"{prefix}  [{i}]");
                    FormatDictionary(sb, listOfDicts[i], indent + 2);
                }
                break;

            case IList<object> list:
                sb.AppendLine($"{prefix}{key}: [{list.Count}]");
                for (var i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item is Dictionary<string, object?> itemDict)
                    {
                        sb.AppendLine($"{prefix}  [{i}]");
                        FormatDictionary(sb, itemDict, indent + 2);
                    }
                    else
                    {
                        sb.AppendLine($"{prefix}  [{i}] {FormatSimpleValue(item)}");
                    }
                }
                break;

            case DocumentReference docRef:
                sb.AppendLine($"{prefix}{key}: Ref({docRef.Id})");
                break;

            case Timestamp ts:
                sb.AppendLine($"{prefix}{key}: {ts.ToDateTime():yyyy-MM-dd HH:mm:ss}");
                break;

            case string s:
                sb.AppendLine($"{prefix}{key}: \"{s}\"");
                break;

            default:
                sb.AppendLine($"{prefix}{key}: {value}");
                break;
        }
    }

    private static string FormatSimpleValue(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        DocumentReference docRef => $"Ref({docRef.Id})",
        Timestamp ts => ts.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
        _ => value.ToString() ?? "null"
    };
}

/// <summary>
/// Holds parsed information about a DocumentSnapshot for efficient indexing and lookup.
/// </summary>
public record SnapshotInfo(
    DocumentSnapshot Snapshot,
    string CollectionPath,
    string DocumentId,
    string? ParentDocumentPath);

/// <summary>
/// Immutable context for shaping operations. Holds indexed snapshots and aggregations.
/// </summary>
internal record ShapingContext(
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByCollection,
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByParentPath,
    IReadOnlyDictionary<string, object> Aggregations)
{
    public static ShapingContext Create(
        IReadOnlyList<DocumentSnapshot> snapshots,
        Dictionary<string, object>? aggregations)
    {
        var byCollection = new Dictionary<string, List<SnapshotInfo>>();
        var byParentPath = new Dictionary<string, List<SnapshotInfo>>();

        foreach (var snapshot in snapshots)
        {
            var info = ParseSnapshotPath(snapshot);

            // Index by collection
            if (!byCollection.TryGetValue(info.CollectionPath, out var collectionList))
            {
                collectionList = new List<SnapshotInfo>();
                byCollection[info.CollectionPath] = collectionList;
            }
            collectionList.Add(info);

            // Index by parent path (for subcollections)
            if (info.ParentDocumentPath != null)
            {
                if (!byParentPath.TryGetValue(info.ParentDocumentPath, out var parentList))
                {
                    parentList = new List<SnapshotInfo>();
                    byParentPath[info.ParentDocumentPath] = parentList;
                }
                parentList.Add(info);
            }
        }

        return new ShapingContext(byCollection, byParentPath, aggregations ?? new Dictionary<string, object>());
    }

    private static SnapshotInfo ParseSnapshotPath(DocumentSnapshot snapshot)
    {
        var fullPath = snapshot.Reference.Path;
        var documentsMarker = "/documents/";
        var documentsIndex = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);

        if (documentsIndex < 0)
            throw new InvalidOperationException($"Invalid Firestore path format: {fullPath}");

        var relativePath = fullPath.Substring(documentsIndex + documentsMarker.Length);
        var segments = relativePath.Split('/');

        if (segments.Length < 2 || segments.Length % 2 != 0)
            throw new InvalidOperationException($"Invalid Firestore path format: {fullPath}");

        var collectionPath = segments[^2];
        var documentId = segments[^1];

        string? parentDocumentPath = null;
        if (segments.Length > 2)
        {
            parentDocumentPath = string.Join("/", segments.Take(segments.Length - 2));
        }

        return new SnapshotInfo(snapshot, collectionPath, documentId, parentDocumentPath);
    }
}

/// <summary>
/// Transforms a flat list of DocumentSnapshots into a hierarchical dictionary structure
/// based on the query's AST (ResolvedFirestoreQuery).
/// </summary>
public class SnapshotShaper : ISnapshotShaper
{
    /// <inheritdoc />
    public ShapedResult Shape(
        ResolvedFirestoreQuery query,
        IReadOnlyList<DocumentSnapshot> snapshots,
        Dictionary<string, object>? aggregations = null)
    {
        if (snapshots.Count == 0)
            return new ShapedResult([]);

        var context = ShapingContext.Create(snapshots, aggregations);
        var roots = FindRoots(context, query);

        if (roots.Count == 0)
            return new ShapedResult([]);

        var projectionSubcollections = query.Projection?.Subcollections ?? [];
        var pkName = query.PrimaryKeyPropertyName;
        var includePk = ShouldIncludePk(query.Projection?.Fields, pkName);

        return new ShapedResult(
            roots.Select(root => ShapeNode(context, root, query.Includes, projectionSubcollections, includePk, pkName)).ToList());
    }

    private static bool ShouldIncludePk(IReadOnlyList<FirestoreProjectedField>? fields, string? pkName) =>
        fields == null || (pkName != null && fields.Any(f => f.FieldPath == pkName));

    private static List<SnapshotInfo> FindRoots(ShapingContext context, ResolvedFirestoreQuery query)
    {
        if (!context.ByCollection.TryGetValue(query.CollectionPath, out var candidates))
            return [];

        if (query.DocumentId != null)
            return candidates.Where(c => c.DocumentId == query.DocumentId).ToList();

        return candidates.Where(c => c.ParentDocumentPath == null).ToList();
    }

    private static Dictionary<string, object?> ShapeNode(
        ShapingContext context,
        SnapshotInfo info,
        IReadOnlyList<ResolvedInclude> includes,
        IReadOnlyList<ResolvedSubcollectionProjection>? projectionSubcollections = null,
        bool includePk = true,
        string? pkName = null)
    {
        var dict = info.Snapshot.ToDictionary();
        var nodePath = $"{info.CollectionPath}/{info.DocumentId}";

        if (includePk && pkName != null)
        {
            var result = new Dictionary<string, object?> { [pkName] = info.DocumentId };
            foreach (var kvp in dict)
                result[kvp.Key] = kvp.Value;
            dict = result;
        }

        foreach (var include in includes)
        {
            if (include.IsCollection)
            {
                var childPkName = include.PrimaryKeyPropertyName;
                var childIncludePk = ShouldIncludePk(null, childPkName); // No projection fields for includes
                dict[include.NavigationName] = ResolveChildren(
                    context, nodePath, include.CollectionPath, include.NestedIncludes, null, childIncludePk, childPkName);
            }
            else
            {
                ResolveReferenceInPlace(context, dict, include);
            }
        }

        if (projectionSubcollections != null)
        {
            foreach (var subcol in projectionSubcollections)
            {
                dict[subcol.ResultName] = ResolveProjectionSubcollection(context, nodePath, subcol);
            }
        }

        return dict;
    }

    private static object? ResolveProjectionSubcollection(
        ShapingContext context,
        string parentPath,
        ResolvedSubcollectionProjection subcol)
    {
        if (subcol.IsAggregation)
        {
            var fullParentPath = FindFullParentPath(context, parentPath);
            if (fullParentPath == null)
                return null;

            var key = $"{fullParentPath}:{subcol.ResultName}";
            return context.Aggregations.TryGetValue(key, out var value) ? value : null;
        }

        var pkName = subcol.PrimaryKeyPropertyName;
        var includePk = ShouldIncludePk(subcol.Fields, pkName);
        return ResolveChildren(context, parentPath, subcol.CollectionPath, subcol.Includes, subcol.NestedSubcollections, includePk, pkName);
    }

    private static List<Dictionary<string, object?>> ResolveChildren(
        ShapingContext context,
        string parentPath,
        string collectionPath,
        IReadOnlyList<ResolvedInclude> includes,
        IReadOnlyList<ResolvedSubcollectionProjection>? projectionSubcollections,
        bool includePk,
        string? pkName)
    {
        if (!context.ByParentPath.TryGetValue(parentPath, out var children))
            return [];

        return children
            .Where(c => c.CollectionPath == collectionPath)
            .Select(child => ShapeNode(context, child, includes, projectionSubcollections, includePk, pkName))
            .ToList();
    }

    private static string? FindFullParentPath(ShapingContext context, string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Length < 2)
            return null;

        var collectionPath = parts[0];
        var documentId = parts[1];

        if (context.ByCollection.TryGetValue(collectionPath, out var candidates))
        {
            var found = candidates.FirstOrDefault(c => c.DocumentId == documentId);
            if (found != null)
                return found.Snapshot.Reference.Path;
        }

        return null;
    }

    private static void ResolveReferenceInPlace(
        ShapingContext context,
        Dictionary<string, object?> dict,
        ResolvedInclude include)
    {
        var pathParts = include.NavigationName.Split('.');
        ResolveReferenceRecursive(context, dict, pathParts, 0, include);
    }

    private static void ResolveReferenceRecursive(
        ShapingContext context,
        object? current,
        string[] pathParts,
        int partIndex,
        ResolvedInclude include)
    {
        if (current == null || partIndex >= pathParts.Length)
            return;

        var part = pathParts[partIndex];
        var isLastPart = partIndex == pathParts.Length - 1;

        if (current is IDictionary<string, object?> dict)
        {
            if (!dict.TryGetValue(part, out var value) || value == null)
                return;

            if (isLastPart)
            {
                dict[part] = ResolveReferenceValue(context, value, include);
            }
            else
            {
                ResolveReferenceRecursive(context, value, pathParts, partIndex + 1, include);
            }
        }
        else if (current is IEnumerable<object> array && current is not string)
        {
            foreach (var item in array)
            {
                ResolveReferenceRecursive(context, item, pathParts, partIndex, include);
            }
        }
    }

    private static object? ResolveReferenceValue(
        ShapingContext context,
        object? value,
        ResolvedInclude include)
    {
        if (value is DocumentReference docRef)
        {
            return FindAndShapeReference(context, docRef, include);
        }

        if (value is IEnumerable<object> list && value is not string)
        {
            var results = new List<Dictionary<string, object?>>();
            foreach (var item in list)
            {
                if (item is DocumentReference itemRef)
                {
                    var shaped = FindAndShapeReference(context, itemRef, include);
                    if (shaped != null)
                        results.Add(shaped);
                }
            }
            return results.Count > 0 ? results : null;
        }

        return null;
    }

    private static Dictionary<string, object?>? FindAndShapeReference(
        ShapingContext context,
        DocumentReference docRef,
        ResolvedInclude include)
    {
        var refDocId = docRef.Id;

        if (!context.ByCollection.TryGetValue(include.CollectionPath, out var candidates))
            return null;

        var found = candidates.FirstOrDefault(c => c.DocumentId == refDocId);
        if (found == null)
            return null;

        var pkName = include.PrimaryKeyPropertyName;
        var includePk = ShouldIncludePk(null, pkName); // No projection fields for references
        return ShapeNode(context, found, include.NestedIncludes, null, includePk, pkName);
    }
}