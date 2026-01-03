using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts values from Firestore types to CLR types.
/// </summary>
public class FirestoreTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType)
    {
        if (value == null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Common conversions
        if (underlyingType == typeof(int) && value is long longValue)
        {
            return (int)longValue;
        }

        if (underlyingType == typeof(decimal) && value is double doubleValue)
        {
            return (decimal)doubleValue;
        }

        if (underlyingType == typeof(DateTime) && value is Google.Cloud.Firestore.Timestamp timestamp)
        {
            return timestamp.ToDateTime();
        }

        if (underlyingType == typeof(DateTimeOffset) && value is Google.Cloud.Firestore.Timestamp ts)
        {
            return ts.ToDateTimeOffset();
        }

        // Fallback to System.Convert
        return System.Convert.ChangeType(value, underlyingType);
    }
}
