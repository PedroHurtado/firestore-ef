using Google.Cloud.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Centralizes bidirectional value conversion between CLR types and Firestore types.
/// </summary>
public class FirestoreValueConverter : IFirestoreValueConverter
{
    /// <inheritdoc />
    public object? ToFirestore(object? value, Type? enumType = null)
    {
        if (value == null)
            return null;

        var type = value.GetType();
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
            type = underlyingType;

        // enum → string (handle both enum values and int values with enumType hint)
        if (type.IsEnum)
            return value.ToString();

        // int → enum string (when EF Core passes int for parameterized enum)
        if (enumType != null && IsNumericType(type))
        {
            var enumValue = Enum.ToObject(enumType, value);
            return enumValue.ToString();
        }

        // decimal → double (Firestore doesn't support decimal)
        if (value is decimal d)
            return (double)d;

        // DateTime → UTC
        if (value is DateTime dt)
            return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        // TimeSpan → long (ticks) - Firestore doesn't support TimeSpan natively
        if (value is TimeSpan ts)
            return ts.Ticks;

        // Guid → string (Firestore stores document IDs as strings)
        if (value is Guid g)
            return g.ToString();

        // Collections: convert elements recursively
        if (value is IEnumerable enumerable && value is not string && value is not byte[])
        {
            return ConvertCollectionToFirestore(enumerable);
        }

        // Native types: int, long, double, string, bool - return unchanged
        return value;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
               type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
               type == typeof(ushort) || type == typeof(sbyte);
    }

    /// <inheritdoc />
    public object? FromFirestore(object? value, Type targetType)
    {
        if (value == null)
            return null;

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var actualTargetType = underlyingType ?? targetType;

        // When targetType is object, return value as-is (preserve original type)
        if (actualTargetType == typeof(object))
            return value;

        // DocumentReference → entity type: return null (shallow loading)
        // When a navigation property is stored as DocumentReference but wasn't projected,
        // we return null instead of trying to convert to the entity type.
        // This supports shallow loading where only explicitly projected data is loaded.
        if (value is DocumentReference && actualTargetType != typeof(DocumentReference))
            return null;

        // double → decimal
        if (value is double d && actualTargetType == typeof(decimal))
            return (decimal)d;

        // long → decimal (Firestore returns 0 as Int64, not Double)
        if (value is long ld && actualTargetType == typeof(decimal))
            return (decimal)ld;

        // string → enum
        if (value is string s && actualTargetType.IsEnum)
            return Enum.Parse(actualTargetType, s, ignoreCase: true);

        // string → Guid (document IDs are stored as strings in Firestore)
        if (value is string guidStr && actualTargetType == typeof(Guid))
            return Guid.Parse(guidStr);

        // string → numeric types (document IDs may be numeric but stored as strings)
        if (value is string numStr && IsNumericType(actualTargetType))
            return Convert.ChangeType(numStr, actualTargetType);

        // long → int (Firestore may return long for integers)
        if (value is long l && actualTargetType == typeof(int))
            return (int)l;

        // Timestamp → DateTime (Firestore SDK specific type)
        if (value is Timestamp timestamp && actualTargetType == typeof(DateTime))
            return timestamp.ToDateTime();

        // long → TimeSpan (stored as ticks)
        if (value is long ticks && actualTargetType == typeof(TimeSpan))
            return TimeSpan.FromTicks(ticks);

        // GeoPoint → custom GeoLocation type (positional record with Latitude, Longitude)
        if (value is GeoPoint geoPoint && IsGeoLocationType(actualTargetType))
        {
            return MaterializeGeoLocation(geoPoint, actualTargetType);
        }

        // Collections: convert elements recursively
        if (value is IEnumerable enumerable && value is not string && value is not byte[])
        {
            if (IsGenericList(targetType))
            {
                return ConvertCollectionFromFirestore(enumerable, targetType);
            }
        }

        // Dictionary → ComplexType (nested value objects like Coordenadas)
        if (value is IDictionary<string, object> dict && IsComplexType(actualTargetType))
        {
            return MaterializeComplexType(dict, actualTargetType);
        }

        return value;
    }

