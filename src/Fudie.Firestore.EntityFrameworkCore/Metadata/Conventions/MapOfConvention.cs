using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convention que auto-detecta propiedades IReadOnlyDictionary&lt;TKey, TElement&gt; en entidades
/// y aplica MapOf automáticamente.
///
/// CRÍTICO: Sin esta convention, EF Core fallará al compilar el modelo porque intentará
/// procesar IReadOnlyDictionary como una navegación y no sabrá cómo manejarla.
///
/// Reglas de detección:
/// - IReadOnlyDictionary&lt;TKey, TElement&gt; donde TKey es primitivo/enum y TElement es clase → MapOf Embedded
/// - IDictionary&lt;TKey, TElement&gt; y Dictionary&lt;TKey, TElement&gt; también son soportados
///
/// Esta convention implementa:
/// - IEntityTypeAddedConvention: Para ignorar la propiedad INMEDIATAMENTE cuando se añade el tipo
/// - IModelFinalizingConvention: Para crear shadow properties y limpiar tipos incorrectamente descubiertos
/// </summary>
public class MapOfConvention : IEntityTypeAddedConvention, IModelFinalizingConvention
{
    // Almacena los tipos de elementos que se han marcado como MapOf para limpiarlos después
    // NOTA: No usar static para evitar interferencia entre tests paralelos
    private readonly HashSet<Type> _mapOfElementTypes = [];

    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;
        var clrType = entityType.ClrType;

