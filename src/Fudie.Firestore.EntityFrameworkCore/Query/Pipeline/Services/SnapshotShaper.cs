using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
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
/// <param name="Snapshot">The original Firestore DocumentSnapshot.</param>
/// <param name="CollectionPath">The collection name (e.g., "MenuCategories").</param>
/// <param name="DocumentId">The document ID (e.g., "cat1").</param>
/// <param name="ParentDocumentPath">The parent document path (e.g., "Menus/abc123"), or null for root collections.</param>
public record SnapshotInfo(
    DocumentSnapshot Snapshot,
    string CollectionPath,
    string DocumentId,
    string? ParentDocumentPath);

/// <summary>
/// Transforms a flat list of DocumentSnapshots into a hierarchical dictionary structure
/// based on the query's AST (ResolvedFirestoreQuery).
/// </summary>
public class SnapshotShaper : ISnapshotShaper
{
    private Dictionary<string, List<SnapshotInfo>> _byCollection = new();
    private Dictionary<string, List<SnapshotInfo>> _byParentPath = new();
    private Dictionary<string, object> _aggregations = new();

    /// <inheritdoc />
    public ShapedResult Shape(
        ResolvedFirestoreQuery query,
        IReadOnlyList<DocumentSnapshot> snapshots,
        Dictionary<string, object>? aggregations = null)
    {
        if (snapshots.Count == 0)
            return new ShapedResult([]);

        // Store aggregations for lookup
        _aggregations = aggregations ?? new Dictionary<string, object>();

        // Index all snapshots for efficient lookup
        IndexSnapshots(snapshots);

        // Find root documents matching the query
        var roots = FindRoots(query);

        if (roots.Count == 0)
            return new ShapedResult([]);

        // Get projection subcollections if any
        var projectionSubcollections = query.Projection?.Subcollections ?? [];

        // Check if Id should be included (only if no projection or Id is in projected fields)
        var includeId = !query.HasProjection ||
                        query.Projection!.Fields == null ||
                        query.Projection.Fields.Any(f => f.FieldPath == "Id");

        // Shape each root with its includes and projection subcollections
        return new ShapedResult(roots.Select(root => ShapeNode(root, query.Includes, projectionSubcollections, includeId)).ToList());
    }

    /// <summary>
    /// Indexes all snapshots by collection path and parent document path for efficient lookup.
    /// </summary>
    private void IndexSnapshots(IReadOnlyList<DocumentSnapshot> snapshots)
    {
        _byCollection.Clear();
        _byParentPath.Clear();

        foreach (var snapshot in snapshots)
        {
            var info = ParseSnapshotPath(snapshot);

            // Index by collection
            if (!_byCollection.TryGetValue(info.CollectionPath, out var collectionList))
            {
                collectionList = new List<SnapshotInfo>();
                _byCollection[info.CollectionPath] = collectionList;
            }
            collectionList.Add(info);

            // Index by parent path (for subcollections)
            if (info.ParentDocumentPath != null)
            {
                if (!_byParentPath.TryGetValue(info.ParentDocumentPath, out var parentList))
                {
                    parentList = new List<SnapshotInfo>();
                    _byParentPath[info.ParentDocumentPath] = parentList;
                }
                parentList.Add(info);
            }
        }
    }

    /// <summary>
    /// Parses a DocumentSnapshot's reference path to extract collection, document ID, and parent path.
    /// </summary>
    /// <remarks>
    /// Example paths:
    /// - "projects/demo/databases/(default)/documents/Menus/abc123"
    ///   → CollectionPath: "Menus", DocumentId: "abc123", ParentDocumentPath: null
    /// - "projects/demo/databases/(default)/documents/Menus/abc123/MenuCategories/cat1"
    ///   → CollectionPath: "MenuCategories", DocumentId: "cat1", ParentDocumentPath: "Menus/abc123"
    /// </remarks>
    private static SnapshotInfo ParseSnapshotPath(DocumentSnapshot snapshot)
    {
        var fullPath = snapshot.Reference.Path;

        // Find "/documents/" and get everything after it
        var documentsMarker = "/documents/";
        var documentsIndex = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);

        if (documentsIndex < 0)
            throw new InvalidOperationException($"Invalid Firestore path format: {fullPath}");

