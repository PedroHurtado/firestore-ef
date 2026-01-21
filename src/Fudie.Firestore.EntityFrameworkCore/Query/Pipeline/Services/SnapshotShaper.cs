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
public record ShapedValue(
    object? Value,
    Type TargetType,
    ValueKind Kind
);

/// <summary>
/// An item with its typed values indexed by ResultName.
/// Each value includes its type and how to materialize it.
/// </summary>
public class ShapedItem
{
    /// <summary>
    /// Values indexed by ResultName.
    /// Each value includes its target type and ValueKind.
    /// </summary>
    public Dictionary<string, ShapedValue> Values { get; init; } = new();
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

    private static void FormatShapedValue(StringBuilder sb, string key, ShapedValue shaped, string prefix, int indent)
    {
        var typeName = GetShortTypeName(shaped.TargetType);
        var kindStr = shaped.Kind.ToString();

        switch (shaped.Value)
        {
            case null:
                sb.AppendLine($"{prefix}{key}: null ({typeName}, {kindStr})");
                break;

            case IList<ShapedItem> shapedItems:
                sb.AppendLine($"{prefix}{key}: [{shapedItems.Count}] ({typeName}, {kindStr})");
                for (var i = 0; i < shapedItems.Count; i++)
                {
                    sb.AppendLine($"{prefix}  [{i}] ShapedItem");
                    FormatShapedItem(sb, shapedItems[i], indent + 2);
                }
                break;

            case IList<object> list:
                sb.AppendLine($"{prefix}{key}: [{list.Count}] ({typeName}, {kindStr})");
                for (var i = 0; i < Math.Min(list.Count, 5); i++)
                {
                    sb.AppendLine($"{prefix}  [{i}] {FormatSimpleValue(list[i])}");
                }
                if (list.Count > 5)
                    sb.AppendLine($"{prefix}  ... and {list.Count - 5} more");
                break;

            case Dictionary<string, object?> dict:
                sb.AppendLine($"{prefix}{key}: ({typeName}, {kindStr})");
                FormatDictionary(sb, dict, indent + 1);
                break;

            case DocumentReference docRef:
                sb.AppendLine($"{prefix}{key}: Ref({docRef.Id}) ({typeName}, {kindStr})");
                break;

            case string s:
                sb.AppendLine($"{prefix}{key}: \"{s}\" ({typeName}, {kindStr})");
                break;

            default:
                sb.AppendLine($"{prefix}{key}: {shaped.Value} ({typeName}, {kindStr})");
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

        var shapedNodes = roots
            .Select(root => ShapeNode(context, root, query.Includes, projectionSubcollections, includePk, pkName))
            .ToList();

        // For projections, flatten the dictionaries so paths like "Direccion.Ciudad" become top-level keys
        if (query.HasProjection)
        {
            shapedNodes = shapedNodes.Select(FlattenForProjection).ToList();
        }

        // Build TypedItems from AST projection fields
        var typedItems = BuildTypedItems(shapedNodes, query);

        return new ShapedResult(shapedNodes, typedItems, query.HasProjection);
    }

    /// <summary>
    /// Builds TypedItems with ShapedValue metadata from the AST projection fields.
    /// This is the new SRP structure that the Materializer will use.
    /// </summary>
    private static List<ShapedItem> BuildTypedItems(
        List<Dictionary<string, object?>> items,
        ResolvedFirestoreQuery query)
    {
        var projectedFields = query.Projection?.Fields ?? [];
        var subcollections = query.Projection?.Subcollections ?? [];
        var pkName = query.PrimaryKeyPropertyName;

        return items.Select(item => BuildTypedItem(item, projectedFields, subcollections, pkName)).ToList();
    }

    private static ShapedItem BuildTypedItem(
        Dictionary<string, object?> rawItem,
        IReadOnlyList<FirestoreProjectedField> projectedFields,
        IReadOnlyList<ResolvedSubcollectionProjection> subcollections,
        string? pkName)
    {
        var shapedItem = new ShapedItem();

        // If we have projected fields, use them (projection case)
        if (projectedFields.Count > 0)
        {
            foreach (var field in projectedFields)
            {
                if (rawItem.TryGetValue(field.FieldPath, out var value))
                {
                    var kind = DetermineValueKind(field.FieldType, value);
                    shapedItem.Values[field.ResultName] = new ShapedValue(value, field.FieldType, kind);
                }
            }
        }
        else
        {
            // No projection - copy all values from rawItem, inferring types
            foreach (var kvp in rawItem)
            {
                var value = kvp.Value;
                var inferredType = InferType(value);
                var kind = DetermineValueKindFromValue(value);

                // Convert nested ObjectLists to ShapedItems recursively
                if (kind == ValueKind.ObjectList)
                {
                    var nestedItems = ConvertToShapedItems(value);
                    if (nestedItems != null)
                    {
                        shapedItem.Values[kvp.Key] = new ShapedValue(nestedItems, inferredType, kind);
                        continue;
                    }
                }

                shapedItem.Values[kvp.Key] = new ShapedValue(value, inferredType, kind);
            }
        }

        // Add subcollections with type info
        foreach (var subcol in subcollections)
        {
            if (rawItem.TryGetValue(subcol.ResultName, out var value))
            {
                var kind = DetermineSubcollectionKind(subcol, value);
                var targetType = GetSubcollectionTargetType(subcol);

                // Convert raw subcollection items to ShapedItems if it's an ObjectList
                if (kind == ValueKind.ObjectList && value is IList<Dictionary<string, object?>> listOfDicts)
                {
                    var nestedItems = listOfDicts
                        .Select(d => BuildTypedItem(d, subcol.Fields ?? [], subcol.NestedSubcollections ?? [], subcol.PrimaryKeyPropertyName))
                        .ToList();
                    shapedItem.Values[subcol.ResultName] = new ShapedValue(nestedItems, targetType, kind);
                }
                else
                {
                    shapedItem.Values[subcol.ResultName] = new ShapedValue(value, targetType, kind);
                }
            }
        }

        // Add PK if not already present (only for projection case where PK might be missing)
        if (projectedFields.Count > 0 && pkName != null && rawItem.TryGetValue(pkName, out var pkValue) && !shapedItem.Values.ContainsKey(pkName))
        {
            shapedItem.Values[pkName] = new ShapedValue(pkValue, typeof(string), ValueKind.Scalar);
        }

        return shapedItem;
    }

    /// <summary>
    /// Converts a collection value to a list of ShapedItems.
    /// Handles various collection types that might come from Firestore.
    /// </summary>
    private static List<ShapedItem>? ConvertToShapedItems(object? value)
    {
        if (value == null)
            return null;

        // Try IList<Dictionary<string, object?>> first
        if (value is IList<Dictionary<string, object?>> listOfDictsNullable)
        {
            return listOfDictsNullable
                .Select(d => BuildTypedItem(d, [], [], null))
                .ToList();
        }

        // Try IList<Dictionary<string, object>> (without nullable)
        if (value is IList<Dictionary<string, object>> listOfDicts)
        {
            return listOfDicts
                .Select(d => BuildTypedItem(d.ToDictionary(k => k.Key, k => (object?)k.Value), [], [], null))
                .ToList();
        }

        // Try IList<object> where items are dictionaries
        if (value is IList<object> list)
        {
            var results = new List<ShapedItem>();
            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> dictNullable)
                    results.Add(BuildTypedItem(dictNullable, [], [], null));
                else if (item is Dictionary<string, object> dict)
                    results.Add(BuildTypedItem(dict.ToDictionary(k => k.Key, k => (object?)k.Value), [], [], null));
                else
                    return null; // Not a list of dictionaries
            }
            return results;
        }

