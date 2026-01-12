// Archivo: Metadata/Conventions/ArrayOfAnnotations.cs
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Constantes y métodos helper para anotaciones de ArrayOf en Firestore.
/// </summary>
public static class ArrayOfAnnotations
{
    private const string Prefix = "Firestore:ArrayOf:";

    /// <summary>
    /// Tipo de array: "Embedded" | "GeoPoint" | "Reference"
    /// </summary>
    public const string Type = Prefix + "Type";

    /// <summary>
    /// Tipo CLR del elemento del array
    /// </summary>
    public const string ElementClrType = Prefix + "ElementClrType";

    /// <summary>
    /// Nombre de la propiedad que contiene el array
    /// </summary>
    public const string PropertyName = Prefix + "PropertyName";

    /// <summary>
    /// Configuración anidada para elementos del array (referencias, arrays anidados)
    /// </summary>
    public const string NestedConfig = Prefix + "NestedConfig";

    /// <summary>
    /// Valores posibles para el tipo de array
    /// </summary>
    public static class ArrayType
    {
        public const string Embedded = "Embedded";
        public const string GeoPoint = "GeoPoint";
        public const string Reference = "Reference";
    }

    #region Extension Methods para IReadOnlyEntityType

    /// <summary>
    /// Obtiene el tipo de array configurado para una propiedad
    /// </summary>
    public static string? GetArrayOfType(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{Type}:{propertyName}")?.Value as string;
    }

    /// <summary>
    /// Establece el tipo de array para una propiedad
    /// </summary>
    public static void SetArrayOfType(this IMutableEntityType entityType, string propertyName, string arrayType)
    {
        entityType.SetAnnotation($"{Type}:{propertyName}", arrayType);
    }

    /// <summary>
    /// Obtiene el tipo CLR del elemento del array
    /// </summary>
    public static System.Type? GetArrayOfElementClrType(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{ElementClrType}:{propertyName}")?.Value as System.Type;
    }

    /// <summary>
    /// Establece el tipo CLR del elemento del array
    /// </summary>
    public static void SetArrayOfElementClrType(this IMutableEntityType entityType, string propertyName, System.Type elementType)
    {
        entityType.SetAnnotation($"{ElementClrType}:{propertyName}", elementType);
    }

    /// <summary>
    /// Verifica si una propiedad está configurada como ArrayOf
    /// </summary>
    public static bool IsArrayOf(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetArrayOfType(propertyName) != null;
    }

    /// <summary>
    /// Verifica si una propiedad está configurada como ArrayOf de tipo Embedded
    /// </summary>
    public static bool IsArrayOfEmbedded(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetArrayOfType(propertyName) == ArrayType.Embedded;
    }

    /// <summary>
    /// Verifica si una propiedad está configurada como ArrayOf de tipo GeoPoint
    /// </summary>
    public static bool IsArrayOfGeoPoint(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetArrayOfType(propertyName) == ArrayType.GeoPoint;
    }

    /// <summary>
    /// Verifica si una propiedad está configurada como ArrayOf de tipo Reference
    /// </summary>
    public static bool IsArrayOfReference(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetArrayOfType(propertyName) == ArrayType.Reference;
    }

    #endregion
}