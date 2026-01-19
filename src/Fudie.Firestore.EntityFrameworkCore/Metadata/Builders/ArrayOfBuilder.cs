// Archivo: Metadata/Builders/ArrayOfBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para cambiar el tipo de array detectado por convención.
/// La convención detecta automáticamente el tipo, este builder permite cambiarlo.
/// </summary>
/// <typeparam name="TEntity">Tipo de la entidad que contiene el array</typeparam>
/// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
public class ArrayOfBuilder<TEntity, TElement>
    where TEntity : class
    where TElement : class
{
    private readonly IMutableEntityType _entityType;
    private readonly string _propertyName;

    internal ArrayOfBuilder(
        EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression)
    {
        _entityType = entityTypeBuilder.Metadata;
        var memberInfo = propertyExpression.GetMemberAccess();
        _propertyName = memberInfo.Name;
    }

    /// <summary>
    /// Configura el array como una colección de valores primitivos.
    /// Útil cuando la convención detectó otro tipo pero se quiere tratar como primitivo.
    /// </summary>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfBuilder<TEntity, TElement> AsPrimitive()
    {
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.Primitive);
        return this;
    }

    /// <summary>
    /// Configura el array como una colección de GeoPoints nativos de Firestore.
    /// El tipo TElement debe tener propiedades Latitude/Longitude.
    /// </summary>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfBuilder<TEntity, TElement> AsGeoPoints()
    {
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
        _entityType.SetArrayOfType(_propertyName, ArrayOfAnnotations.ArrayType.Reference);
        return this;
    }

    /// <summary>
    /// Obtiene el nombre de la propiedad configurada
    /// </summary>
    internal string PropertyName => _propertyName;
}