        var relativePath = fullPath.Substring(documentsIndex + documentsMarker.Length);
        var segments = relativePath.Split('/');

        // Segments are always pairs: collection/docId/collection/docId/...
        // Last two segments are the current collection and document ID
        if (segments.Length < 2 || segments.Length % 2 != 0)
            throw new InvalidOperationException($"Invalid Firestore path format: {fullPath}");

        var collectionPath = segments[segments.Length - 2];
        var documentId = segments[segments.Length - 1];

        // Parent path is everything before the last collection/docId pair
        string? parentDocumentPath = null;
        if (segments.Length > 2)
        {
            // Join all segments except the last two
            parentDocumentPath = string.Join("/", segments.Take(segments.Length - 2));
        }

        return new SnapshotInfo(snapshot, collectionPath, documentId, parentDocumentPath);
    }

    /// <summary>
    /// Finds the root documents that match the query's collection path and optional document ID.
    /// </summary>
    private List<SnapshotInfo> FindRoots(ResolvedFirestoreQuery query)
    {
        if (!_byCollection.TryGetValue(query.CollectionPath, out var candidates))
            return new List<SnapshotInfo>();

        // Filter by document ID if specified
        if (query.DocumentId != null)
            return candidates.Where(c => c.DocumentId == query.DocumentId).ToList();

        // For collection queries, return only root-level documents (no parent)
        return candidates.Where(c => c.ParentDocumentPath == null).ToList();
    }

    /// <summary>
    /// Shapes a single snapshot into a dictionary, resolving all its includes recursively.
    /// </summary>
    private Dictionary<string, object?> ShapeNode(
        SnapshotInfo info,
        IReadOnlyList<ResolvedInclude> includes,
        IReadOnlyList<ResolvedSubcollectionProjection>? projectionSubcollections = null,
        bool includeId = true)
    {
        var dict = info.Snapshot.ToDictionary();

        // Insert Id at the beginning only if requested
        if (includeId)
        {
            var result = new Dictionary<string, object?> { ["Id"] = info.DocumentId };
            foreach (var kvp in dict)
                result[kvp.Key] = kvp.Value;
            dict = result;
        }

        var nodePath = $"{info.CollectionPath}/{info.DocumentId}";

        // Process includes (from .Include())
        foreach (var include in includes)
        {
            if (include.IsCollection)
            {
                dict[include.NavigationName] = ResolveSubCollection(nodePath, include);
            }
            else
            {
                // Reference: navigate path and resolve DocumentReferences in place
                ResolveReferenceInPlace(dict, include);
            }
        }

        // Process projection subcollections (from .Select(x => new { ..., SubCollection = x.Nav... }))
        if (projectionSubcollections != null)
        {
            foreach (var subcol in projectionSubcollections)
            {
                dict[subcol.ResultName] = ResolveProjectionSubcollection(nodePath, subcol);
            }
        }

        return dict;
    }

    /// <summary>
    /// Resolves a projection subcollection by finding all child documents under the parent path.
    /// For aggregations, returns the stored aggregation value directly.
    /// </summary>
    private object? ResolveProjectionSubcollection(
        string parentPath,
        ResolvedSubcollectionProjection subcol)
    {
        // Handle aggregation subcollections (Count, Sum, Average)
        if (subcol.IsAggregation)
        {
            // Build full parent document path for lookup
            // parentPath is like "Clientes/cli-001", need to find the full Firestore path
            var fullParentPath = FindFullParentPath(parentPath);
            if (fullParentPath == null)
                return null;

            var key = $"{fullParentPath}:{subcol.ResultName}";
            return _aggregations.TryGetValue(key, out var value) ? value : null;
        }

        if (!_byParentPath.TryGetValue(parentPath, out var children))
            return new List<Dictionary<string, object?>>();

        // Filter by the subcollection's collection path
        var matching = children.Where(c => c.CollectionPath == subcol.CollectionPath);

        // Check if Id should be included for this subcollection
        var includeId = subcol.Fields == null || subcol.Fields.Any(f => f.FieldPath == "Id");

        // Recursively shape each child with nested includes and nested subcollections
        return matching
            .Select(child => ShapeNode(child, subcol.Includes, subcol.NestedSubcollections, includeId))
            .ToList();
    }

    /// <summary>
    /// Finds the full Firestore document path from a relative path like "Clientes/cli-001".
    /// </summary>
    private string? FindFullParentPath(string relativePath)
    {
        // Search in indexed snapshots to find the full path
        var parts = relativePath.Split('/');
        if (parts.Length < 2)
            return null;

        var collectionPath = parts[0];
        var documentId = parts[1];

        if (_byCollection.TryGetValue(collectionPath, out var candidates))
        {
            var found = candidates.FirstOrDefault(c => c.DocumentId == documentId);
            if (found != null)
                return found.Snapshot.Reference.Path;
        }

        return null;
    }

    /// <summary>
    /// Resolves a subcollection include by finding all child documents under the parent path.
    /// </summary>
    private List<Dictionary<string, object?>> ResolveSubCollection(string parentPath, ResolvedInclude include)
    {
        if (!_byParentPath.TryGetValue(parentPath, out var children))
            return new List<Dictionary<string, object?>>();

        // Filter by the include's collection path
        var matching = children.Where(c => c.CollectionPath == include.CollectionPath);

        // Recursively shape each child with nested includes
        return matching
            .Select(child => ShapeNode(child, include.NestedIncludes))
            .ToList();
    }

    /// <summary>
    /// Resolves a reference include by navigating the path and replacing DocumentReferences in place.
    /// Handles paths like "Items.MenuItem" where Items is an embedded array.
    /// </summary>
    private void ResolveReferenceInPlace(Dictionary<string, object?> dict, ResolvedInclude include)
    {
        var pathParts = include.NavigationName.Split('.');
        ResolveReferenceRecursive(dict, pathParts, 0, include);
    }

    /// <summary>
    /// Recursively navigates the path, handling arrays, and resolves DocumentReferences at the final segment.
    /// </summary>
    private void ResolveReferenceRecursive(
        object? current,
        string[] pathParts,
        int partIndex,
        ResolvedInclude include)
    {
        if (current == null || partIndex >= pathParts.Length)
            return;

        var part = pathParts[partIndex];
        var isLastPart = partIndex == pathParts.Length - 1;

        // Case 1: Current is a dictionary
        if (current is IDictionary<string, object?> dict)
        {
            if (!dict.TryGetValue(part, out var value) || value == null)
                return;

            if (isLastPart)
            {
                // Final segment: resolve the DocumentReference(s)
                dict[part] = ResolveReferenceValue(value, include);
            }
            else
            {
                // Navigate deeper
                ResolveReferenceRecursive(value, pathParts, partIndex + 1, include);
            }
        }
        // Case 2: Current is an array - iterate and process each element
        else if (current is IEnumerable<object> array && current is not string)
        {
            foreach (var item in array)
            {
                ResolveReferenceRecursive(item, pathParts, partIndex, include);
            }
        }
    }

    /// <summary>
    /// Resolves a DocumentReference value (single or array) to its shaped dictionary form.
    /// </summary>
    private object? ResolveReferenceValue(object? value, ResolvedInclude include)
    {
        // Single DocumentReference
        if (value is DocumentReference docRef)
        {
            return FindAndShapeReference(docRef, include);
        }

        // Array of DocumentReferences
        if (value is IEnumerable<object> list && value is not string)
        {
            var results = new List<Dictionary<string, object?>>();
            foreach (var item in list)
            {
                if (item is DocumentReference itemRef)
                {
                    var shaped = FindAndShapeReference(itemRef, include);
                    if (shaped != null)
                        results.Add(shaped);
                }
            }
            return results.Count > 0 ? results : null;
        }

        return null;
    }

    /// <summary>
    /// Finds a referenced document in the indexed snapshots and shapes it with nested includes.
    /// </summary>
    private Dictionary<string, object?>? FindAndShapeReference(DocumentReference docRef, ResolvedInclude include)
    {
        // Extract document ID from the reference
        var refDocId = docRef.Id;

        // Look up in the indexed snapshots by collection
        if (!_byCollection.TryGetValue(include.CollectionPath, out var candidates))
            return null;

        var found = candidates.FirstOrDefault(c => c.DocumentId == refDocId);
        if (found == null)
            return null;

        // Shape with nested includes
        return ShapeNode(found, include.NestedIncludes);
    }

}