        return null;
    }

    /// <summary>
    /// Infers CLR type from Firestore value when we don't have AST type info.
    /// </summary>
    private static Type InferType(object? value) => value switch
    {
        null => typeof(object),
        string => typeof(string),
        long => typeof(long),
        double => typeof(double),
        bool => typeof(bool),
        Timestamp => typeof(DateTime),
        DocumentReference => typeof(object), // Entity reference
        Dictionary<string, object?> => typeof(object), // ComplexType or nested
        IList<Dictionary<string, object?>> => typeof(IEnumerable<object>), // ObjectList
        IList<object> => typeof(IEnumerable<object>),
        _ => value.GetType()
    };

    /// <summary>
    /// Determines ValueKind from the actual value when we don't have AST type info.
    /// </summary>
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
        IList<Dictionary<string, object?>> => ValueKind.ObjectList,
        IList<object> list when list.Count > 0 && list[0] is Dictionary<string, object?> => ValueKind.ObjectList,
        IList<object> => ValueKind.ScalarList,
        _ => ValueKind.Scalar
    };

    private static ValueKind DetermineValueKind(Type type, object? value)
    {
        // Null is scalar
        if (value == null)
            return ValueKind.Scalar;

        // Simple types
        if (IsSimpleType(type))
            return ValueKind.Scalar;

        // Collections
        if (IsCollectionType(type))
        {
            var elementType = GetElementType(type);
            return IsSimpleType(elementType) ? ValueKind.ScalarList : ValueKind.ObjectList;
        }

        // DocumentReference indicates Entity
        if (value is DocumentReference)
            return ValueKind.Entity;

        // Dictionary indicates ComplexType
        if (value is Dictionary<string, object?>)
            return ValueKind.ComplexType;

        return ValueKind.Scalar;
    }

    private static ValueKind DetermineSubcollectionKind(ResolvedSubcollectionProjection subcol, object? value)
    {
        // Aggregation result is scalar
        if (subcol.IsAggregation)
            return ValueKind.Scalar;

        // Check if it's a scalar list (single field of simple type)
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
            return typeof(decimal); // Aggregations return decimal

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

    /// <summary>
    /// Flattens a hierarchical dictionary into a flat dictionary with dot-separated keys.
    /// Used for projections where the Materializer needs to find values by path.
    /// </summary>
    /// <example>
    /// Input: { "Direccion": { "Ciudad": "Bilbao", "Coordenadas": { "Altitud": 19 } } }
    /// Output: { "Direccion.Ciudad": "Bilbao", "Direccion.Coordenadas.Altitud": 19 }
    /// </example>
    private static Dictionary<string, object?> FlattenForProjection(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        FlattenRecursive(dict, "", result);
        return result;
    }

    private static void FlattenRecursive(Dictionary<string, object?> source, string prefix, Dictionary<string, object?> result)
    {
        foreach (var kvp in source)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object?> nested)
            {
                // Recurse into nested dictionary
                FlattenRecursive(nested, key, result);
            }
            else
            {
                // Leaf value - add with full path as key
                result[key] = kvp.Value;
            }
        }
    }

    private static bool ShouldIncludePk(IReadOnlyList<FirestoreProjectedField>? fields, string? pkName) =>
        fields == null || (pkName != null && fields.Any(f => f.FieldPath == pkName));

    /// <summary>
    /// Gets the relative document path from the full Firestore path.
    /// For "projects/xxx/databases/yyy/documents/Clientes/cli-xxx/Pedidos/ped-xxx"
    /// returns "Clientes/cli-xxx/Pedidos/ped-xxx"
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
        string? pkName = null,
        IReadOnlyList<FirestoreProjectedField>? projectedFields = null)
    {
        var rawDict = info.Snapshot.ToDictionary();
        // Use full parent path for nested subcollections (ThenInclude)
        // For Clientes/cli-xxx/Pedidos/ped-xxx, nodePath must be "Clientes/cli-xxx/Pedidos/ped-xxx"
        // so that children (LineaPedidos) can be found by their ParentDocumentPath
        var nodePath = GetRelativeDocumentPath(info);

        // Build output dictionary with field renaming for projections
        var dict = new Dictionary<string, object?>();

        if (includePk && pkName != null)
        {
            dict[pkName] = info.DocumentId;
        }

        // Apply field projection/renaming if specified
        if (projectedFields is { Count: > 0 })
        {
            foreach (var field in projectedFields)
            {
                if (rawDict.TryGetValue(field.FieldPath, out var value))
                {
                    dict[field.ResultName] = value;
                }
            }

            // Also copy reference fields needed for FK resolution
            // e.g., if projectedFields has "Libro.Titulo", we need to copy "Libro" (the DocumentReference)
            foreach (var include in includes)
            {
                if (!include.IsCollection && rawDict.TryGetValue(include.NavigationName, out var refValue))
                {
                    dict[include.NavigationName] = refValue;
                }
            }
        }
        else
        {
            // No projection - copy all fields
            foreach (var kvp in rawDict)
            {
                dict[kvp.Key] = kvp.Value;
            }
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
        return ResolveChildren(context, parentPath, subcol.CollectionPath, subcol.Includes, subcol.NestedSubcollections, includePk, pkName, subcol.Fields);
    }

    private static List<Dictionary<string, object?>> ResolveChildren(
        ShapingContext context,
        string parentPath,
        string collectionPath,
        IReadOnlyList<ResolvedInclude> includes,
        IReadOnlyList<ResolvedSubcollectionProjection>? projectionSubcollections,
        bool includePk,
        string? pkName,
        IReadOnlyList<FirestoreProjectedField>? projectedFields = null)
    {
        if (!context.ByParentPath.TryGetValue(parentPath, out var children))
            return [];

        var results = children
            .Where(c => c.CollectionPath == collectionPath)
            .Select(child => ShapeNode(context, child, includes, projectionSubcollections, includePk, pkName, projectedFields))
            .ToList();

        // Flatten subcollection items for projections (same as root level)
        if (projectedFields is { Count: > 0 })
        {
            results = results.Select(FlattenForProjection).ToList();
        }

        return results;
    }

    private static string? FindFullParentPath(ShapingContext context, string relativePath)
    {
        var parts = relativePath.Split('/');
        if (parts.Length < 2)
            return null;

        // Take the last 2 segments (collection/documentId)
        // For "Clientes/cli-xxx/Pedidos/ped-xxx", we want "Pedidos" and "ped-xxx"
        var collectionPath = parts[^2];
        var documentId = parts[^1];

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