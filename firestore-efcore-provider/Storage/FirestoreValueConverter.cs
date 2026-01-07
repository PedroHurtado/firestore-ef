using Google.Cloud.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Storage;

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

        // double → decimal
        if (value is double d && actualTargetType == typeof(decimal))
            return (decimal)d;

        // string → enum
        if (value is string s && actualTargetType.IsEnum)
            return Enum.Parse(actualTargetType, s, ignoreCase: true);

        // long → int (Firestore may return long for integers)
        if (value is long l && actualTargetType == typeof(int))
            return (int)l;

        // Timestamp → DateTime (Firestore SDK specific type)
        if (value is Timestamp timestamp && actualTargetType == typeof(DateTime))
            return timestamp.ToDateTime();

        // long → TimeSpan (stored as ticks)
        if (value is long ticks && actualTargetType == typeof(TimeSpan))
            return TimeSpan.FromTicks(ticks);

        // Collections: convert elements recursively
        if (value is IEnumerable enumerable && value is not string && value is not byte[])
        {
            if (IsGenericList(targetType))
            {
                return ConvertCollectionFromFirestore(enumerable, targetType);
            }
        }

        return value;
    }

    private static object[] ConvertCollectionToFirestore(IEnumerable enumerable)
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
            else
            {
                result.Add(item);
            }
        }

        return result.ToArray();
    }

    private static object ConvertCollectionFromFirestore(IEnumerable enumerable, Type targetType)
    {
        var elementType = targetType.GetGenericArguments()[0];
        var underlyingElementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

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

            object? convertedItem;

            // double → decimal
            if (item is double d && underlyingElementType == typeof(decimal))
            {
                convertedItem = (decimal)d;
            }
            // string → enum
            else if (item is string s && underlyingElementType.IsEnum)
            {
                convertedItem = Enum.Parse(underlyingElementType, s, ignoreCase: true);
            }
            // long → int
            else if (item is long l && underlyingElementType == typeof(int))
            {
                convertedItem = (int)l;
            }
            // Timestamp → DateTime
            else if (item is Timestamp ts && underlyingElementType == typeof(DateTime))
            {
                convertedItem = ts.ToDateTime();
            }
            else
            {
                convertedItem = item;
            }

            list.Add(convertedItem);
        }

        return list;
    }

    private static bool IsGenericList(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
    }
}
