using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Google.Cloud.Firestore;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Classifies how a value should be materialized.
/// This information comes from the AST (FirestoreProjectedField.FieldType).
/// </summary>
public enum ValueKind
{
    /// <summary>Simple value: string, int, decimal, enum, DateTime, bool</summary>
    Scalar,
    /// <summary>Nested owned type: Direccion, Coordenadas</summary>
    ComplexType,
    /// <summary>Related entity via FK: Autor, Cliente</summary>
    Entity,
    /// <summary>List of simple values: List&lt;decimal&gt;, List&lt;string&gt;</summary>
    ScalarList,
    /// <summary>List of objects: List&lt;Pedido&gt;, List&lt;anonymous&gt;</summary>
    ObjectList
}

/// <summary>
/// A value with its materialization metadata.
/// The Materializer uses this to know HOW to materialize without needing the AST.
/// </summary>
/// <param name="Value">The raw value extracted from Firestore</param>
/// <param name="TargetType">Target CLR type (from AST: FieldType)</param>
/// <param name="Kind">How to materialize this value</param>
/// <param name="ResultName">Property name for the Materializer (from AST: ResultName)</param>
public record ShapedValue(
    object? Value,
    Type TargetType,
    ValueKind Kind,
    string ResultName
);

/// <summary>
/// An item with its typed values indexed by FieldPath.
/// Each value includes its type, how to materialize it, and the ResultName for the Materializer.
/// </summary>
public class ShapedItem
{
    /// <summary>
    /// Values indexed by FieldPath (unique key).
    /// Each value includes its target type, ValueKind, and ResultName.
    /// </summary>
    public Dictionary<string, ShapedValue> Values { get; init; } = new();

    /// <summary>
    /// Converts this ShapedItem to a legacy dictionary format for backward compatibility.
    /// Uses ResultName as the key (not FieldPath) for Materializer compatibility.
    /// </summary>
    [Obsolete("Use Values directly. Remove when Materializer is migrated.")]
    public Dictionary<string, object?> ToLegacyDict()
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in Values)
        {
            // Use ResultName as key for Materializer compatibility
            result[kvp.Value.ResultName] = ConvertToLegacyValue(kvp.Value);
        }
        return result;
    }

    private static object? ConvertToLegacyValue(ShapedValue shaped)
    {
        return shaped.Value switch
        {
            IList<ShapedItem> shapedItems => shapedItems.Select(si => si.ToLegacyDict()).ToList(),
            ShapedItem nestedItem => nestedItem.ToLegacyDict(),
            _ => shaped.Value
        };
    }
}

/// <summary>
/// Wrapper class for shaped query results that provides formatted ToString() for debugging.
/// Contains both the legacy Items (for current Materializer) and the new TypedItems (for future SRP refactoring).
/// </summary>
public class ShapedResult
{
    /// <summary>
    /// Legacy format - raw dictionaries for the current Materializer.
    /// Will be removed after SRP refactoring is complete.
    /// </summary>
    public List<Dictionary<string, object?>> Items { get; init; }

    /// <summary>
    /// New format - typed items with ValueKind metadata.
    /// The Materializer will use this after SRP refactoring.
    /// </summary>
    public List<ShapedItem> TypedItems { get; init; }

    /// <summary>
    /// Indicates if this result has a projection applied.
    /// </summary>
    public bool HasProjection { get; init; }

    public ShapedResult(List<Dictionary<string, object?>> items)
    {
        Items = items;
        TypedItems = [];
        HasProjection = false;
    }

    public ShapedResult(List<Dictionary<string, object?>> items, List<ShapedItem> typedItems, bool hasProjection)
    {
        Items = items;
        TypedItems = typedItems;
        HasProjection = hasProjection;
    }

    public override string ToString()
    {
        // If we have TypedItems, show the new SRP structure
        if (TypedItems.Count > 0)
            return ToStringTyped();

        // Otherwise show legacy format
        return ToStringLegacy();
    }

