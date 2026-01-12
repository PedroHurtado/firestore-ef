using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts values from Firestore types to CLR types.
/// Used for aggregation results and field value conversions.
/// </summary>
public interface ITypeConverter
{
    /// <summary>
    /// Converts a value to the target type.
    /// </summary>
    /// <param name="value">The value to convert (may be null).</param>
    /// <param name="targetType">The target CLR type.</param>
    /// <returns>The converted value.</returns>
    object? Convert(object? value, Type targetType);
}
