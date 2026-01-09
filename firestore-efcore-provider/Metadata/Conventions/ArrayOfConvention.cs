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
/// - List&lt;T&gt; donde T es Entity registrada en el modelo → ArrayOf Reference (auto-detectado)
///
/// Esta convention también implementa IModelFinalizingConvention para:
/// 1. Detectar ArrayOf References cuando T es una entidad registrada
/// 2. Limpiar entidades descubiertas incorrectamente por EF Core
/// </summary>
public class ArrayOfConvention : IEntityTypeAddedConvention, IModelFinalizingConvention
{
    // Almacena los tipos de elementos que se han marcado como ArrayOf para limpiarlos después
    private static readonly HashSet<Type> _arrayOfElementTypes = [];

    // Almacena las propiedades que podrían ser ArrayOf Reference pendientes de validar
    private static readonly List<(Type EntityType, string PropertyName, Type ElementType)> _pendingReferenceArrays = [];

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
            // Ignorar la propiedad AHORA para evitar que EF Core cree FK inversa
            // y guardar para procesar en ModelFinalizing cuando sepamos si es entidad registrada
            if (ConventionHelpers.HasPrimaryKeyStructure(elementType))
            {
                IgnoreProperty(entityTypeBuilder, propertyInfo.Name);
                _pendingReferenceArrays.Add((clrType, propertyInfo.Name, elementType));
                continue;
            }

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
    /// 1. Limpia ArrayOf de propiedades que son SubCollections (configuradas después en OnModelCreating)
    /// 2. Auto-detecta ArrayOf References cuando List&lt;T&gt; y T es entidad registrada
    /// 3. Elimina entidades descubiertas incorrectamente por EF Core
    /// </summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;

        // Paso 1: Limpiar ArrayOf de propiedades que son SubCollections
        // Esto ocurre cuando SubCollection se configura en OnModelCreating DESPUÉS de que
        // ProcessEntityTypeAdded haya aplicado ArrayOf Embedded
        CleanupArrayOfForSubCollections(model);

        // Paso 2: Auto-detectar ArrayOf References
        AutoDetectArrayOfReferences(model);

        // Paso 3: Procesar ArrayOf References pendientes (propiedades con PK structure)
        foreach (var (entityClrType, propertyName, elementType) in _pendingReferenceArrays)
        {
            var entityType = model.FindEntityType(entityClrType);
            if (entityType == null)
                continue;

            // ✅ Omitir si ya es SubCollection
            var navigation = entityType.FindNavigation(propertyName);
            if (navigation != null && navigation.IsSubCollection())
                continue;

            // El elementType es una entidad registrada → aplicar ArrayOf Reference
            if (model.FindEntityType(elementType) != null)
            {
                ApplyArrayOfReference(entityType, propertyName, elementType);
            }
        }
        _pendingReferenceArrays.Clear();

        // Paso 4: Limpiar entidades que son ArrayOf elements
        var entitiesToRemove = model.GetEntityTypes()
            .Where(et => _arrayOfElementTypes.Contains(et.ClrType))
            .ToList();

        foreach (var entityType in entitiesToRemove)
        {
            if (entityType.FindPrimaryKey() == null)
            {
                modelBuilder.Ignore(entityType.ClrType);
            }
        }

        _arrayOfElementTypes.Clear();
    }

    /// <summary>
    /// Limpia las anotaciones ArrayOf de propiedades que fueron configuradas como SubCollection
    /// en OnModelCreating. Esto ocurre porque ProcessEntityTypeAdded se ejecuta antes de
    /// OnModelCreating y puede marcar una propiedad como ArrayOf Embedded cuando en realidad
    /// será una SubCollection.
    /// </summary>
    private static void CleanupArrayOfForSubCollections(IConventionModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                if (!navigation.IsCollection)
                    continue;

                if (!navigation.IsSubCollection())
                    continue;

                // Esta navegación es SubCollection - limpiar cualquier anotación ArrayOf
                var propertyName = navigation.Name;
                if (entityType.IsArrayOf(propertyName))
                {
                    var mutableEntityType = (IMutableEntityType)entityType;
                    mutableEntityType.RemoveAnnotation($"{ArrayOfAnnotations.Type}:{propertyName}");
                    mutableEntityType.RemoveAnnotation($"{ArrayOfAnnotations.ElementClrType}:{propertyName}");
                }
            }
        }
    }

    /// <summary>
    /// Auto-detecta List&lt;T&gt; donde T es una entidad registrada en el modelo
    /// y aplica ArrayOf Reference automáticamente.
    /// También elimina las navegaciones y FKs inversas que EF Core haya creado.
    /// </summary>
    private static void AutoDetectArrayOfReferences(IConventionModel model)
    {
        // Obtener todos los tipos de entidades registradas
        var registeredEntityTypes = model.GetEntityTypes()
            .Select(et => et.ClrType)
            .ToHashSet();

        foreach (var entityType in model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;

            foreach (var propertyInfo in clrType.GetProperties())
            {
                var propertyType = propertyInfo.PropertyType;

                // Solo procesar colecciones genéricas
                if (!ConventionHelpers.IsGenericCollection(propertyType))
                    continue;

                var elementType = ConventionHelpers.GetCollectionElementType(propertyType);
                if (elementType == null)
                    continue;

                // Verificar si ya está configurado
                if (entityType.IsArrayOf(propertyInfo.Name))
                    continue;

                // ✅ Omitir navegaciones que son SubCollections
                var navigation = entityType.FindNavigation(propertyInfo.Name);
                if (navigation != null && navigation.IsSubCollection())
                    continue;

                // Si el elementType es una entidad registrada → ArrayOf Reference
                if (registeredEntityTypes.Contains(elementType))
                {
                    ApplyArrayOfReference(entityType, propertyInfo.Name, elementType);
                }
            }
        }
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

    private static void ApplyArrayOfReference(IConventionEntityType entityType, string propertyName, Type elementType)
    {
        var mutableEntityType = (IMutableEntityType)entityType;
        mutableEntityType.SetArrayOfType(propertyName, ArrayOfAnnotations.ArrayType.Reference);
        mutableEntityType.SetArrayOfElementClrType(propertyName, elementType);
    }

    private static void IgnoreProperty(IConventionEntityTypeBuilder entityTypeBuilder, string propertyName)
    {
        entityTypeBuilder.Ignore(propertyName);
    }
}
