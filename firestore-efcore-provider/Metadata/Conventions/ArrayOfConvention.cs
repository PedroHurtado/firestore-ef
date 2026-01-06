using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convention que auto-detecta propiedades List&lt;T&gt; en entidades y aplica ArrayOf automáticamente.
///
/// Reglas de detección:
/// - List&lt;T&gt; donde T es GeoPoint (Lat/Lng sin Id) → ArrayOf GeoPoint
/// - List&lt;T&gt; donde T es ComplexType (no Entity, no GeoPoint) → ArrayOf Embedded
/// - List&lt;T&gt; donde T es Entity → NO aplica (requiere configuración explícita con AsReferences())
///
/// Esta convention también implementa IModelFinalizingConvention para limpiar entidades
/// que fueron descubiertas incorrectamente por EF Core antes de que la convention pudiera actuar.
/// </summary>
public class ArrayOfConvention : IEntityTypeAddedConvention, IModelFinalizingConvention
{
    // Almacena los tipos de elementos que se han marcado como ArrayOf para limpiarlos después
    private static readonly HashSet<Type> _arrayOfElementTypes = [];

    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;
        var clrType = entityType.ClrType;
        var model = entityType.Model;

        foreach (var propertyInfo in clrType.GetProperties())
        {
            var propertyType = propertyInfo.PropertyType;

            // Solo procesar colecciones genéricas
            if (!ConventionHelpers.IsGenericCollection(propertyType))
                continue;

            var elementType = ConventionHelpers.GetCollectionElementType(propertyType);
            if (elementType == null)
                continue;

            // Ignorar tipos primitivos y strings
            if (ConventionHelpers.IsPrimitiveOrSimpleType(elementType))
                continue;

            // Verificar si ya está configurado explícitamente
            if (entityType.IsArrayOf(propertyInfo.Name))
                continue;

            // Si el elementType tiene PK structure, es probable que sea una entidad real
            // NO aplicar convention automática - requiere config explícita
            if (ConventionHelpers.HasPrimaryKeyStructure(elementType))
                continue;

            // Caso 1: Es GeoPoint puro (Lat/Lng sin Id) → ArrayOf GeoPoint
            if (ConventionHelpers.IsGeoPointType(elementType))
            {
                ApplyArrayOfGeoPoint(entityType, propertyInfo.Name, elementType);
                IgnoreProperty(entityTypeBuilder, propertyInfo.Name);
                _arrayOfElementTypes.Add(elementType);
                continue;
            }

            // Caso 2: Es ComplexType → ArrayOf Embedded
            if (elementType.IsClass && !elementType.IsAbstract)
            {
                ApplyArrayOfEmbedded(entityType, propertyInfo.Name, elementType);
                IgnoreProperty(entityTypeBuilder, propertyInfo.Name);
                _arrayOfElementTypes.Add(elementType);
            }
        }
    }

    /// <summary>
    /// Se ejecuta al final del proceso de construcción del modelo.
    /// Elimina entidades que fueron descubiertas incorrectamente por EF Core
    /// cuando deberían ser tratadas como ArrayOf elements.
    /// </summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;

        // Encontrar entidades que son realmente ArrayOf elements y no deberían ser entidades
        var entitiesToRemove = model.GetEntityTypes()
            .Where(et => _arrayOfElementTypes.Contains(et.ClrType))
            .ToList();

        foreach (var entityType in entitiesToRemove)
        {
            // Solo remover si no tiene PK definida explícitamente
            if (entityType.FindPrimaryKey() == null)
            {
                modelBuilder.Ignore(entityType.ClrType);
            }
        }

        // Limpiar el set para la próxima ejecución
        _arrayOfElementTypes.Clear();
    }

    private static void ApplyArrayOfGeoPoint(IConventionEntityType entityType, string propertyName, Type elementType)
    {
        var mutableEntityType = (IMutableEntityType)entityType;
        mutableEntityType.SetArrayOfType(propertyName, ArrayOfAnnotations.ArrayType.GeoPoint);
        mutableEntityType.SetArrayOfElementClrType(propertyName, elementType);
    }

    private static void ApplyArrayOfEmbedded(IConventionEntityType entityType, string propertyName, Type elementType)
    {
        var mutableEntityType = (IMutableEntityType)entityType;
        mutableEntityType.SetArrayOfType(propertyName, ArrayOfAnnotations.ArrayType.Embedded);
        mutableEntityType.SetArrayOfElementClrType(propertyName, elementType);
    }

    private static void IgnoreProperty(IConventionEntityTypeBuilder entityTypeBuilder, string propertyName)
    {
        entityTypeBuilder.Ignore(propertyName);
    }
}
