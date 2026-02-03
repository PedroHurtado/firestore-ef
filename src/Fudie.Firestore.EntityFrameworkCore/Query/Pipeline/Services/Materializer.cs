using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts shaped query results into typed CLR instances.
/// Uses ShapedItem metadata (TargetType, Kind, ResultName) - no AST dependency.
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
    public List<object> Materialize(ShapedResult shaped, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(shaped);
        ArgumentNullException.ThrowIfNull(targetType);

        // Handle scalar projections (e.g., Select(e => e.Name) where targetType is string)
        if (IsSimpleType(targetType))
        {
            return MaterializeScalarProjection(shaped, targetType);
        }

        // Handle direct collection projections (e.g., Select(e => e.Cantidades) where targetType is List<int>)
        if (IsCollectionType(targetType))
        {
            return MaterializeDirectCollection(shaped, targetType);
        }

        var results = new List<object>(shaped.TypedItems.Count);

        foreach (var shapedItem in shaped.TypedItems)
        {
            var itemToMaterialize = shapedItem;

            // Handle direct ComplexType/Entity projection (e.g., Select(p => p.Direccion))
            // When the ShapedItem is a wrapper with a single nested ComplexType/Entity
            if (shaped.HasProjection && shapedItem.Values.Count == 1)
            {
                var singleValue = shapedItem.Values.Values.First();
                if ((singleValue.Kind == ValueKind.ComplexType || singleValue.Kind == ValueKind.Entity)
                    && singleValue.Value is ShapedItem nestedItem
                    && string.Equals(singleValue.ResultName, targetType.Name, StringComparison.OrdinalIgnoreCase))
                {
                    itemToMaterialize = nestedItem;
                }
            }

            var instance = MaterializeItem(itemToMaterialize, targetType);
            results.Add(instance);
        }

        return results;
    }

    private object MaterializeItem(ShapedItem item, Type targetType)
    {
        var strategy = GetOrCreateStrategy(targetType);

        // Build constructor arguments
        var args = new object?[strategy.ConstructorParams.Count];
        var usedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in strategy.ConstructorParams)
        {
            var shapedValue = FindShapedValue(item, param.DictKey);
            if (shapedValue != null)
            {
                // Use strategy's TargetType for proper type conversion
                args[param.ParamIndex] = MaterializeValueWithTargetType(shapedValue, param.TargetType);
                usedProperties.Add(param.DictKey);
            }
            else
            {
                // Property not found - for collections create empty (Firestore doesn't store empty arrays)
                args[param.ParamIndex] = IsCollectionType(param.TargetType)
                    ? FinalizeCollection(CreateCollection(param.TargetType, GetCollectionElementType(param.TargetType) ?? typeof(object)), param.TargetType)
                    : GetDefaultValue(param.TargetType);
            }
        }

        // Create instance
        var instance = strategy.Constructor.Invoke(args);

        // Set remaining members
        foreach (var member in strategy.MemberSetters)
        {
            if (usedProperties.Contains(member.DictKey))
                continue;

            var shapedValue = FindShapedValue(item, member.DictKey);
            if (shapedValue == null)
                continue;

            // Use strategy's TargetType for proper type conversion
            var materializedValue = MaterializeValueWithTargetType(shapedValue, member.TargetType);

            // Skip setting null values for collection types - preserve field initializers (e.g., = [])
            if (materializedValue == null && IsCollectionType(member.TargetType))
                continue;

            if (member.Member is PropertyInfo prop)
            {
                prop.SetValue(instance, materializedValue);
            }
            else if (member.Member is FieldInfo field)
            {
                field.SetValue(instance, materializedValue);
            }
        }

        return instance;
    }

    /// <summary>
    /// Materializes a value using the target type from the strategy (not from ShapedValue).
    /// This ensures proper type conversion (e.g., List&lt;PedidoConVendedor&gt; instead of IEnumerable&lt;object&gt;).
    /// </summary>
    private object? MaterializeValueWithTargetType(ShapedValue shaped, Type targetType)
    {
        if (shaped.Value == null)
        {
            // For collection types, return empty collection (Firestore doesn't store empty arrays)
            if (IsCollectionType(targetType))
            {
                var elementType = GetCollectionElementType(targetType) ?? typeof(object);
                var emptyCollection = CreateCollection(targetType, elementType);
                return FinalizeCollection(emptyCollection, targetType);
            }
            return GetDefaultValue(targetType);
        }

        // Check if target type is a dictionary - if so, materialize as Map
        if (IsDictionaryType(targetType))
        {
            return MaterializeMapWithTarget(shaped, targetType);
        }

        return shaped.Kind switch
        {
            ValueKind.Scalar => _converter.FromFirestore(shaped.Value, targetType),
            ValueKind.ComplexType => MaterializeComplexTypeWithTarget(shaped, targetType),
            ValueKind.Entity => MaterializeEntityWithTarget(shaped, targetType),
            ValueKind.ObjectList => MaterializeObjectListWithTarget(shaped, targetType),
            ValueKind.ScalarList => MaterializeScalarListWithTarget(shaped, targetType),
            ValueKind.Map => MaterializeMapWithTarget(shaped, targetType),
            _ => _converter.FromFirestore(shaped.Value, targetType)
        };
    }

    private object? MaterializeComplexTypeWithTarget(ShapedValue shaped, Type targetType)
    {
        if (shaped.Value is ShapedItem nestedItem)
        {
            return MaterializeItem(nestedItem, targetType);
        }

        if (shaped.Value is Dictionary<string, object?> dict)
        {
            var item = ConvertDictToShapedItem(dict);
            return MaterializeItem(item, targetType);
        }

        return null;
    }

    private object? MaterializeEntityWithTarget(ShapedValue shaped, Type targetType)
    {
        if (shaped.Value is ShapedItem nestedItem)
        {
            return MaterializeItem(nestedItem, targetType);
        }

        return null;
    }

    private object? MaterializeScalarListWithTarget(ShapedValue shaped, Type targetType)
    {
        if (shaped.Value is not IList list)
            return _converter.FromFirestore(shaped.Value, targetType);

        var elementType = GetCollectionElementType(targetType) ?? typeof(object);
        var collection = CreateCollection(targetType, elementType);

        foreach (var item in list)
        {
            var converted = _converter.FromFirestore(item, elementType);
            AddToCollection(collection, converted);
        }

        return FinalizeCollection(collection, targetType);
    }

    private object? MaterializeObjectListWithTarget(ShapedValue shaped, Type targetType)
    {
        var elementType = GetCollectionElementType(targetType) ?? typeof(object);

        if (shaped.Value is IList<ShapedItem> shapedItems)
        {
            var collection = CreateCollection(targetType, elementType);

            foreach (var item in shapedItems)
            {
                var materialized = MaterializeItem(item, elementType);
                AddToCollection(collection, materialized);
            }

            return FinalizeCollection(collection, targetType);
        }

        if (shaped.Value is IList list)
        {
            var collection = CreateCollection(targetType, elementType);

            foreach (var item in list)
            {
                object? materialized;
                if (item is ShapedItem si)
                {
                    materialized = MaterializeItem(si, elementType);
                }
                else if (item is Dictionary<string, object?> dict)
                {
                    var shapedItem = ConvertDictToShapedItem(dict);
                    materialized = MaterializeItem(shapedItem, elementType);
                }
                else
                {
                    materialized = _converter.FromFirestore(item, elementType);
                }
                AddToCollection(collection, materialized);
            }

            return FinalizeCollection(collection, targetType);
        }

        return null;
    }

    /// <summary>
    /// Materializes a Firestore Map into a CLR dictionary (IReadOnlyDictionary, Dictionary, etc.).
    /// The map values can be complex types, scalar values, or nested arrays.
    /// </summary>
    private object? MaterializeMapWithTarget(ShapedValue shaped, Type targetType)
    {
        // Get key and value types from target dictionary type
        var (keyType, valueType) = GetDictionaryKeyValueTypes(targetType);
        if (keyType == null || valueType == null)
            return null;

        // Get the raw dictionary data
        IDictionary<string, object?>? rawDict = shaped.Value switch
        {
            ShapedItem shapedItem => shapedItem.Values.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Value),
            Dictionary<string, object?> dict => dict,
            IDictionary<string, object> dict => dict.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value),
            _ => null
        };

        if (rawDict == null)
            return null;

        // Create the target dictionary
        var concreteDict = CreateDictionary(targetType, keyType, valueType);

        foreach (var kvp in rawDict)
        {
            // Convert key from string to target key type (e.g., string → DayOfWeek enum)
            var convertedKey = _converter.FromFirestore(kvp.Key, keyType);
            if (convertedKey == null)
                continue;

            // Materialize value based on type
            object? convertedValue;
            if (kvp.Value is ShapedValue sv)
            {
                convertedValue = MaterializeValueWithTargetType(sv, valueType);
            }
            else if (kvp.Value is ShapedItem nestedItem)
            {
                convertedValue = MaterializeItem(nestedItem, valueType);
            }
            else if (kvp.Value is Dictionary<string, object?> nestedDict)
            {
                var nestedShapedItem = ConvertDictToShapedItem(nestedDict);
                convertedValue = MaterializeItem(nestedShapedItem, valueType);
            }
            else if (IsSimpleType(valueType))
            {
                convertedValue = _converter.FromFirestore(kvp.Value, valueType);
            }
            else
            {
                // Complex type from raw dictionary
                if (kvp.Value is IDictionary<string, object> objDict)
                {
                    var dictWithNullable = objDict.ToDictionary(k => k.Key, k => (object?)k.Value);
                    var nestedShapedItem = ConvertDictToShapedItem(dictWithNullable);
                    convertedValue = MaterializeItem(nestedShapedItem, valueType);
                }
                else
                {
                    convertedValue = _converter.FromFirestore(kvp.Value, valueType);
                }
            }

            AddToDictionary(concreteDict, convertedKey, convertedValue);
        }

        return FinalizeDictionary(concreteDict, targetType);
    }

    /// <summary>
    /// Finds a ShapedValue by ResultName (for projections) or by key (for entities).
    /// ResultName always has a value - in projections it's the property name, in entities it equals the key.
    /// </summary>
    private static ShapedValue? FindShapedValue(ShapedItem item, string propertyName)
    {
        // First try direct key match (entity case: key = "Nombre", ResultName = "Nombre")
        if (item.Values.TryGetValue(propertyName, out var direct))
            return direct;

        // Then try by ResultName (projection case: key = "Direccion.Ciudad", ResultName = "Ciudad")
        foreach (var kvp in item.Values)
        {
            if (string.Equals(kvp.Value.ResultName, propertyName, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    private object? MaterializeValue(ShapedValue shaped)
    {
        if (shaped.Value == null)
            return GetDefaultValue(shaped.TargetType);

        return shaped.Kind switch
        {
            ValueKind.Scalar => _converter.FromFirestore(shaped.Value, shaped.TargetType),
            ValueKind.ComplexType => MaterializeComplexTypeWithTarget(shaped, shaped.TargetType),
            ValueKind.Entity => MaterializeEntityWithTarget(shaped, shaped.TargetType),
            ValueKind.ObjectList => MaterializeObjectListWithTarget(shaped, shaped.TargetType),
            ValueKind.ScalarList => _converter.FromFirestore(shaped.Value, shaped.TargetType),
            _ => _converter.FromFirestore(shaped.Value, shaped.TargetType)
        };
    }

    /// <summary>
    /// Converts a raw dictionary to ShapedItem (fallback for edge cases).
    /// Key is used as both FieldPath and ResultName since we don't have AST info.
    /// </summary>
    private static ShapedItem ConvertDictToShapedItem(Dictionary<string, object?> dict)
    {
        var item = new ShapedItem();
        foreach (var kvp in dict)
        {
            var kind = InferValueKind(kvp.Value);
            var type = InferType(kvp.Value);
            // Key serves as both FieldPath (dict key) and ResultName
            item.Values[kvp.Key] = new ShapedValue(kvp.Value, type, kind, kvp.Key);
        }
        return item;
    }

    private static ValueKind InferValueKind(object? value) => value switch
    {
        null => ValueKind.Scalar,
        string => ValueKind.Scalar,
        long => ValueKind.Scalar,
        double => ValueKind.Scalar,
        bool => ValueKind.Scalar,
        Dictionary<string, object?> => ValueKind.ComplexType,
        IList<Dictionary<string, object?>> => ValueKind.ObjectList,
        IList<object> list when list.Count > 0 && list[0] is Dictionary<string, object?> => ValueKind.ObjectList,
        IList<object> => ValueKind.ScalarList,
        _ => ValueKind.Scalar
    };

    private static Type InferType(object? value) => value switch
    {
        null => typeof(object),
        string => typeof(string),
        long => typeof(long),
        double => typeof(double),
        bool => typeof(bool),
        _ => value.GetType()
    };

    private List<object> MaterializeScalarProjection(ShapedResult shaped, Type targetType)
    {
        var results = new List<object>(shaped.TypedItems.Count);

        foreach (var item in shaped.TypedItems)
        {
            // For scalar projections, extract the single value
            var firstValue = item.Values.Values.FirstOrDefault();
            if (firstValue != null)
            {
                var converted = _converter.FromFirestore(firstValue.Value, targetType);
                if (converted != null)
                {
                    results.Add(converted);
                }
            }
        }

        return results;
    }

    private List<object> MaterializeDirectCollection(ShapedResult shaped, Type targetType)
    {
        var results = new List<object>(shaped.TypedItems.Count);

        foreach (var item in shaped.TypedItems)
        {
            var firstValue = item.Values.Values.FirstOrDefault();
            if (firstValue?.Value is IList list)
            {
                var materialized = MaterializeCollectionValue(list, targetType);
                results.Add(materialized);
            }
        }

        return results;
    }

    private object MaterializeCollectionValue(IList items, Type collectionType)
    {
        var elementType = GetCollectionElementType(collectionType) ?? typeof(object);
        var collection = CreateCollection(collectionType, elementType);

        foreach (var item in items)
        {
            var materialized = _converter.FromFirestore(item, elementType);
            AddToCollection(collection, materialized);
        }

        return FinalizeCollection(collection, collectionType);
    }

    #region Strategy Discovery

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

        var paramNamesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var dictKey = ToPascalCase(param.Name!);
            constructorParams.Add(new ConstructorParamMapping(dictKey, i, param.ParameterType));
            paramNamesUsed.Add(dictKey);
        }

        var memberSetters = new List<MemberMapping>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (paramNamesUsed.Contains(prop.Name))
                continue;

            if (prop.GetIndexParameters().Length > 0)
                continue;

            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                memberSetters.Add(new MemberMapping(prop.Name, prop, prop.PropertyType));
                continue;
            }

            var backingFieldName = GetBackingFieldName(prop.Name);
            var backingField = type.GetField(backingFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

        var validConstructors = constructors
            .Where(c => !IsCopyConstructor(c, type))
            .ToList();

        if (validConstructors.Count == 0)
            throw new InvalidOperationException($"Type '{type.FullName}' has no valid constructors.");

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

    #endregion

    #region Collection Helpers

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

    private static Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length == 1)
                return genericArgs[0];
        }

        if (collectionType.IsArray)
            return collectionType.GetElementType();

        var enumerableInterface = collectionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0];
    }

    private static object CreateCollection(Type collectionType, Type elementType)
    {
        if (collectionType.IsArray)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(listType)!;
        }

        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            return Activator.CreateInstance(collectionType)!;
        }

        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(ISet<>))
        {
            var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
            return Activator.CreateInstance(hashSetType)!;
        }

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
                var addMethod = collection.GetType().GetMethod("Add")!;
                addMethod.Invoke(collection, [item]);
                break;
        }
    }

    private static object FinalizeCollection(object collection, Type targetType)
    {
        if (targetType.IsArray)
        {
            var toArrayMethod = collection.GetType().GetMethod("ToArray")!;
            return toArrayMethod.Invoke(collection, null)!;
        }
        return collection;
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return null;
    }

    #endregion

    #region Dictionary Helpers

    private static bool IsDictionaryType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(Dictionary<,>) ||
               genericDef == typeof(IDictionary<,>) ||
               genericDef == typeof(IReadOnlyDictionary<,>);
    }

    private static (Type? keyType, Type? valueType) GetDictionaryKeyValueTypes(Type dictionaryType)
    {
        if (!dictionaryType.IsGenericType)
            return (null, null);

        var genericArgs = dictionaryType.GetGenericArguments();
        if (genericArgs.Length != 2)
            return (null, null);

        return (genericArgs[0], genericArgs[1]);
    }

    private static object CreateDictionary(Type targetType, Type keyType, Type valueType)
    {
        // Always create a concrete Dictionary<,> - we'll convert if needed for read-only interfaces
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        return Activator.CreateInstance(dictType)!;
    }

    private static void AddToDictionary(object dictionary, object key, object? value)
    {
        // Dictionary<,> implements IDictionary, use it directly
        if (dictionary is IDictionary dict)
        {
            dict[key] = value;
            return;
        }

        // Fallback for non-IDictionary implementations
        var addMethod = dictionary.GetType().GetMethod("Add")!;
        addMethod.Invoke(dictionary, [key, value]);
    }

    private static object FinalizeDictionary(object dictionary, Type targetType)
    {
        // Dictionary<,> is compatible with both IDictionary<,> and IReadOnlyDictionary<,>
        // since Dictionary<,> implements both interfaces
        return dictionary;
    }

    #endregion
}