using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Projections;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Materializes projection results from Firestore documents.
/// Creates anonymous types or DTOs from DocumentSnapshots and aggregation results.
/// </summary>
public class ProjectionMaterializer : IProjectionMaterializer
{
    private readonly IFirestoreValueConverter _valueConverter;

    public ProjectionMaterializer(IFirestoreValueConverter valueConverter)
    {
        _valueConverter = valueConverter;
    }

    /// <inheritdoc />
    public object Materialize(
        ResolvedProjectionDefinition projection,
        DocumentSnapshot rootSnapshot,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations)
    {
        var targetType = projection.ClrType;
        IDictionary<string, object> data = rootSnapshot.ToDictionary();

        // Handle primitive types directly (string, int, decimal, etc.)
        if (IsPrimitiveOrSimpleType(targetType))
        {
            return MaterializePrimitiveValue(projection, data, targetType);
        }

        // Handle List<T> projections (ArrayOf fields) - e.g., Select(p => p.Etiquetas)
        if (IsCollectionType(targetType) && projection.Fields != null && projection.Fields.Count == 1)
        {
            var field = projection.Fields[0];
            var arrayValue = GetNestedValue(data, field.FieldPath);
            return _valueConverter.FromFirestore(arrayValue, targetType) ?? CreateEmptyCollection(targetType);
        }

        // Handle ComplexType projection (e.g., Select(p => p.Direccion))
        // When projecting a single ComplexType, extract its sub-dictionary
        if (projection.Fields != null && projection.Fields.Count == 1)
        {
            var field = projection.Fields[0];
            var nestedData = GetNestedValue(data, field.FieldPath);
            if (nestedData is IDictionary<string, object> complexTypeData)
            {
                data = complexTypeData;
            }
        }

        // Handle complex types with constructor
        var constructor = GetBestConstructor(targetType);
        var parameters = constructor.GetParameters();

        // If parameterless constructor, use property setters
        if (parameters.Length == 0)
        {
            return MaterializeWithPropertySetters(
                targetType,
                projection,
                rootSnapshot,
                data,
                allSnapshots,
                aggregations);
        }

        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            args[i] = ResolveParameterValue(
                param,
                projection,
                rootSnapshot,
                data,
                allSnapshots,
                aggregations);
        }

