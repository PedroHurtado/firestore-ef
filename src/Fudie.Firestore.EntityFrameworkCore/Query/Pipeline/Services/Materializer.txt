using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Fudie.Firestore.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts shaped query results (hierarchical dictionaries) into typed CLR instances.
/// Supports DDD patterns: private/protected constructors, backing fields, partial constructors.
/// </summary>
public class Materializer : IMaterializer
{
    private static readonly ConcurrentDictionary<Type, Lazy<MaterializationStrategy>> _strategyCache = new();

    private readonly IFirestoreValueConverter _converter;

    // Subcollection field mappings for nested materialization
    private IReadOnlyDictionary<string, IReadOnlyList<FirestoreProjectedField>>? _subcollectionFieldMappings;

    public Materializer(IFirestoreValueConverter converter)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    /// <inheritdoc />
    public List<object> Materialize(
        ShapedResult shaped,
        Type targetType,
        IReadOnlyList<FirestoreProjectedField>? projectedFields = null,
        IReadOnlyList<ResolvedSubcollectionProjection>? subcollections = null)
    {
        ArgumentNullException.ThrowIfNull(shaped);
        ArgumentNullException.ThrowIfNull(targetType);

        // Build subcollection field mappings for nested materialization
        _subcollectionFieldMappings = BuildSubcollectionFieldMappings(subcollections);

        // Handle scalar projections (e.g., Select(e => e.Name) where targetType is string)
        // In this case, the dictionary has a single value that needs to be extracted directly
        if (IsSimpleType(targetType))
        {
            return MaterializeScalarProjection(shaped, targetType);
        }

        // Handle direct collection projections (e.g., Select(e => e.Cantidades) where targetType is List<int>)
        // In this case, the dictionary has a single value that is the collection
        if (IsCollectionType(targetType))
        {
            return MaterializeDirectCollection(shaped, targetType);
        }

        // Handle direct ComplexType projection (e.g., Select(p => p.Direccion) where targetType is Direccion)
        // projectedFields has one field: FieldPath = "Direccion", FieldType = Direccion
        // shapedResult has: { "Direccion.Calle": "...", "Direccion.Ciudad": "..." }
        // We need to extract the nested dictionary for "Direccion" before materializing
        if (projectedFields is { Count: 1 } &&
            projectedFields[0].FieldPath == projectedFields[0].ResultName &&
            projectedFields[0].FieldType == targetType &&
            !IsSimpleType(targetType))
        {
            return MaterializeDirectComplexType(shaped, targetType, projectedFields[0].FieldPath);
        }

        var strategy = GetOrCreateStrategy(targetType);

        // Build field mapping for projections: ResultName -> FieldPath
        // ResultName is the constructor parameter name (e.g., "Ciudad")
        // FieldPath is the flattened dictionary key (e.g., "Direccion.Ciudad")
        Dictionary<string, string>? fieldMapping = null;
        if (projectedFields is { Count: > 0 })
        {
            fieldMapping = projectedFields.ToDictionary(
                f => ToPascalCase(f.ResultName),
                f => f.FieldPath,
                StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<object>(shaped.Items.Count);

        foreach (var dict in shaped.Items)
        {
            var instance = ExecuteStrategy(dict, strategy, fieldMapping);
            results.Add(instance);
        }

        return results;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<FirestoreProjectedField>>? BuildSubcollectionFieldMappings(
        IReadOnlyList<ResolvedSubcollectionProjection>? subcollections)
    {
        if (subcollections == null || subcollections.Count == 0)
            return null;

        var mappings = new Dictionary<string, IReadOnlyList<FirestoreProjectedField>>(StringComparer.OrdinalIgnoreCase);

        foreach (var subcol in subcollections)
        {
            if (subcol.Fields is { Count: > 0 })
            {
                mappings[subcol.ResultName] = subcol.Fields;
            }
        }

        return mappings.Count > 0 ? mappings : null;
    }

    /// <summary>
    /// Materializes direct ComplexType projections where the result type is a ComplexType.
    /// The dictionary has prefixed keys like "Direccion.Calle" that need to be extracted.
    /// </summary>
    private List<object> MaterializeDirectComplexType(ShapedResult shaped, Type targetType, string prefix)
    {
        var results = new List<object>(shaped.Items.Count);
        var strategy = GetOrCreateStrategy(targetType);

        foreach (var dict in shaped.Items)
        {
            // Extract nested dictionary using the prefix (e.g., "Direccion")
            var nestedDict = GetValueOrNestedDict(dict, prefix);

            if (nestedDict is Dictionary<string, object?> nested)
            {
                var instance = ExecuteStrategy(nested, strategy);
                results.Add(instance);
            }
        }

        return results;
    }

    /// <summary>
    /// Materializes scalar projections where the result type is a simple type (string, int, etc.)
    /// Each dictionary should have a single value to extract.
    /// </summary>
    private List<object> MaterializeScalarProjection(ShapedResult shaped, Type targetType)
    {
        var results = new List<object>(shaped.Items.Count);

        foreach (var dict in shaped.Items)
        {
            // For scalar projections, extract the single value from the dictionary
            var value = dict.Values.FirstOrDefault();
            var converted = _converter.FromFirestore(value, targetType);
            if (converted != null)
            {
                results.Add(converted);
            }
        }

        return results;
    }

    /// <summary>
    /// Materializes direct collection projections where the result type is a collection (List, array, etc.)
    /// Each dictionary should have a single value that is the collection.
    /// </summary>
    private List<object> MaterializeDirectCollection(ShapedResult shaped, Type targetType)
    {
        var results = new List<object>(shaped.Items.Count);

        foreach (var dict in shaped.Items)
        {
            // For collection projections, extract the single value that should be the collection
            var value = dict.Values.FirstOrDefault();

            if (value is IList list)
            {
                var materialized = MaterializeCollection(list, targetType);
                results.Add(materialized);
            }
        }

        return results;
    }

    /// <summary>
    /// Determines if a type is a simple/primitive type that doesn't need materialization strategy.
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum ||
               (Nullable.GetUnderlyingType(type) is { } underlying && IsSimpleType(underlying));
    }

    private static MaterializationStrategy GetOrCreateStrategy(Type type)
    {
        var lazy = _strategyCache.GetOrAdd(type, t => new Lazy<MaterializationStrategy>(() => DiscoverStrategy(t)));
        return lazy.Value;
    }

    private static MaterializationStrategy DiscoverStrategy(Type type)
    {
        var constructor = FindBestConstructor(type);
        var parameters = constructor.GetParameters();
        var constructorParams = new List<ConstructorParamMapping>(parameters.Length);

        // Map constructor parameters (camelCase param name -> PascalCase dict key)
        var paramNamesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var dictKey = ToPascalCase(param.Name!);
            constructorParams.Add(new ConstructorParamMapping(dictKey, i, param.ParameterType));
            paramNamesUsed.Add(dictKey);
        }

        // Find properties not covered by constructor
        var memberSetters = new List<MemberMapping>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (paramNamesUsed.Contains(prop.Name))
                continue;

            // Skip indexers
            if (prop.GetIndexParameters().Length > 0)
                continue;

            // Try public setter first
            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                memberSetters.Add(new MemberMapping(prop.Name, prop, prop.PropertyType));
                continue;
            }

            // Try backing field
            var backingFieldName = GetBackingFieldName(prop.Name);
            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
            {
                memberSetters.Add(new MemberMapping(prop.Name, backingField, backingField.FieldType));
            }
        }

