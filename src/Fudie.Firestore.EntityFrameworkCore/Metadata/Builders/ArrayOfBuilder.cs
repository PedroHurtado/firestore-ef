// Archivo: Metadata/Builders/ArrayOfBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para configurar arrays embebidos en documentos de Firestore.
/// Soporta fluent API para configurar el tipo de array.
/// </summary>
/// <typeparam name="TEntity">Tipo de la entidad que contiene el array</typeparam>
/// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
public class ArrayOfBuilder<TEntity, TElement>
    where TEntity : class
    where TElement : class
{
    private readonly EntityTypeBuilder<TEntity> _entityTypeBuilder;
    private readonly IMutableEntityType _entityType;
    private readonly string _propertyName;
    private readonly Type _elementType;

    internal ArrayOfBuilder(
        EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression)
    {
        _entityTypeBuilder = entityTypeBuilder;
        _entityType = entityTypeBuilder.Metadata;
        _elementType = typeof(TElement);

        var memberInfo = propertyExpression.GetMemberAccess();
        _propertyName = memberInfo.Name;

        // ✅ Ignorar la propiedad para que EF Core no intente registrarla como navegación
        // ArrayOf se maneja directamente por el serializador de Firestore
        _entityTypeBuilder.Ignore(_propertyName);

        // Crear shadow property para change tracking
        // Esta propiedad almacena el JSON serializado del array para detectar cambios
        var shadowPropertyName = GetShadowPropertyName(_propertyName);
        _entityTypeBuilder.Property<string?>(shadowPropertyName);

        // Marcar la shadow property como ArrayOf tracker
        var shadowProperty = _entityType.FindProperty(shadowPropertyName);
        shadowProperty?.SetAnnotation(ArrayOfAnnotations.JsonTrackerFor, _propertyName);

        // Registrar anotación base como Embedded por defecto
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.Embedded);
        _entityType.SetArrayOfElementClrType(_propertyName, _elementType);

        // Detectar y guardar el backing field para propiedades read-only
        var backingField = DetectBackingField(typeof(TEntity), _propertyName);
        _entityType.SetArrayOfBackingField(_propertyName, backingField);
    }

    /// <summary>
    /// Detecta el backing field para una propiedad siguiendo las convenciones de EF Core.
    /// </summary>
    private static FieldInfo? DetectBackingField(Type entityType, string propertyName)
    {
        // Convenciones de EF Core para backing fields:
        // 1. _propertyName (camelCase with underscore prefix)
        // 2. _PropertyName (PascalCase with underscore prefix)
        // 3. m_propertyName (Hungarian notation)
        // 4. <PropertyName>k__BackingField (compiler-generated)
        var candidateNames = new[]
        {
            $"_{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}", // _priceOptions
            $"_{propertyName}",                                               // _PriceOptions
            $"m_{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}", // m_priceOptions
            $"<{propertyName}>k__BackingField"                                // compiler-generated
        };

        foreach (var fieldName in candidateNames)
        {
            var field = entityType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field;
        }

        return null;
    }

    /// <summary>
    /// Genera el nombre de la shadow property para tracking de cambios.
    /// </summary>
    internal static string GetShadowPropertyName(string propertyName) => $"__{propertyName}_Json";

    /// <summary>
    /// Configura el array como una colección de GeoPoints nativos de Firestore.
    /// El tipo TElement debe tener propiedades Latitude/Longitude.
    /// </summary>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfBuilder<TEntity, TElement> AsGeoPoints()
    {
        // TODO: Fase 3 - Implementar validación de propiedades Lat/Lng
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.GeoPoint);
        return this;
    }

    /// <summary>
    /// Configura el array como una colección de DocumentReferences.
    /// TElement debe ser una entidad registrada en el modelo.
    /// </summary>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfBuilder<TEntity, TElement> AsReferences()
    {
        // TODO: Fase 4 - Implementar validación de entidad registrada
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.Reference);
        return this;
    }

    /// <summary>
    /// Obtiene el nombre de la propiedad configurada
    /// </summary>
    internal string PropertyName => _propertyName;

    /// <summary>
    /// Obtiene el tipo de elemento del array
    /// </summary>
    internal Type ElementType => _elementType;

    /// <summary>
    /// Obtiene el EntityTypeBuilder subyacente
    /// </summary>
    internal EntityTypeBuilder<TEntity> EntityTypeBuilder => _entityTypeBuilder;
}