        foreach (var propertyInfo in clrType.GetProperties())
        {
            var propertyType = propertyInfo.PropertyType;

            // Solo procesar diccionarios genéricos
            if (!ConventionHelpers.IsGenericDictionary(propertyType))
                continue;

            var keyType = ConventionHelpers.GetDictionaryKeyType(propertyType);
            var valueType = ConventionHelpers.GetDictionaryValueType(propertyType);

            if (keyType == null || valueType == null)
                continue;

            // Verificar que la clave es un tipo válido para Firestore Map keys (primitivo o enum)
            if (!IsValidMapKeyType(keyType))
                continue;

            // Ignorar tipos primitivos como value (no tiene sentido MapOf<TKey, int>)
            if (ConventionHelpers.IsPrimitiveOrSimpleType(valueType))
                continue;

            // Verificar si ya está configurado explícitamente
            if (entityType.IsMapOf(propertyInfo.Name))
                continue;

            // CRÍTICO: Ignorar la propiedad AHORA para evitar que EF Core falle
            IgnoreProperty(entityTypeBuilder, propertyInfo.Name);

            // Aplicar MapOf Embedded para elementos de clase
            if (valueType.IsClass && !valueType.IsAbstract)
            {
                ApplyMapOfEmbedded(entityType, propertyInfo.Name, keyType, valueType);
                _mapOfElementTypes.Add(valueType);
            }
        }
    }

    /// <summary>
    /// Se ejecuta al final del proceso de construcción del modelo.
    /// 1. Detecta backing fields para MapOf
    /// 2. Crea shadow properties para change tracking
    /// 3. Elimina entidades descubiertas incorrectamente por EF Core
    /// </summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;

        // Paso 1: Detectar diccionarios con backing fields que EF Core ignoró
        DetectIgnoredDictionariesWithBackingFields(model);

        // Paso 2: Limpiar entidades que son MapOf elements (no deben ser entidades independientes)
        var entitiesToRemove = model.GetEntityTypes()
            .Where(et => _mapOfElementTypes.Contains(et.ClrType))
            .ToList();

        foreach (var entityType in entitiesToRemove)
        {
            // Solo remover si no tiene PK (es un embedded type, no una entidad real)
            if (entityType.FindPrimaryKey() == null)
            {
                modelBuilder.Ignore(entityType.ClrType);
            }
        }

        _mapOfElementTypes.Clear();

        // Paso 3: Crear shadow properties para change tracking de todas las propiedades MapOf
        CreateShadowPropertiesForMapOf(model);
    }

    /// <summary>
    /// Crea shadow properties __{PropertyName}_Json para todas las propiedades MapOf.
    /// Estas shadow properties se usan para detectar cambios en los maps.
    /// </summary>
    private static void CreateShadowPropertiesForMapOf(IConventionModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            foreach (var propertyInfo in clrType.GetProperties())
            {
                var propertyName = propertyInfo.Name;

                // Solo procesar propiedades que están anotadas como MapOf
                if (!entityType.IsMapOf(propertyName))
                    continue;

                // Verificar si ya existe la shadow property
                var shadowPropertyName = MapOfAnnotations.GetShadowPropertyName(propertyName);
                if (entityType.FindProperty(shadowPropertyName) != null)
                    continue;

                // Crear la shadow property
                var mutableEntityType = (IMutableEntityType)entityType;
                var shadowProperty = mutableEntityType.AddProperty(shadowPropertyName, typeof(string));
                shadowProperty.IsNullable = true;

                // Marcar como tracker JSON
                shadowProperty.SetAnnotation(MapOfAnnotations.JsonTrackerFor, propertyName);
            }
        }
    }

    /// <summary>
    /// Detecta diccionarios con backing fields que EF Core ignoró silenciosamente.
    /// Esto incluye propiedades como IReadOnlyDictionary&lt;DayOfWeek, DaySchedule&gt; WeeklyHours
    /// que tienen un backing field _weeklyHours pero EF Core las ignora porque
    /// no tienen setter público.
    /// </summary>
    private void DetectIgnoredDictionariesWithBackingFields(IConventionModel model)
    {
        foreach (var entityType in model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;

            foreach (var (propertyName, fieldInfo, keyType, valueType) in ConventionHelpers.FindDictionaryBackingFields(clrType))
            {
                // Ya está configurado como MapOf
                if (entityType.IsMapOf(propertyName))
                    continue;

                // Es una propiedad escalar de EF Core
                if (entityType.FindProperty(propertyName) != null)
                    continue;

                // Verificar que la clave es un tipo válido
                if (!IsValidMapKeyType(keyType))
                    continue;

                // Ignorar tipos primitivos como value
                if (ConventionHelpers.IsPrimitiveOrSimpleType(valueType))
                    continue;

                // Aplicar MapOf Embedded
                if (valueType.IsClass && !valueType.IsAbstract)
                {
                    ApplyMapOfEmbedded(entityType, propertyName, keyType, valueType);
                    _mapOfElementTypes.Add(valueType);
                }
            }
        }
    }

    /// <summary>
    /// Verifica si un tipo es válido como clave de Firestore Map.
    /// En Firestore, las claves de Map son siempre strings, por lo que
    /// soportamos tipos que se pueden serializar a string de forma determinística.
    /// </summary>
    private static bool IsValidMapKeyType(Type type)
    {
        // Tipos primitivos (int, long, etc.) → ToString()
        if (type.IsPrimitive)
            return true;

        // String → directo
        if (type == typeof(string))
            return true;

        // Enums → ToString()
        if (type.IsEnum)
            return true;

        // Guid → ToString()
        if (type == typeof(Guid))
            return true;

        // DateTime, DateTimeOffset → ToString("o") formato ISO
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return true;

        // Nullable de tipos válidos
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
            return IsValidMapKeyType(underlyingType);

        return false;
    }

    private static void ApplyMapOfEmbedded(IConventionEntityType entityType, string propertyName, Type keyType, Type valueType)
    {
        var mutableEntityType = (IMutableEntityType)entityType;
        mutableEntityType.SetMapOfKeyClrType(propertyName, keyType);
        mutableEntityType.SetMapOfElementClrType(propertyName, valueType);
    }

    private static void IgnoreProperty(IConventionEntityTypeBuilder entityTypeBuilder, string propertyName)
    {
        entityTypeBuilder.Ignore(propertyName);
    }
}