        return new MaterializationStrategy(constructor, constructorParams, memberSetters);
    }

    private static ConstructorInfo FindBestConstructor(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (constructors.Length == 0)
            throw new InvalidOperationException($"Type '{type.FullName}' has no constructors.");

        // Filter out copy constructors (records generate these with signature Type(Type original))
        var validConstructors = constructors
            .Where(c => !IsCopyConstructor(c, type))
            .ToList();

        if (validConstructors.Count == 0)
            throw new InvalidOperationException($"Type '{type.FullName}' has no valid constructors (only copy constructor found).");

        // Prefer constructor with most parameters (more specific)
        return validConstructors.OrderByDescending(c => c.GetParameters().Length).First();
    }

    private static bool IsCopyConstructor(ConstructorInfo constructor, Type type)
    {
        var parameters = constructor.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == type;
    }

    private static string GetBackingFieldName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private object ExecuteStrategy(Dictionary<string, object?> dict, MaterializationStrategy strategy, Dictionary<string, string>? fieldMapping = null)
    {
        // Build constructor arguments
        var args = new object?[strategy.ConstructorParams.Count];
        foreach (var mapping in strategy.ConstructorParams)
        {
            // For projections, map ResultName (DictKey) to FieldPath (actual dictionary key)
            var dictKey = fieldMapping != null && fieldMapping.TryGetValue(mapping.DictKey, out var mappedKey)
                ? mappedKey
                : mapping.DictKey;

            var value = GetValueOrNestedDict(dict, dictKey);
            args[mapping.ParamIndex] = MaterializeValue(value, mapping.TargetType, mapping.DictKey);
        }

        // Create instance
        var instance = strategy.Constructor.Invoke(args);

        // Set remaining members
        foreach (var mapping in strategy.MemberSetters)
        {
            // For projections, map ResultName (DictKey) to FieldPath (actual dictionary key)
            var dictKey = fieldMapping != null && fieldMapping.TryGetValue(mapping.DictKey, out var mappedKey)
                ? mappedKey
                : mapping.DictKey;

            var value = GetValueOrNestedDict(dict, dictKey);
            var materializedValue = MaterializeValue(value, mapping.TargetType, mapping.DictKey);

            // Skip setting null values for collection types - preserve field initializers (e.g., = [])
            if (materializedValue == null && IsCollectionType(mapping.TargetType))
                continue;

            if (mapping.Member is PropertyInfo prop)
            {
                prop.SetValue(instance, materializedValue);
            }
            else if (mapping.Member is FieldInfo field)
            {
                field.SetValue(instance, materializedValue);
            }
        }

        return instance;
    }

    /// <summary>
    /// Gets a value from the dictionary, or builds a nested dictionary from prefixed keys.
    /// For example, if dictKey is "Resumen" and dict has "Resumen.TotalPedidos" and "Resumen.Cantidad",
    /// returns a new dictionary with { "TotalPedidos": ..., "Cantidad": ... }
    /// </summary>
    private static object? GetValueOrNestedDict(Dictionary<string, object?> dict, string dictKey)
    {
        // First try exact match
        if (dict.TryGetValue(dictKey, out var value))
            return value;

        // Look for prefixed keys (e.g., "Resumen.TotalPedidos" for key "Resumen")
        var prefix = dictKey + ".";
        var nestedDict = new Dictionary<string, object?>();

        foreach (var kvp in dict)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                var nestedKey = kvp.Key.Substring(prefix.Length);
                nestedDict[nestedKey] = kvp.Value;
            }
        }

        return nestedDict.Count > 0 ? nestedDict : null;
    }

    private object? MaterializeValue(object? value, Type targetType, string? propertyName = null)
    {
        if (value == null)
            return GetDefaultValue(targetType);

        // Nested entity/ComplexType (dictionary)
        if (value is Dictionary<string, object?> nestedDict)
        {
            // If targetType is simple but value is a single-key dictionary,
            // extract the value (e.g., { "Total": 299.99 } -> 299.99 for decimal)
            if (IsSimpleType(targetType) && nestedDict.Count == 1)
            {
                var singleValue = nestedDict.Values.First();
                return _converter.FromFirestore(singleValue, targetType);
            }

            var strategy = GetOrCreateStrategy(targetType);
            return ExecuteStrategy(nestedDict, strategy);
        }

        // Collection (entities/ComplexTypes or simple values)
        if (value is IList list && IsCollectionType(targetType))
        {
            return MaterializeCollection(list, targetType, propertyName);
        }

        // Simple value - use converter
        return _converter.FromFirestore(value, targetType);
    }

    private static bool IsCollectionType(Type type)
    {
        if (type.IsArray)
            return true;

        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(List<>) ||
               genericDef == typeof(IList<>) ||
               genericDef == typeof(ICollection<>) ||
               genericDef == typeof(IEnumerable<>) ||
               genericDef == typeof(IReadOnlyCollection<>) ||
               genericDef == typeof(IReadOnlyList<>) ||
               genericDef == typeof(HashSet<>) ||
               genericDef == typeof(ISet<>);
    }

    private bool IsComplexType(Type elementType)
    {
        // Simple types are handled by the converter
        return !elementType.IsPrimitive &&
               elementType != typeof(string) &&
               elementType != typeof(decimal) &&
               elementType != typeof(DateTime) &&
               elementType != typeof(Guid) &&
               !elementType.IsEnum;
    }

    private object MaterializeCollection(IList items, Type collectionType, string? propertyName = null)
    {
        var elementType = GetCollectionElementType(collectionType);
        if (elementType == null)
            throw new InvalidOperationException($"Cannot determine element type for collection type '{collectionType.FullName}'.");

        // Check if this subcollection has field mappings
        Dictionary<string, string>? subcollectionFieldMapping = null;
        if (propertyName != null &&
            _subcollectionFieldMappings != null &&
            _subcollectionFieldMappings.TryGetValue(propertyName, out var subcollectionFields))
        {
            subcollectionFieldMapping = subcollectionFields.ToDictionary(
                f => ToPascalCase(f.ResultName),
                f => f.FieldPath,
                StringComparer.OrdinalIgnoreCase);
        }

        // Create the appropriate collection type
        var collection = CreateCollection(collectionType, elementType);

        foreach (var item in items)
        {
            object? materializedItem;

            // If item is a dictionary and we have subcollection field mapping, use it
            if (item is Dictionary<string, object?> dictItem && subcollectionFieldMapping != null)
            {
                var strategy = GetOrCreateStrategy(elementType);
                materializedItem = ExecuteStrategy(dictItem, strategy, subcollectionFieldMapping);
            }
            else
            {
                materializedItem = MaterializeValue(item, elementType);
            }

            AddToCollection(collection, materializedItem);
        }

        // Handle arrays - need to convert from List<T>
        if (collectionType.IsArray)
        {
            var toArrayMethod = collection.GetType().GetMethod("ToArray")!;
            return toArrayMethod.Invoke(collection, null)!;
        }

        return collection;
    }

    private static object CreateCollection(Type collectionType, Type elementType)
    {
        // Arrays - use List<T> as intermediate, convert later
        if (collectionType.IsArray)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(listType)!;
        }

        // HashSet<T>
        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            return Activator.CreateInstance(collectionType)!;
        }

        // ISet<T> -> HashSet<T>
        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(ISet<>))
        {
            var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
            return Activator.CreateInstance(hashSetType)!;
        }

        // List<T>, IList<T>, ICollection<T>, IEnumerable<T>, IReadOnlyCollection<T>, IReadOnlyList<T> -> List<T>
        var concreteListType = typeof(List<>).MakeGenericType(elementType);
        return Activator.CreateInstance(concreteListType)!;
    }

    private static void AddToCollection(object collection, object? item)
    {
        switch (collection)
        {
            case IList list:
                list.Add(item);
                break;
            default:
                // For HashSet<T> and other collections, use Add method via reflection
                var addMethod = collection.GetType().GetMethod("Add")!;
                addMethod.Invoke(collection, [item]);
                break;
        }
    }

    private static Type? GetCollectionElementType(Type collectionType)
    {
        // Handle List<T>, IList<T>, IReadOnlyCollection<T>, IEnumerable<T>, ICollection<T>
        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length == 1)
                return genericArgs[0];
        }

        // Handle arrays
        if (collectionType.IsArray)
            return collectionType.GetElementType();

        // Try to find IEnumerable<T> interface
        var enumerableInterface = collectionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0];
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
