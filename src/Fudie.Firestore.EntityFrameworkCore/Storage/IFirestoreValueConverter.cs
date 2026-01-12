using System;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Centralizes bidirectional value conversion between CLR types and Firestore types.
/// Eliminates duplicated conversion logic in QueryBuilder and Deserializer.
///
/// Conversions:
/// - decimal ↔ double (Firestore doesn't support decimal)
/// - enum ↔ string (Firestore stores enums as strings)
/// - DateTime → UTC (Firestore stores timestamps in UTC)
/// - List&lt;decimal&gt; ↔ double[]
/// - List&lt;enum&gt; ↔ string[]
/// </summary>
public interface IFirestoreValueConverter
{
    /// <summary>
    /// Converts a CLR value to a Firestore-compatible type.
    /// Used when building queries and serializing documents.
    /// </summary>
    /// <param name="value">The CLR value to convert.</param>
    /// <param name="enumType">Optional enum type for int-to-enum-string conversion.
    /// When EF Core parameterizes an enum value, it may pass an int instead of the enum.
    /// Provide the enum type to convert the int to the enum name string.</param>
    /// <returns>The Firestore-compatible value.</returns>
    object? ToFirestore(object? value, Type? enumType = null);

    /// <summary>
    /// Converts a Firestore value back to the target CLR type.
    /// Used when deserializing documents.
    /// </summary>
    /// <param name="value">The Firestore value to convert.</param>
    /// <param name="targetType">The target CLR type.</param>
    /// <returns>The converted CLR value.</returns>
    object? FromFirestore(object? value, Type targetType);
}
