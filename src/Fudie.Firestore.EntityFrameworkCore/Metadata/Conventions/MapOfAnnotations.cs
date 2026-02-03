// Archivo: Metadata/Conventions/MapOfAnnotations.cs
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Constantes y métodos helper para anotaciones de MapOf en Firestore.
/// </summary>
public static class MapOfAnnotations
{
    private const string Prefix = "Firestore:MapOf:";

    /// <summary>
    /// Tipo CLR de la clave del diccionario
    /// </summary>
    public const string KeyClrType = Prefix + "KeyClrType";

    /// <summary>
    /// Tipo CLR del elemento del diccionario
    /// </summary>
    public const string ElementClrType = Prefix + "ElementClrType";

    /// <summary>
    /// Nombre de la propiedad que contiene el diccionario
    /// </summary>
    public const string PropertyName = Prefix + "PropertyName";

    /// <summary>
    /// Configuración anidada para elementos del diccionario (referencias, arrays, maps anidados)
    /// </summary>
    public const string NestedConfig = Prefix + "NestedConfig";

    /// <summary>
    /// Indica que una shadow property es un tracker JSON para una propiedad MapOf.
    /// El valor es el nombre de la propiedad MapOf original.
    /// </summary>
    public const string JsonTrackerFor = "Firestore:MapOfTrackerFor";

    /// <summary>
    /// FieldInfo del backing field para la propiedad MapOf.
    /// Se usa para escribir directamente al campo cuando la propiedad es read-only.
    /// </summary>
    public const string BackingField = Prefix + "BackingField";

    /// <summary>
    /// Lista de propiedades ignoradas en elementos de MapOf.
    /// Estas propiedades no se serializan a Firestore.
    /// </summary>
    public const string IgnoredProperties = Prefix + "IgnoredProperties";

    /// <summary>
    /// Genera el nombre de la shadow property para tracking de cambios.
    /// Formato: __{PropertyName}_Json
    /// </summary>
    public static string GetShadowPropertyName(string propertyName) => $"__{propertyName}_Json";

    #region Extension Methods para IReadOnlyEntityType

    /// <summary>
    /// Obtiene el tipo CLR de la clave del diccionario
    /// </summary>
    public static System.Type? GetMapOfKeyClrType(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{KeyClrType}:{propertyName}")?.Value as System.Type;
    }

    /// <summary>
    /// Establece el tipo CLR de la clave del diccionario
    /// </summary>
    public static void SetMapOfKeyClrType(this IMutableEntityType entityType, string propertyName, System.Type keyType)
    {
        entityType.SetAnnotation($"{KeyClrType}:{propertyName}", keyType);
    }

    /// <summary>
    /// Obtiene el tipo CLR del elemento del diccionario
    /// </summary>
    public static System.Type? GetMapOfElementClrType(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{ElementClrType}:{propertyName}")?.Value as System.Type;
    }

    /// <summary>
    /// Establece el tipo CLR del elemento del diccionario
    /// </summary>
    public static void SetMapOfElementClrType(this IMutableEntityType entityType, string propertyName, System.Type elementType)
    {
        entityType.SetAnnotation($"{ElementClrType}:{propertyName}", elementType);
    }

    /// <summary>
    /// Verifica si una propiedad está configurada como MapOf
    /// </summary>
    public static bool IsMapOf(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetMapOfKeyClrType(propertyName) != null;
    }

    /// <summary>
    /// Obtiene el backing field para una propiedad MapOf
    /// </summary>
    public static System.Reflection.FieldInfo? GetMapOfBackingField(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{BackingField}:{propertyName}")?.Value as System.Reflection.FieldInfo;
    }

    /// <summary>
    /// Establece el backing field para una propiedad MapOf
    /// </summary>
    public static void SetMapOfBackingField(this IMutableEntityType entityType, string propertyName, System.Reflection.FieldInfo? fieldInfo)
    {
        if (fieldInfo != null)
        {
            entityType.SetAnnotation($"{BackingField}:{propertyName}", fieldInfo);
        }
    }

    /// <summary>
    /// Obtiene las propiedades ignoradas para elementos de una propiedad MapOf
    /// </summary>
    public static HashSet<string>? GetMapOfIgnoredProperties(this IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.FindAnnotation($"{IgnoredProperties}:{propertyName}")?.Value as HashSet<string>;
    }

    /// <summary>
    /// Agrega una propiedad ignorada para elementos de una propiedad MapOf
    /// </summary>
    public static void AddMapOfIgnoredProperty(this IMutableEntityType entityType, string mapPropertyName, string ignoredPropertyName)
    {
        var existing = entityType.FindAnnotation($"{IgnoredProperties}:{mapPropertyName}")?.Value as HashSet<string>;
        var set = existing ?? new HashSet<string>();
        set.Add(ignoredPropertyName);
        entityType.SetAnnotation($"{IgnoredProperties}:{mapPropertyName}", set);
    }

    #endregion
}