    /// <summary>
    /// New format showing ShapedValue with ValueKind metadata.
    /// This is what the Materializer will receive after SRP refactoring.
    /// </summary>
    private string ToStringTyped()
    {
        if (TypedItems.Count == 0)
            return "[]";

        var sb = new StringBuilder();
        sb.AppendLine($"HasProjection: {HasProjection}");
        sb.AppendLine();

        for (var i = 0; i < TypedItems.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.AppendLine($"[{i}] ShapedItem");
            FormatShapedItem(sb, TypedItems[i], 1);
        }
        return sb.ToString();
    }

    private static void FormatShapedItem(StringBuilder sb, ShapedItem item, int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var kvp in item.Values)
        {
            FormatShapedValue(sb, kvp.Key, kvp.Value, prefix, indent);
        }
    }

    private static void FormatShapedValue(StringBuilder sb, string fieldPath, ShapedValue shaped, string prefix, int indent)
    {
        var typeName = GetShortTypeName(shaped.TargetType);
        var kindStr = shaped.Kind.ToString();
        // Format: FieldPath: value (ResultName, Type, Kind) - only show ResultName if different
        var metaParts = fieldPath != shaped.ResultName
            ? $"{shaped.ResultName}, {typeName}, {kindStr}"
            : $"{typeName}, {kindStr}";

        switch (shaped.Value)
        {
            case null:
                sb.AppendLine($"{prefix}{fieldPath}: null ({metaParts})");
                break;

            case IList<ShapedItem> shapedItems:
                sb.AppendLine($"{prefix}{fieldPath}: [{shapedItems.Count}] ({metaParts})");
                for (var i = 0; i < shapedItems.Count; i++)
                {
                    sb.AppendLine($"{prefix}  [{i}] ShapedItem");
                    FormatShapedItem(sb, shapedItems[i], indent + 2);
                }
                break;

            case IList<object> list:
                sb.AppendLine($"{prefix}{fieldPath}: [{list.Count}] ({metaParts})");
                for (var i = 0; i < Math.Min(list.Count, 5); i++)
                {
                    sb.AppendLine($"{prefix}  [{i}] {FormatSimpleValue(list[i])}");
                }
                if (list.Count > 5)
                    sb.AppendLine($"{prefix}  ... and {list.Count - 5} more");
                break;

            case ShapedItem nestedItem:
                sb.AppendLine($"{prefix}{fieldPath}: ({metaParts})");
                FormatShapedItem(sb, nestedItem, indent + 1);
                break;

            case Dictionary<string, object?> dict:
                sb.AppendLine($"{prefix}{fieldPath}: ({metaParts})");
                FormatDictionary(sb, dict, indent + 1);
                break;

            case DocumentReference docRef:
                sb.AppendLine($"{prefix}{fieldPath}: Ref({docRef.Id}) ({metaParts})");
                break;

            case string s:
                sb.AppendLine($"{prefix}{fieldPath}: \"{s}\" ({metaParts})");
                break;

            default:
                sb.AppendLine($"{prefix}{fieldPath}: {shaped.Value} ({metaParts})");
                break;
        }
    }

    private static string GetShortTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var args = string.Join(", ", type.GetGenericArguments().Select(GetShortTypeName));
            var baseName = genericDef.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
                baseName = baseName.Substring(0, tickIndex);
            return $"{baseName}<{args}>";
        }

        if (type.Name.StartsWith("<>") || type.Name.Contains("AnonymousType"))
            return "Anonymous";

        return type.Name;
    }

    /// <summary>
    /// Legacy format for debugging with current Items structure.
    /// </summary>
    private string ToStringLegacy()
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
/// Configuration options for shaping a node. Reduces parameter count in ShapeNode.
/// </summary>
public record ShapeOptions(
    IReadOnlyList<ResolvedInclude> Includes,
    IReadOnlyList<ResolvedSubcollectionProjection>? Subcollections = null,
    IReadOnlyList<FirestoreProjectedField>? ProjectedFields = null,
    bool IncludePk = true,
    string? PkName = null);

