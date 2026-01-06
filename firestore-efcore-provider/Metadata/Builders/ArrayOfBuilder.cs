// Archivo: Metadata/Builders/ArrayOfBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Builders;

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

        // Registrar anotación base como Embedded por defecto
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.Embedded);
        _entityType.SetArrayOfElementClrType(_propertyName, _elementType);
    }

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
