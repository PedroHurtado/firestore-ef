// Archivo: Metadata/Builders/MapOfBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para configurar un diccionario como un Map nativo de Firestore.
/// </summary>
/// <typeparam name="TEntity">Tipo de la entidad que contiene el diccionario</typeparam>
/// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
/// <typeparam name="TElement">Tipo de los valores del diccionario</typeparam>
public class MapOfBuilder<TEntity, TKey, TElement>
    where TEntity : class
    where TElement : class
{
    private readonly IMutableEntityType _entityType;
    private readonly string _propertyName;

    internal MapOfBuilder(
        EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, IReadOnlyDictionary<TKey, TElement>>> propertyExpression)
    {
        _entityType = entityTypeBuilder.Metadata;
        var memberInfo = propertyExpression.GetMemberAccess();
        _propertyName = memberInfo.Name;

        // Registrar tipos en anotaciones
        _entityType.SetMapOfKeyClrType(_propertyName, typeof(TKey));
        _entityType.SetMapOfElementClrType(_propertyName, typeof(TElement));
    }

    /// <summary>
    /// Obtiene el nombre de la propiedad configurada
    /// </summary>
    internal string PropertyName => _propertyName;
}