/// <summary>
/// Immutable context for shaping operations. Holds indexed snapshots and aggregations.
/// </summary>
internal record ShapingContext(
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByCollection,
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByParentPath,
    IReadOnlyDictionary<string, SnapshotInfo> ByFullPath,
    IReadOnlyDictionary<string, object> Aggregations)
{
    public static ShapingContext Create(
        IReadOnlyList<DocumentSnapshot> snapshots,
        Dictionary<string, object>? aggregations)
    {
        var byCollection = new Dictionary<string, List<SnapshotInfo>>();
        var byParentPath = new Dictionary<string, List<SnapshotInfo>>();
        var byFullPath = new Dictionary<string, SnapshotInfo>();

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

            // Index by full path for O(1) lookup
            var fullPath = $"{info.CollectionPath}/{info.DocumentId}";
            byFullPath[fullPath] = info;
        }

        return new ShapingContext(byCollection, byParentPath, byFullPath, aggregations ?? new Dictionary<string, object>());
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
/// Transforms a flat list of DocumentSnapshots into a hierarchical structure
/// based on the query's AST (ResolvedFirestoreQuery).
/// Single-pass implementation: builds ShapedItem directly with metadata.
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
            return new ShapedResult([], [], false);

        var context = ShapingContext.Create(snapshots, aggregations);
        var roots = FindRoots(context, query);

        if (roots.Count == 0)
            return new ShapedResult([], [], false);

        var options = BuildRootOptions(query);

        // Single pass: ShapedItem is the source of truth
        var typedItems = roots
            .Select(root => ShapeNode(context, root, options))
            .ToList();

        // Legacy: Derive Dictionary for current Materializer
#pragma warning disable CS0618
        var legacyItems = typedItems.Select(t => t.ToLegacyDict()).ToList();
#pragma warning restore CS0618

        return new ShapedResult(legacyItems, typedItems, query.HasProjection);
    }

    private static ShapeOptions BuildRootOptions(ResolvedFirestoreQuery query)
    {
        var pkName = query.PrimaryKeyPropertyName;
        var includePk = ShouldIncludePk(query.Projection?.Fields, pkName);

        return new ShapeOptions(
            Includes: query.Includes,
            Subcollections: query.Projection?.Subcollections,
            ProjectedFields: query.Projection?.Fields,
            IncludePk: includePk,
            PkName: pkName);
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

    /// <summary>
    /// Gets the relative document path from the full Firestore path.
    /// </summary>
    private static string GetRelativeDocumentPath(SnapshotInfo info)
    {
        var fullPath = info.Snapshot.Reference.Path;
        var documentsMarker = "/documents/";
        var documentsIndex = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);

        if (documentsIndex < 0)
            return $"{info.CollectionPath}/{info.DocumentId}";

        return fullPath.Substring(documentsIndex + documentsMarker.Length);
    }

    /// <summary>
    /// Shapes a single node (document) into a ShapedItem with metadata.
    /// Single-pass: extracts fields and builds ShapedValue inline.
    /// </summary>
    private static ShapedItem ShapeNode(
        ShapingContext context,
        SnapshotInfo info,
        ShapeOptions options)
    {
        var rawDict = info.Snapshot.ToDictionary();
        var nodePath = GetRelativeDocumentPath(info);
        var shapedItem = new ShapedItem();

        // 1. Add Primary Key
        AddPrimaryKey(shapedItem, info.DocumentId, options);

        // 2. Extract fields with metadata (passing context and includes for FK resolution)
        ExtractFields(context, rawDict, options, shapedItem);

        // 3. Resolve includes (collections and references) - for non-projection cases
        ResolveIncludes(context, shapedItem, nodePath, options.Includes);

        // 4. Resolve projection subcollections
        ResolveSubcollections(context, shapedItem, nodePath, options.Subcollections);

        return shapedItem;
    }

    private static void AddPrimaryKey(ShapedItem shapedItem, string documentId, ShapeOptions options)
    {
        if (options.IncludePk && options.PkName != null)
        {
            // PK uses same name for FieldPath and ResultName
            shapedItem.Values[options.PkName] = new ShapedValue(documentId, typeof(string), ValueKind.Scalar, options.PkName);
        }
    }

    private static void ExtractFields(
        ShapingContext context,
        Dictionary<string, object> rawDict,
        ShapeOptions options,
        ShapedItem shapedItem)
    {
        if (options.ProjectedFields is { Count: > 0 })
        {
            // Projection case: use FieldPath as key, ResultName in ShapedValue
            foreach (var field in options.ProjectedFields)
            {
                var value = GetValueByPathWithFkResolution(context, rawDict, field.FieldPath, options.Includes);
                if (value != null || rawDict.ContainsKey(field.FieldPath.Split('.')[0]))
                {
                    var kind = DetermineValueKind(field.FieldType, value);

                    // For ComplexType, preserve hierarchy as ShapedItem
                    if (kind == ValueKind.ComplexType && value is Dictionary<string, object> nestedDict)
                    {
                        var nestedItem = ExtractDictAsShapedItem(nestedDict);
                        shapedItem.Values[field.FieldPath] = new ShapedValue(nestedItem, field.FieldType, kind, field.ResultName);
                    }
                    else
                    {
                        shapedItem.Values[field.FieldPath] = new ShapedValue(value, field.FieldType, kind, field.ResultName);
                    }
                }
            }
        }
        else
        {
            // No projection: extract all preserving hierarchy
            // FieldPath and ResultName are the same (raw field name)
            foreach (var kvp in rawDict)
            {
                shapedItem.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value, kvp.Key);
            }
        }
    }

    /// <summary>
    /// Gets a value by path, resolving FK references when encountered.
    /// For path "Autor.Nombre", if Autor is a DocumentReference, it resolves the reference
    /// and extracts Nombre from the resolved document.
    /// </summary>
    private static object? GetValueByPathWithFkResolution(
        ShapingContext context,
        Dictionary<string, object> dict,
        string path,
        IReadOnlyList<ResolvedInclude> includes)
    {
        var parts = path.Split('.');
        object? current = dict;
        var currentIncludes = includes;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (current is Dictionary<string, object> d)
            {
                if (!d.TryGetValue(part, out current))
                    return null;

                // If we hit a DocumentReference and there are more parts, resolve it
                if (current is DocumentReference docRef && i < parts.Length - 1)
                {
                    // Find the include for this navigation
                    var include = currentIncludes.FirstOrDefault(inc => inc.NavigationName == part);
                    if (include == null)
                        return null;

                    // Resolve the reference
                    var key = $"{include.CollectionPath}/{docRef.Id}";
                    if (!context.ByFullPath.TryGetValue(key, out var resolved))
                        return null;

                    // Continue navigation from the resolved document
                    current = resolved.Snapshot.ToDictionary();
                    currentIncludes = include.NestedIncludes;
                }
            }
            else if (current is Dictionary<string, object?> dNullable)
            {
                if (!dNullable.TryGetValue(part, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static ShapedValue ExtractValuePreservingHierarchy(object? value, string resultName)
    {
        return value switch
        {
            Dictionary<string, object?> dict => new ShapedValue(
                Value: ExtractDictAsShapedItemNullable(dict),
                TargetType: typeof(object),
                Kind: ValueKind.ComplexType,
                ResultName: resultName),

            IDictionary<string, object> dict => new ShapedValue(
                Value: ExtractDictAsShapedItemGeneric(dict),
                TargetType: typeof(object),
                Kind: ValueKind.ComplexType,
                ResultName: resultName),

            IList<Dictionary<string, object?>> list => new ShapedValue(
                Value: list.Select(ExtractDictAsShapedItemNullable).ToList(),
                TargetType: typeof(IEnumerable<object>),
                Kind: ValueKind.ObjectList,
                ResultName: resultName),

            IList<object> list when list.Count > 0 && list[0] is Dictionary<string, object?> => new ShapedValue(
                Value: list.Cast<Dictionary<string, object?>>().Select(ExtractDictAsShapedItemNullable).ToList(),
                TargetType: typeof(IEnumerable<object>),
                Kind: ValueKind.ObjectList,
                ResultName: resultName),

            DocumentReference docRef => new ShapedValue(
                Value: docRef,
                TargetType: typeof(object),
                Kind: ValueKind.Entity,
                ResultName: resultName),

            _ => new ShapedValue(
                Value: value,
                TargetType: InferType(value),
                Kind: DetermineValueKindFromValue(value),
                ResultName: resultName)
        };
    }

    private static ShapedItem ExtractDictAsShapedItem(Dictionary<string, object> dict)
    {
        var item = new ShapedItem();
        foreach (var kvp in dict)
        {
            // For nested dicts, key is both FieldPath and ResultName
            item.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value, kvp.Key);
        }
        return item;
    }

    private static ShapedItem ExtractDictAsShapedItemGeneric(IDictionary<string, object> dict)
    {
        var item = new ShapedItem();
        foreach (var kvp in dict)
        {
            // For nested dicts, key is both FieldPath and ResultName
            item.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value, kvp.Key);
        }
        return item;
    }

    private static ShapedItem ExtractDictAsShapedItemNullable(Dictionary<string, object?> dict)
    {
        var item = new ShapedItem();
        foreach (var kvp in dict)
        {
            // For nested dicts, key is both FieldPath and ResultName
            item.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value, kvp.Key);
        }
        return item;
    }

    private static void ResolveIncludes(
        ShapingContext context,
        ShapedItem shapedItem,
        string nodePath,
        IReadOnlyList<ResolvedInclude> includes)
    {
        foreach (var include in includes)
        {
            if (include.IsCollection)
            {
                var children = ResolveCollectionInclude(context, nodePath, include);
                var targetType = typeof(IEnumerable<object>);
                shapedItem.Values[include.NavigationName] = new ShapedValue(children, targetType, ValueKind.ObjectList, include.NavigationName);
            }
            else
            {
                ResolveReferenceInclude(context, shapedItem, include);
            }
        }
    }

    private static List<ShapedItem> ResolveCollectionInclude(
        ShapingContext context,
        string parentPath,
        ResolvedInclude include)
    {
        var childOptions = new ShapeOptions(
            Includes: include.NestedIncludes,
            PkName: include.PrimaryKeyPropertyName,
            IncludePk: true);

        return ResolveChildren(context, parentPath, include.CollectionPath, childOptions);
    }

    private static void ResolveReferenceInclude(
        ShapingContext context,
        ShapedItem shapedItem,
        ResolvedInclude include)
    {
        var pathParts = include.NavigationName.Split('.');

        // Navigate recursively through the ShapedItem hierarchy
        ResolveReferenceRecursive(context, shapedItem, pathParts, 0, include);
    }

    /// <summary>
    /// Recursively navigates through ShapedItem hierarchy to resolve references.
    /// For path "Secciones.EtiquetasDestacadas":
    /// 1. Find "Secciones" in current ShapedItem → List of ShapedItem
    /// 2. For each ShapedItem in list, find "EtiquetasDestacadas" → List of DocumentReference
    /// 3. Resolve each DocumentReference to a ShapedItem
    /// </summary>
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

        switch (current)
        {
            case ShapedItem shapedItem:
                if (!shapedItem.Values.TryGetValue(part, out var shapedValue))
                    return;

                if (isLastPart)
                {
                    // We've reached the final field - resolve the references
                    var resolved = ResolveReferenceValue(context, shapedValue.Value, include);
                    if (resolved != null)
                    {
                        var (value, kind) = resolved.Value;
                        var targetType = kind == ValueKind.Entity ? typeof(object) : typeof(IEnumerable<object>);
                        shapedItem.Values[part] = new ShapedValue(value, targetType, kind, part);
                    }
                }
                else
                {
                    // Continue navigating deeper
                    ResolveReferenceRecursive(context, shapedValue.Value, pathParts, partIndex + 1, include);
                }
                break;

            case IList<ShapedItem> shapedItems:
                // Iterate over each item and continue navigating
                foreach (var item in shapedItems)
                {
                    ResolveReferenceRecursive(context, item, pathParts, partIndex, include);
                }
                break;

            case IList<object> list:
                // Handle raw object lists (might contain dictionaries or ShapedItems)
                foreach (var item in list)
                {
                    ResolveReferenceRecursive(context, item, pathParts, partIndex, include);
                }
                break;
        }
    }

    /// <summary>
    /// Resolves DocumentReference(s) to ShapedItem(s).
    /// Returns the resolved value and its ValueKind, or null if no resolution possible.
    /// </summary>
    private static (object value, ValueKind kind)? ResolveReferenceValue(
        ShapingContext context,
        object? value,
        ResolvedInclude include)
    {
        if (value is DocumentReference docRef)
        {
            var resolved = FindAndShapeReference(context, docRef, include);
            return resolved != null ? (resolved, ValueKind.Entity) : null;
        }

        if (value is IEnumerable<object> list && value is not string)
        {
            var results = new List<ShapedItem>();
            foreach (var item in list)
            {
                if (item is DocumentReference itemRef)
                {
                    var shaped = FindAndShapeReference(context, itemRef, include);
                    if (shaped != null)
                        results.Add(shaped);
                }
            }
            return results.Count > 0 ? (results, ValueKind.ObjectList) : null;
        }

        return null;
    }

    private static ShapedItem? FindAndShapeReference(
        ShapingContext context,
        DocumentReference docRef,
        ResolvedInclude include)
    {
        var key = $"{include.CollectionPath}/{docRef.Id}";

        if (!context.ByFullPath.TryGetValue(key, out var found))
            return null;

        var childOptions = new ShapeOptions(
            Includes: include.NestedIncludes,
            PkName: include.PrimaryKeyPropertyName,
            IncludePk: true);

        return ShapeNode(context, found, childOptions);
    }

    private static void ResolveSubcollections(
        ShapingContext context,
        ShapedItem shapedItem,
        string nodePath,
        IReadOnlyList<ResolvedSubcollectionProjection>? subcollections)
    {
        if (subcollections == null) return;

        foreach (var subcol in subcollections)
        {
            var value = ResolveProjectionSubcollection(context, nodePath, subcol);
            if (value != null)
            {
                var kind = DetermineSubcollectionKind(subcol);
                var targetType = GetSubcollectionTargetType(subcol);
                shapedItem.Values[subcol.ResultName] = new ShapedValue(value, targetType, kind, subcol.ResultName);
            }
        }
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

        var childOptions = new ShapeOptions(
            Includes: subcol.Includes,
            Subcollections: subcol.NestedSubcollections,
            ProjectedFields: subcol.Fields,
            PkName: subcol.PrimaryKeyPropertyName,
            IncludePk: ShouldIncludePk(subcol.Fields, subcol.PrimaryKeyPropertyName));

        var children = ResolveChildren(context, parentPath, subcol.CollectionPath, childOptions);

        // For ScalarList, extract just the values
        if (subcol.Fields?.Count == 1 && IsSimpleType(subcol.Fields[0].FieldType))
        {
            var fieldName = subcol.Fields[0].ResultName;
            return children
                .Select(c => c.Values.TryGetValue(fieldName, out var sv) ? sv.Value : null)
                .Where(v => v != null)
                .ToList();
        }

        return children;
    }

    private static List<ShapedItem> ResolveChildren(
        ShapingContext context,
        string parentPath,
        string collectionPath,
        ShapeOptions options)
    {
        if (!context.ByParentPath.TryGetValue(parentPath, out var children))
            return [];

        return children
            .Where(c => c.CollectionPath == collectionPath)
            .Select(child => ShapeNode(context, child, options))
            .ToList();
    }

    private static string? FindFullParentPath(ShapingContext context, string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Length < 2)
            return null;

        var collectionPath = parts[^2];
        var documentId = parts[^1];
        var key = $"{collectionPath}/{documentId}";

        if (context.ByFullPath.TryGetValue(key, out var found))
            return found.Snapshot.Reference.Path;

        return null;
    }

    #region Type Inference Helpers

    private static Type InferType(object? value) => value switch
    {
        null => typeof(object),
        string => typeof(string),
        long => typeof(long),
        double => typeof(double),
        bool => typeof(bool),
        Timestamp => typeof(DateTime),
        DocumentReference => typeof(object),
        Dictionary<string, object?> => typeof(object),
        IList<Dictionary<string, object?>> => typeof(IEnumerable<object>),
        IList<object> => typeof(IEnumerable<object>),
        _ => value.GetType()
    };

    private static ValueKind DetermineValueKindFromValue(object? value) => value switch
    {
        null => ValueKind.Scalar,
        string => ValueKind.Scalar,
        long => ValueKind.Scalar,
        double => ValueKind.Scalar,
        bool => ValueKind.Scalar,
        Timestamp => ValueKind.Scalar,
        DocumentReference => ValueKind.Entity,
        Dictionary<string, object?> => ValueKind.ComplexType,
        IDictionary<string, object> => ValueKind.ComplexType,
        IList<Dictionary<string, object?>> => ValueKind.ObjectList,
        IList<object> list when list.Count > 0 && list[0] is Dictionary<string, object?> => ValueKind.ObjectList,
        IList<object> => ValueKind.ScalarList,
        _ => ValueKind.Scalar
    };

    private static ValueKind DetermineValueKind(Type type, object? value)
    {
        if (value == null)
            return ValueKind.Scalar;

        if (IsSimpleType(type))
            return ValueKind.Scalar;

        if (IsCollectionType(type))
        {
            var elementType = GetElementType(type);
            return IsSimpleType(elementType) ? ValueKind.ScalarList : ValueKind.ObjectList;
        }

        if (value is DocumentReference)
            return ValueKind.Entity;

        if (value is Dictionary<string, object?> or Dictionary<string, object>)
            return ValueKind.ComplexType;

        return ValueKind.Scalar;
    }

    private static ValueKind DetermineSubcollectionKind(ResolvedSubcollectionProjection subcol)
    {
        if (subcol.IsAggregation)
            return ValueKind.Scalar;

        if (subcol.Fields?.Count == 1)
        {
            var singleField = subcol.Fields[0];
            if (IsSimpleType(singleField.FieldType))
                return ValueKind.ScalarList;
        }

        return ValueKind.ObjectList;
    }

    private static Type GetSubcollectionTargetType(ResolvedSubcollectionProjection subcol)
    {
        if (subcol.IsAggregation)
            return typeof(decimal);

        if (subcol.Fields?.Count == 1)
        {
            var singleField = subcol.Fields[0];
            if (IsSimpleType(singleField.FieldType))
                return typeof(IEnumerable<>).MakeGenericType(singleField.FieldType);
        }

        return typeof(IEnumerable<object>);
    }

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid)
            || underlying == typeof(TimeSpan);
    }

    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string))
            return false;
        return type.IsArray
            || (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()));
    }

    private static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType()!;
        if (collectionType.IsGenericType)
            return collectionType.GetGenericArguments()[0];
        return typeof(object);
    }

    #endregion
}