    private static List<object> ConvertCollectionToFirestore(IEnumerable enumerable)
    {
        var result = new List<object>();

        foreach (var item in enumerable)
        {
            if (item == null)
                continue;

            var itemType = item.GetType();

            // decimal → double
            if (item is decimal d)
            {
                result.Add((double)d);
            }
            // enum → string
            else if (itemType.IsEnum)
            {
                result.Add(item.ToString()!);
            }
            // DateTime → UTC
            else if (item is DateTime dt)
            {
                result.Add(dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime());
            }
            // Guid → string
            else if (item is Guid g)
            {
                result.Add(g.ToString());
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    private object ConvertCollectionFromFirestore(IEnumerable enumerable, Type targetType)
    {
        var elementType = targetType.GetGenericArguments()[0];

        // Create List<T>
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                list.Add(null);
                continue;
            }

            // Use FromFirestore recursively to handle all conversions:
            // - GeoPoint → GeoLocation
            // - Dictionary → ComplexType
            // - double → decimal
            // - string → enum
            // - long → int
            // - Timestamp → DateTime
            var convertedItem = FromFirestore(item, elementType);
            list.Add(convertedItem);
        }

        return list;
    }

    private static bool IsGenericList(Type type)
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

    /// <summary>
    /// Determines if a type is a GeoLocation-like type (has Latitude and Longitude properties/parameters).
    /// </summary>
    private static bool IsGeoLocationType(Type type)
    {
        // Check for constructor with Latitude/Longitude parameters (positional record)
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 2)
            {
                var hasLatitude = parameters.Any(p =>
                    p.Name?.Equals("Latitude", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lat", StringComparison.OrdinalIgnoreCase) == true);
                var hasLongitude = parameters.Any(p =>
                    p.Name?.Equals("Longitude", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lng", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lon", StringComparison.OrdinalIgnoreCase) == true);

                if (hasLatitude && hasLongitude)
                    return true;
            }
        }

        // Check for properties Latitude/Longitude
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasLatProp = props.Any(p => p.Name.Equals("Latitude", StringComparison.OrdinalIgnoreCase));
        var hasLngProp = props.Any(p => p.Name.Equals("Longitude", StringComparison.OrdinalIgnoreCase));

        return hasLatProp && hasLngProp;
    }

    /// <summary>
    /// Materializes a GeoLocation-like type from a Firestore GeoPoint.
    /// </summary>
    private static object MaterializeGeoLocation(GeoPoint geoPoint, Type targetType)
    {
        // Try constructor with Latitude, Longitude parameters (positional record)
        var constructors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 2)
            {
                var latParam = parameters.FirstOrDefault(p =>
                    p.Name?.Equals("Latitude", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lat", StringComparison.OrdinalIgnoreCase) == true);
                var lngParam = parameters.FirstOrDefault(p =>
                    p.Name?.Equals("Longitude", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lng", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Equals("Lon", StringComparison.OrdinalIgnoreCase) == true);

                if (latParam != null && lngParam != null)
                {
                    var args = new object?[2];
                    args[Array.IndexOf(parameters, latParam)] = geoPoint.Latitude;
                    args[Array.IndexOf(parameters, lngParam)] = geoPoint.Longitude;
                    return ctor.Invoke(args);
                }
            }
        }

        // Fallback: use property setters
        var instance = Activator.CreateInstance(targetType)!;
        var latProp = targetType.GetProperty("Latitude", BindingFlags.Public | BindingFlags.Instance);
        var lngProp = targetType.GetProperty("Longitude", BindingFlags.Public | BindingFlags.Instance);

        latProp?.SetValue(instance, geoPoint.Latitude);
        lngProp?.SetValue(instance, geoPoint.Longitude);

        return instance;
    }

    /// <summary>
    /// Determines if a type is a complex type (value object) that can be materialized from a dictionary.
    /// Complex types are non-primitive classes with a parameterless or single-argument constructor.
    /// </summary>
    private static bool IsComplexType(Type type)
    {
        // Exclude primitives, strings, and other simple types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(byte[]))
            return false;

        // Exclude interfaces and abstract types
        if (type.IsInterface || type.IsAbstract)
            return false;

        // Exclude collections
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            return false;

        // Must be a class or struct with properties
        return (type.IsClass || type.IsValueType) && type.GetProperties().Length > 0;
    }

    /// <summary>
    /// Materializes a ComplexType from a Firestore dictionary.
    /// Supports both constructor-based and property-setter initialization.
    /// </summary>
    private object MaterializeComplexType(IDictionary<string, object> data, Type targetType)
    {
        var constructor = GetBestConstructor(targetType);
        var parameters = constructor.GetParameters();

        if (parameters.Length == 0)
        {
            // Use parameterless constructor and property setters
            var instance = Activator.CreateInstance(targetType)!;

            foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;

                if (data.TryGetValue(prop.Name, out var propValue) && propValue != null)
                {
                    var convertedValue = FromFirestore(propValue, prop.PropertyType);
                    prop.SetValue(instance, convertedValue);
                }
            }

            return instance;
        }

        // Use constructor with parameters
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name!;

            // Try to find value in dictionary (case-insensitive)
            var dictKey = data.Keys.FirstOrDefault(k =>
                k.Equals(paramName, StringComparison.OrdinalIgnoreCase));

            if (dictKey != null && data[dictKey] != null)
            {
                args[i] = FromFirestore(data[dictKey], param.ParameterType);
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

        // Prefer parameterless constructor for value objects
        var parameterless = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterless != null)
            return parameterless;

        // Otherwise, prefer constructor with most parameters
        return constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
    }
}
