using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
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

    public Materializer(IFirestoreValueConverter converter)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    /// <inheritdoc />
    public List<object> Materialize(ShapedResult shaped, Type targetType, IReadOnlyList<FirestoreProjectedField>? projectedFields = null)
    {
        ArgumentNullException.ThrowIfNull(shaped);
        ArgumentNullException.ThrowIfNull(targetType);

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

            dict.TryGetValue(dictKey, out var value);
            args[mapping.ParamIndex] = MaterializeValue(value, mapping.TargetType);
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

            dict.TryGetValue(dictKey, out var value);
            var materializedValue = MaterializeValue(value, mapping.TargetType);

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

    private object? MaterializeValue(object? value, Type targetType)
    {
        if (value == null)
            return GetDefaultValue(targetType);

        // Nested entity/ComplexType (dictionary)
        if (value is Dictionary<string, object?> nestedDict)
        {
            var strategy = GetOrCreateStrategy(targetType);
            return ExecuteStrategy(nestedDict, strategy);
        }

        // Collection (entities/ComplexTypes or simple values)
        if (value is IList list && IsCollectionType(targetType))
        {
            return MaterializeCollection(list, targetType);
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

    private object MaterializeCollection(IList items, Type collectionType)
    {
        var elementType = GetCollectionElementType(collectionType);
        if (elementType == null)
            throw new InvalidOperationException($"Cannot determine element type for collection type '{collectionType.FullName}'.");

        // Create the appropriate collection type
        var collection = CreateCollection(collectionType, elementType);

        foreach (var item in items)
        {
            var materializedItem = MaterializeValue(item, elementType);
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