        return constructor.Invoke(args);
    }

    private object MaterializePrimitiveValue(
        ResolvedProjectionDefinition projection,
        IDictionary<string, object> data,
        Type targetType)
    {
        // If there's a single field projection, use it
        if (projection.Fields != null && projection.Fields.Count == 1)
        {
            var field = projection.Fields[0];
            var value = GetNestedValue(data, field.FieldPath);
            return _valueConverter.FromFirestore(value, targetType)!;
        }

        // Fallback: try to find a value that matches the target type
        // This handles cases like Select(e => e.Name) where Name maps directly
        foreach (var kvp in data)
        {
            var converted = _valueConverter.FromFirestore(kvp.Value, targetType);
            if (converted != null && converted.GetType() == targetType)
            {
                return converted;
            }
        }

        // Return default if nothing found
        return targetType.IsValueType
            ? Activator.CreateInstance(targetType)!
            : null!;
    }

    private static bool IsPrimitiveOrSimpleType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || (Nullable.GetUnderlyingType(type) != null && IsPrimitiveOrSimpleType(Nullable.GetUnderlyingType(type)!));
    }

    private static bool IsCollectionType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(List<>)
            || genericDef == typeof(IList<>)
            || genericDef == typeof(ICollection<>)
            || genericDef == typeof(IEnumerable<>)
            || genericDef == typeof(HashSet<>)
            || genericDef == typeof(ISet<>);
    }

    private static object CreateEmptyCollection(Type collectionType)
    {
        if (!collectionType.IsGenericType)
            return new List<object>();

        var elementType = collectionType.GetGenericArguments()[0];
        var genericDef = collectionType.GetGenericTypeDefinition();

        if (genericDef == typeof(HashSet<>) || genericDef == typeof(ISet<>))
        {
            return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType))!;
        }

        return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
    }

    private static bool IsAnonymousType(Type type)
    {
        return type.IsClass
            && type.IsSealed
            && type.Attributes.HasFlag(TypeAttributes.NotPublic)
            && type.Name.Contains("AnonymousType");
    }

    /// <summary>
    /// Materializes a nested anonymous type from fields or aggregations with a common prefix.
    /// E.g., aggregations "Resumen.TotalPedidos" and "Resumen.Cantidad" become { TotalPedidos = ..., Cantidad = ... }
    /// </summary>
    private object MaterializeNestedAnonymousType(
        Type nestedType,
        string prefix,
        List<FirestoreProjectedField>? nestedFields,
        ResolvedProjectionDefinition projection,
        DocumentSnapshot rootSnapshot,
        IDictionary<string, object> data,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations)
    {
        var constructor = GetBestConstructor(nestedType);
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name!;
            var fullResultName = $"{prefix}{paramName}";

            // Check if it's a subcollection aggregation (e.g., "Resumen.TotalPedidos" from Sum)
            var aggregationKey = $"{rootSnapshot.Reference.Path}:{fullResultName}";
            if (aggregations.TryGetValue(aggregationKey, out var aggValue))
            {
                args[i] = _valueConverter.FromFirestore(aggValue, param.ParameterType);
                continue;
            }

            // Check if it's a subcollection projection
            var subcollection = projection.Subcollections
                .FirstOrDefault(s => s.ResultName.Equals(fullResultName, StringComparison.OrdinalIgnoreCase));

            if (subcollection != null)
            {
                if (subcollection.IsAggregation)
                {
                    // The aggregation result should be in the aggregations dictionary
                    var subAggKey = $"{rootSnapshot.Reference.Path}:{subcollection.ResultName}";
                    if (aggregations.TryGetValue(subAggKey, out var subAggValue))
                    {
                        args[i] = _valueConverter.FromFirestore(subAggValue, param.ParameterType);
                    }
                }
                else
                {
                    args[i] = MaterializeSubcollection(
                        subcollection,
                        rootSnapshot,
                        allSnapshots,
                        aggregations,
                        param.ParameterType);
                }
                continue;
            }

            // Check if it's a field with exact match
            var field = nestedFields?.FirstOrDefault(f =>
                f.ResultName.Equals(fullResultName, StringComparison.OrdinalIgnoreCase));

            if (field != null)
            {
                // Use ResolveFieldValue to handle collection prefixes and Reference navigation
                var fieldValue = ResolveFieldValue(field.FieldPath, data, rootSnapshot, allSnapshots);

                // Handle full Reference entity projection (e.g., l.Autor where Autor is complete entity)
                if (fieldValue is ReferenceSnapshotWrapper wrapper)
                {
                    args[i] = MaterializeFullEntity(wrapper.Snapshot, wrapper.Snapshot.ToDictionary(), param.ParameterType);
                }
                else
                {
                    args[i] = _valueConverter.FromFirestore(fieldValue, param.ParameterType);
                }
                continue;
            }

            // Check for deeper nesting (e.g., "Resumen.Detalle.Valor")
            var deeperPrefix = $"{fullResultName}.";
            var deeperFields = projection.Fields?
                .Where(f => f.ResultName.StartsWith(deeperPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (deeperFields != null && deeperFields.Count > 0 && IsAnonymousType(param.ParameterType))
            {
                args[i] = MaterializeNestedAnonymousType(
                    param.ParameterType,
                    deeperPrefix,
                    deeperFields,
                    projection,
                    rootSnapshot,
                    data,
                    allSnapshots,
                    aggregations);
                continue;
            }

            // Default value
            args[i] = param.ParameterType.IsValueType
                ? Activator.CreateInstance(param.ParameterType)
                : null;
        }

        return constructor.Invoke(args);
    }

    private object MaterializeWithPropertySetters(
        Type targetType,
        ResolvedProjectionDefinition projection,
        DocumentSnapshot rootSnapshot,
        IDictionary<string, object> data,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations)
    {
        var instance = Activator.CreateInstance(targetType)!;

        foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;

            var propName = prop.Name;
            object? value = null;

            // 1. Check if it's a subcollection aggregation
            var aggregationKey = $"{rootSnapshot.Reference.Path}:{propName}";
            if (aggregations.TryGetValue(aggregationKey, out var aggValue))
            {
                value = _valueConverter.FromFirestore(aggValue, prop.PropertyType);
            }
            // 2. Check if it's a subcollection projection
            else
            {
                var subcollection = projection.Subcollections
                    .FirstOrDefault(s => s.ResultName.Equals(propName, StringComparison.OrdinalIgnoreCase));

                if (subcollection != null && !subcollection.IsAggregation)
                {
                    value = MaterializeSubcollection(
                        subcollection,
                        rootSnapshot,
                        allSnapshots,
                        aggregations,
                        prop.PropertyType);
                }
                // 3. Check if it's a projected field
                else
                {
                    var field = projection.Fields?
                        .FirstOrDefault(f => f.ResultName.Equals(propName, StringComparison.OrdinalIgnoreCase));

                    if (field != null)
                    {
                        // Handle Id field specially
                        if (field.FieldPath.Equals("Id", StringComparison.OrdinalIgnoreCase))
                        {
                            value = _valueConverter.FromFirestore(rootSnapshot.Id, prop.PropertyType);
                        }
                        else
                        {
                            // Use ResolveFieldValue to handle collection prefixes and Reference navigation
                            var fieldValue = ResolveFieldValue(field.FieldPath, data, rootSnapshot, allSnapshots);

                            // Handle full Reference entity projection (e.g., l.Autor where Autor is complete entity)
                            if (fieldValue is ReferenceSnapshotWrapper wrapper)
                            {
                                value = MaterializeFullEntity(wrapper.Snapshot, wrapper.Snapshot.ToDictionary(), prop.PropertyType);
                            }
                            else
                            {
                                value = _valueConverter.FromFirestore(fieldValue, prop.PropertyType);
                            }
                        }
                    }
                    // 4. Try direct field match from document
                    else if (data.TryGetValue(propName, out var directValue))
                    {
                        value = _valueConverter.FromFirestore(directValue, prop.PropertyType);
                    }
                    // 5. Handle Id specially
                    else if (propName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        value = _valueConverter.FromFirestore(rootSnapshot.Id, prop.PropertyType);
                    }
                }
            }

            if (value != null)
            {
                prop.SetValue(instance, value);
            }
        }

        return instance;
    }

    /// <inheritdoc />
    public IReadOnlyList<object> MaterializeMany(
        ResolvedProjectionDefinition projection,
        IEnumerable<DocumentSnapshot> rootSnapshots,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations)
    {
        return rootSnapshots
            .Select(snapshot => Materialize(projection, snapshot, allSnapshots, aggregations))
            .ToList();
    }

    private object? ResolveParameterValue(
        ParameterInfo param,
        ResolvedProjectionDefinition projection,
        DocumentSnapshot rootSnapshot,
        IDictionary<string, object> data,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations)
    {
        var paramName = param.Name!;

        // 1. Check if it's a subcollection aggregation
        var aggregationKey = $"{rootSnapshot.Reference.Path}:{paramName}";
        if (aggregations.TryGetValue(aggregationKey, out var aggValue))
        {
            return _valueConverter.FromFirestore(aggValue, param.ParameterType);
        }

        // 2. Check if it's a subcollection projection
        var subcollection = projection.Subcollections
            .FirstOrDefault(s => s.ResultName.Equals(paramName, StringComparison.OrdinalIgnoreCase));

        if (subcollection != null && !subcollection.IsAggregation)
        {
            return MaterializeSubcollection(
                subcollection,
                rootSnapshot,
                allSnapshots,
                aggregations,
                param.ParameterType);
        }

        // 3. Check if it's a projected field
        var field = projection.Fields?
            .FirstOrDefault(f => f.ResultName.Equals(paramName, StringComparison.OrdinalIgnoreCase));

        if (field != null)
        {
            // Handle Id field specially - it comes from DocumentSnapshot.Id, not from data
            if (field.FieldPath.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                return _valueConverter.FromFirestore(rootSnapshot.Id, param.ParameterType);
            }

            // Use ResolveFieldValue to handle collection prefixes and Reference navigation
            var fieldValue = ResolveFieldValue(field.FieldPath, data, rootSnapshot, allSnapshots);

            // Handle full Reference entity projection (e.g., l.Autor where Autor is complete entity)
            if (fieldValue is ReferenceSnapshotWrapper wrapper)
            {
                return MaterializeFullEntity(wrapper.Snapshot, wrapper.Snapshot.ToDictionary(), param.ParameterType);
            }

            return _valueConverter.FromFirestore(fieldValue, param.ParameterType);
        }

        // 4. Check if it's a nested anonymous type (fields or subcollections with prefix like "Resumen.TotalPedidos")
        var nestedPrefix = $"{paramName}.";
        var nestedFields = projection.Fields?
            .Where(f => f.ResultName.StartsWith(nestedPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Also check for subcollections with the prefix (for nested aggregations)
        var nestedSubcollections = projection.Subcollections
            .Where(s => s.ResultName.StartsWith(nestedPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (((nestedFields != null && nestedFields.Count > 0) || nestedSubcollections.Count > 0)
            && IsAnonymousType(param.ParameterType))
        {
            return MaterializeNestedAnonymousType(
                param.ParameterType,
                nestedPrefix,
                nestedFields,
                projection,
                rootSnapshot,
                data,
                allSnapshots,
                aggregations);
        }

        // 5. Try direct field match from document
        if (data.TryGetValue(paramName, out var directValue))
        {
            return _valueConverter.FromFirestore(directValue, param.ParameterType);
        }

        // 5. Handle Id specially
        if (paramName.Equals("Id", StringComparison.OrdinalIgnoreCase))
        {
            return _valueConverter.FromFirestore(rootSnapshot.Id, param.ParameterType);
        }

        // Return default for unmatched parameters
        return param.ParameterType.IsValueType
            ? Activator.CreateInstance(param.ParameterType)
            : null;
    }

    private object MaterializeSubcollection(
        ResolvedSubcollectionProjection subcollection,
        DocumentSnapshot parentSnapshot,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations,
        Type targetListType)
    {
        // Find all snapshots that belong to this subcollection
        var parentPath = parentSnapshot.Reference.Path;
        var subcollectionPrefix = $"{parentPath}/{subcollection.CollectionPath}/";

        // Calculate expected depth: parentPath depth + 2 (for collection name and document id)
        var expectedDepth = GetPathDepth(parentPath) + 2;

        var subcollectionSnapshots = allSnapshots
            .Where(kv => kv.Key.StartsWith(subcollectionPrefix, StringComparison.Ordinal))
            .Where(kv => GetPathDepth(kv.Key) == expectedDepth)
            .Select(kv => kv.Value)
            .ToList();

        // Determine the element type
        var elementType = GetListElementType(targetListType) ?? subcollection.TargetEntityType;

        // Materialize each item
        var items = new List<object>();
        foreach (var snapshot in subcollectionSnapshots)
        {
            var item = MaterializeSubcollectionItem(
                subcollection,
                snapshot,
                allSnapshots,
                aggregations,
                elementType);
            items.Add(item);
        }

        // Create the appropriate list type
        return CreateTypedCollection(items, elementType, targetListType);
    }

    private object MaterializeSubcollectionItem(
        ResolvedSubcollectionProjection subcollection,
        DocumentSnapshot snapshot,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots,
        IReadOnlyDictionary<string, object> aggregations,
        Type elementType)
    {
        var data = snapshot.ToDictionary();

        // If no specific fields, deserialize full entity
        if (subcollection.Fields == null || subcollection.Fields.Count == 0)
        {
            return MaterializeFullEntity(snapshot, data, elementType);
        }

        // If projecting a single primitive field (e.g., .Select(p => p.Total))
        // Return the value directly instead of trying to construct a complex type
        if (subcollection.Fields.Count == 1 && IsPrimitiveOrSimpleType(elementType))
        {
            var field = subcollection.Fields[0];
            var fieldValue = GetNestedValue(data, field.FieldPath);
            return _valueConverter.FromFirestore(fieldValue, elementType)!;
        }

        // Create projection with specific fields
        var constructor = GetBestConstructor(elementType);
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name!;

            // Check nested subcollection aggregations
            var nestedAggKey = $"{snapshot.Reference.Path}:{paramName}";
            if (aggregations.TryGetValue(nestedAggKey, out var nestedAggValue))
            {
                args[i] = _valueConverter.FromFirestore(nestedAggValue, param.ParameterType);
                continue;
            }

            // Check nested subcollections
            var nestedSubcollection = subcollection.NestedSubcollections
                .FirstOrDefault(s => s.ResultName.Equals(paramName, StringComparison.OrdinalIgnoreCase));

            if (nestedSubcollection != null && !nestedSubcollection.IsAggregation)
            {
                args[i] = MaterializeSubcollection(
                    nestedSubcollection,
                    snapshot,
                    allSnapshots,
                    aggregations,
                    param.ParameterType);
                continue;
            }

            // Check projected fields
            var field = subcollection.Fields
                .FirstOrDefault(f => f.ResultName.Equals(paramName, StringComparison.OrdinalIgnoreCase));

            if (field != null)
            {
                // Handle Id field specially - it comes from DocumentSnapshot.Id, not from data
                if (field.FieldPath.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = _valueConverter.FromFirestore(snapshot.Id, param.ParameterType);
                    continue;
                }

                var fieldValue = GetNestedValue(data, field.FieldPath);
                args[i] = _valueConverter.FromFirestore(fieldValue, param.ParameterType);
                continue;
            }

            // Direct field match
            if (data.TryGetValue(paramName, out var directValue))
            {
                args[i] = _valueConverter.FromFirestore(directValue, param.ParameterType);
                continue;
            }

            // Id handling
            if (paramName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                args[i] = _valueConverter.FromFirestore(snapshot.Id, param.ParameterType);
                continue;
            }

            // Default value
            args[i] = param.ParameterType.IsValueType
                ? Activator.CreateInstance(param.ParameterType)
                : null;
        }

        return constructor.Invoke(args);
    }

    private object MaterializeFullEntity(
        DocumentSnapshot snapshot,
        IDictionary<string, object> data,
        Type entityType)
    {
        var constructor = GetBestConstructor(entityType);
        var parameters = constructor.GetParameters();

        if (parameters.Length == 0)
        {
            // Parameterless constructor - use property setters
            var entity = Activator.CreateInstance(entityType)!;
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;

                object? value = null;
                if (prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    value = snapshot.Id;
                }
                else if (data.TryGetValue(prop.Name, out var propValue))
                {
                    value = propValue;
                }

                if (value != null)
                {
                    prop.SetValue(entity, _valueConverter.FromFirestore(value, prop.PropertyType));
                }
            }
            return entity;
        }

        // Constructor with parameters
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name!;

            if (paramName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                args[i] = _valueConverter.FromFirestore(snapshot.Id, param.ParameterType);
            }
            else if (data.TryGetValue(paramName, out var paramValue))
            {
                args[i] = _valueConverter.FromFirestore(paramValue, param.ParameterType);
            }
            else
            {
                args[i] = param.ParameterType.IsValueType
                    ? Activator.CreateInstance(param.ParameterType)
                    : null;
            }
        }

        return constructor.Invoke(args);
    }

    private static ConstructorInfo GetBestConstructor(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        // Prefer constructor with most parameters (typical for anonymous types and records)
        return constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
    }

    private static object? GetNestedValue(IDictionary<string, object> data, string path)
    {
        var segments = path.Split('.');
        object? current = data;

        foreach (var segment in segments)
        {
            if (current is IDictionary<string, object> dict)
            {
                if (!dict.TryGetValue(segment, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Resolves a field value from the appropriate source, handling collection prefixes.
    /// Paths like "Libros.Titulo" are resolved from root data, paths like "Autors.Nombre"
    /// are resolved from the referenced document in allSnapshots.
    /// Full entity paths like "Autors" return the complete snapshot dictionary for materialization.
    /// Supports nested references (e.g., PaisEntities.Nombre where Pais is referenced from Autor).
    /// </summary>
    private object? ResolveFieldValue(
        string fieldPath,
        IDictionary<string, object> rootData,
        DocumentSnapshot rootSnapshot,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots)
    {
        var dotIndex = fieldPath.IndexOf('.');
        if (dotIndex <= 0)
        {
            // No dot - could be direct field access or full Reference entity projection
            var directValue = GetNestedValue(rootData, fieldPath);
            if (directValue != null)
            {
                return directValue;
            }

            // Check if fieldPath is a collection name for a full Reference entity projection
            // e.g., fieldPath = "Autors" when projecting l.Autor (full entity)
            // Look for a DocumentReference in rootData that points to this collection
            var result = FindReferenceInData(fieldPath, null, rootData, allSnapshots);
            if (result != null)
                return result;

            return null;
        }

        var prefix = fieldPath[..dotIndex];
        var remainder = fieldPath[(dotIndex + 1)..];

        // Check if the prefix corresponds to a DocumentReference in rootData
        // This handles Reference navigation fields like "Autor" storing a DocumentReference
        var refResult = FindReferenceInData(prefix, remainder, rootData, allSnapshots);
        if (refResult != null)
            return refResult;

        // Not a reference prefix - might be a root collection prefix or ComplexType
        // Try to get from rootData directly first (handles ComplexType paths like "Direccion.Ciudad")
        var directValueNested = GetNestedValue(rootData, fieldPath);
        if (directValueNested != null)
        {
            return directValueNested;
        }

        // If not found, try stripping the prefix (handles root collection prefix like "Libros.Titulo")
        // Only do this if the prefix looks like a collection name (matches root collection pattern)
        var rootCollectionName = ExtractCollectionNameFromPath(rootSnapshot.Reference.Path);
        if (prefix.Equals(rootCollectionName, StringComparison.OrdinalIgnoreCase))
        {
            return GetNestedValue(rootData, remainder);
        }

        return null;
    }

    /// <summary>
    /// Finds a DocumentReference matching the collection prefix and resolves the field from it.
    /// Searches recursively through all snapshots to handle nested references.
    /// </summary>
    private object? FindReferenceInData(
        string collectionPrefix,
        string? fieldRemainder,
        IDictionary<string, object> data,
        IReadOnlyDictionary<string, DocumentSnapshot> allSnapshots)
    {
        // First, check direct references in the current data
        foreach (var kvp in data)
        {
            if (kvp.Value is DocumentReference docRef)
            {
                var refCollectionName = ExtractCollectionNameFromPath(docRef.Path);
                if (collectionPrefix.Equals(refCollectionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (allSnapshots.TryGetValue(docRef.Path, out var refSnapshot))
                    {
                        if (fieldRemainder == null)
                        {
                            // Full entity projection
                            return new ReferenceSnapshotWrapper(refSnapshot);
                        }
                        var refData = refSnapshot.ToDictionary();
                        return GetNestedValue(refData, fieldRemainder);
                    }
                    return null;
                }
            }
        }

        // Not found in current data - search in referenced documents (nested references)
        foreach (var kvp in data)
        {
            if (kvp.Value is DocumentReference docRef)
            {
                if (allSnapshots.TryGetValue(docRef.Path, out var refSnapshot))
                {
                    var refData = refSnapshot.ToDictionary();
                    var nestedResult = FindReferenceInData(collectionPrefix, fieldRemainder, refData, allSnapshots);
                    if (nestedResult != null)
                        return nestedResult;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Wrapper class to identify when a full Reference entity snapshot should be materialized.
    /// </summary>
    private sealed class ReferenceSnapshotWrapper
    {
        public DocumentSnapshot Snapshot { get; }
        public ReferenceSnapshotWrapper(DocumentSnapshot snapshot) => Snapshot = snapshot;
    }

    /// <summary>
    /// Extracts the collection name from a Firestore document path.
    /// "projects/p/databases/d/documents/Libros/doc-id" → "Libros"
    /// "Autors/autor-id" → "Autors"
    /// </summary>
    private static string ExtractCollectionNameFromPath(string path)
    {
        // Handle full path with "/documents/" prefix
        const string marker = "/documents/";
        var markerIndex = path.IndexOf(marker, StringComparison.Ordinal);
        var relativePath = markerIndex >= 0 ? path[(markerIndex + marker.Length)..] : path;

        // Collection name is the first segment
        var slashIndex = relativePath.IndexOf('/');
        return slashIndex > 0 ? relativePath[..slashIndex] : relativePath;
    }

    private static Type? GetListElementType(Type listType)
    {
        if (listType.IsGenericType)
        {
            var genericDef = listType.GetGenericTypeDefinition();
            if (genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(IReadOnlyList<>) ||
                genericDef == typeof(ICollection<>))
            {
                return listType.GetGenericArguments()[0];
            }
        }

        return listType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static object CreateTypedCollection(List<object> items, Type elementType, Type targetCollectionType)
    {
        // Determine which concrete collection type to create based on targetCollectionType
        Type concreteType;

        if (targetCollectionType.IsGenericType)
        {
            var genericDef = targetCollectionType.GetGenericTypeDefinition();

            if (genericDef == typeof(HashSet<>) ||
                genericDef == typeof(ISet<>))
            {
                concreteType = typeof(HashSet<>).MakeGenericType(elementType);
            }
            else if (genericDef == typeof(List<>) ||
                     genericDef == typeof(IList<>) ||
                     genericDef == typeof(ICollection<>) ||
                     genericDef == typeof(IEnumerable<>) ||
                     genericDef == typeof(IReadOnlyList<>) ||
                     genericDef == typeof(IReadOnlyCollection<>))
            {
                concreteType = typeof(List<>).MakeGenericType(elementType);
            }
            else
            {
                // Try to use the target type directly if it's a concrete type
                concreteType = targetCollectionType.IsClass && !targetCollectionType.IsAbstract
                    ? targetCollectionType
                    : typeof(List<>).MakeGenericType(elementType);
            }
        }
        else
        {
            // Fallback to List<T>
            concreteType = typeof(List<>).MakeGenericType(elementType);
        }

        var collection = Activator.CreateInstance(concreteType)!;
        var addMethod = concreteType.GetMethod("Add")!;

        foreach (var item in items)
        {
            addMethod.Invoke(collection, new[] { item });
        }

        return collection;
    }

    private static int GetPathDepth(string path)
    {
        return path.Count(c => c == '/');
    }
}
