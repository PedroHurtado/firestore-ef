using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Métodos helper reutilizables para las conventions de Firestore.
/// Centraliza la lógica de detección de patrones comunes.
/// </summary>
public static class ConventionHelpers
{
    #region Detección de Primary Key

    /// <summary>
    /// Verifica si un tipo tiene estructura de entidad (tiene propiedad Id o {TypeName}Id).
    /// </summary>
    public static bool HasPrimaryKeyStructure(Type type)
    {
        return GetPrimaryKeyProperty(type) != null;
    }

    /// <summary>
    /// Obtiene la propiedad que actúa como Primary Key (Id o {TypeName}Id).
    /// </summary>
    public static PropertyInfo? GetPrimaryKeyProperty(Type type)
    {
        var typeName = type.Name;

        return type.GetProperties()
            .FirstOrDefault(p =>
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals($"{typeName}Id", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene el nombre de la propiedad que actúa como Primary Key.
    /// Retorna "Id" o "{TypeName}Id" según la convención.
    /// </summary>
    public static string? GetPrimaryKeyPropertyName(Type type)
    {
        return GetPrimaryKeyProperty(type)?.Name;
    }

    /// <summary>
    /// Obtiene el nombre convencional para una Foreign Key hacia el tipo especificado.
    /// Sigue la convención de EF Core: "{PrincipalTypeName}Id".
    /// </summary>
    public static string GetForeignKeyPropertyName(Type principalType)
    {
        return $"{principalType.Name}Id";
    }

    /// <summary>
    /// Obtiene el nombre convencional para una Foreign Key hacia el tipo especificado.
    /// Sigue la convención de EF Core: "{PrincipalTypeName}Id".
    /// </summary>
    public static string GetForeignKeyPropertyName<TPrincipal>() where TPrincipal : class
    {
        return GetForeignKeyPropertyName(typeof(TPrincipal));
    }

    #endregion

    #region Detección de GeoPoint

    /// <summary>
    /// Verifica si un tipo tiene estructura de GeoPoint (Latitude + Longitude).
    /// NO verifica si es entidad - usar IsGeoPointType para eso.
    /// </summary>
    public static bool HasGeoPointStructure(Type type)
    {
        return GetLatitudeProperty(type) != null && GetLongitudeProperty(type) != null;
    }

    /// <summary>
    /// Verifica si un tipo es un GeoPoint puro (tiene Lat/Lng pero NO es entidad).
    /// </summary>
    public static bool IsGeoPointType(Type type)
    {
        return HasGeoPointStructure(type) && !HasPrimaryKeyStructure(type);
    }

    /// <summary>
    /// Obtiene la propiedad Latitude (soporta: Latitude, Lat, Latitud).
    /// </summary>
    public static PropertyInfo? GetLatitudeProperty(Type type)
    {
        return type.GetProperties()
            .FirstOrDefault(p =>
                (p.Name.Equals("Latitude", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lat", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Latitud", StringComparison.OrdinalIgnoreCase)) &&
                (p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)));
    }

    /// <summary>
    /// Obtiene la propiedad Longitude (soporta: Longitude, Lng, Lon, Longitud).
    /// </summary>
    public static PropertyInfo? GetLongitudeProperty(Type type)
    {
        return type.GetProperties()
            .FirstOrDefault(p =>
                (p.Name.Equals("Longitude", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lng", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lon", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Longitud", StringComparison.OrdinalIgnoreCase)) &&
                (p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)));
    }

    #endregion

    #region Detección de Colecciones

    /// <summary>
    /// Verifica si un tipo es una colección genérica de elementos.
    /// Soporta: List&lt;T&gt;, HashSet&lt;T&gt;, ICollection&lt;T&gt;, IList&lt;T&gt;, IEnumerable&lt;T&gt;, etc.
    /// NO soporta Dictionary&lt;K,V&gt; ni otros tipos con múltiples argumentos genéricos.
    /// </summary>
    public static bool IsGenericCollection(Type type)
    {
        if (!type.IsGenericType)
            return false;

        // Excluir Dictionary y otros tipos con múltiples argumentos genéricos
        if (type.GetGenericArguments().Length != 1)
            return false;

        var genericDef = type.GetGenericTypeDefinition();

        // Verificar si ES una de las interfaces de colección
        if (genericDef == typeof(IEnumerable<>) ||
            genericDef == typeof(ICollection<>) ||
            genericDef == typeof(IList<>))
            return true;

        // Verificar si IMPLEMENTA IEnumerable<T>
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// Obtiene el tipo de elemento de una colección genérica.
    /// </summary>
    public static Type? GetCollectionElementType(Type type)
    {
        if (type.IsGenericType)
            return type.GetGenericArguments().FirstOrDefault();

        return null;
    }

    #endregion

    #region Detección de Tipos Primitivos

    /// <summary>
    /// Verifica si un tipo es primitivo, string, o un tipo de valor común.
    /// </summary>
    public static bool IsPrimitiveOrSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum ||
               (Nullable.GetUnderlyingType(type) is { } underlying && IsPrimitiveOrSimpleType(underlying));
    }

    #endregion

    #region Detección de Backing Fields

    /// <summary>
    /// Busca el backing field para una propiedad usando las convenciones de EF Core.
    /// Patrones soportados (en orden de precedencia):
    /// 1. _camelCasedPropertyName (ej: _priceOptions para PriceOptions)
    /// 2. _PropertyName (ej: _PriceOptions para PriceOptions)
    /// 3. m_camelCasedPropertyName (ej: m_priceOptions para PriceOptions)
    /// 4. m_PropertyName (ej: m_PriceOptions para PriceOptions)
    /// </summary>
    public static FieldInfo? FindBackingField(Type clrType, string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return null;

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var camelCase = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

        // Patrones EF Core en orden de precedencia
        string[] patterns =
        [
            $"_{camelCase}",      // _priceOptions
            $"_{propertyName}",   // _PriceOptions
            $"m_{camelCase}",     // m_priceOptions
            $"m_{propertyName}"   // m_PriceOptions
        ];

        return patterns
            .Select(name => clrType.GetField(name, flags))
            .FirstOrDefault(f => f != null);
    }

    /// <summary>
    /// Busca backing fields de colecciones que no fueron detectados por EF Core.
    /// Retorna tuplas (propertyName, fieldInfo, elementType) para cada backing field encontrado.
    /// </summary>
    public static IEnumerable<(string PropertyName, FieldInfo FieldInfo, Type ElementType)> FindCollectionBackingFields(Type clrType)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        foreach (var field in clrType.GetFields(flags))
        {
            var fieldType = field.FieldType;

            // Solo procesar colecciones genéricas
            if (!IsGenericCollection(fieldType))
                continue;

            var elementType = GetCollectionElementType(fieldType);
            if (elementType == null)
                continue;

            // Inferir nombre de propiedad desde el nombre del field
            var propertyName = InferPropertyNameFromField(field.Name);
            if (propertyName == null)
                continue;

            // Verificar que existe una propiedad pública con ese nombre
            var property = clrType.GetProperty(propertyName);
            if (property == null)
                continue;

            yield return (propertyName, field, elementType);
        }
    }

    /// <summary>
    /// Infiere el nombre de la propiedad desde el nombre del backing field.
    /// Soporta patrones: _camelCase, _PascalCase, m_camelCase, m_PascalCase
    /// </summary>
    private static string? InferPropertyNameFromField(string fieldName)
    {
        string baseName;

        if (fieldName.StartsWith("m_"))
            baseName = fieldName[2..];
        else if (fieldName.StartsWith("_"))
            baseName = fieldName[1..];
        else
            return null;

        if (string.IsNullOrEmpty(baseName))
            return null;

        // Convertir a PascalCase si es camelCase
        return char.ToUpperInvariant(baseName[0]) + baseName[1..];
    }

    #endregion
}
