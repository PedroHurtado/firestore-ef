using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.EntityFrameworkCore.Extensions;

public static class FirestorePropertyBuilderExtensions
{
    // ============= HELPER METHODS =============

    /// <summary>
    /// Navega desde un ComplexProperty hasta el EntityType contenedor.
    /// </summary>
    private static IMutableEntityType GetContainingEntityType(IReadOnlyComplexProperty complexProperty)
    {
        var declaringType = complexProperty.DeclaringType;

        // Si el tipo declarante es un EntityType, lo devolvemos
        if (declaringType is IMutableEntityType entityType)
        {
            return entityType;
        }

        // Si es un ComplexType, navegamos hacia arriba recursivamente
        if (declaringType is IReadOnlyComplexType complexType)
        {
            return GetContainingEntityType(complexType.ComplexProperty);
        }

        throw new InvalidOperationException(
            $"Cannot find containing EntityType for ComplexProperty '{complexProperty.Name}'");
    }

    // ============= PERSIST NULL VALUES =============

    /// <summary>
    /// Annotation key for PersistNullValues configuration.
    /// </summary>
    public const string PersistNullValuesAnnotation = "Firestore:PersistNullValues";

    /// <summary>
    /// Configures the property to persist null values explicitly in Firestore.
    /// By default, Firestore does not store fields with null values (NoSQL convention).
    /// Use this when you need to query by null (e.g., Where(x => x.Field == null)).
    /// </summary>
    /// <example>
    /// modelBuilder.Entity&lt;MyEntity&gt;(entity =>
    /// {
    ///     entity.Property(e => e.Description).PersistNullValues();
    /// });
    /// </example>
    public static PropertyBuilder PersistNullValues(this PropertyBuilder propertyBuilder)
    {
        propertyBuilder.Metadata.SetAnnotation(PersistNullValuesAnnotation, true);
        return propertyBuilder;
    }

    /// <summary>
    /// Configures the property to persist null values explicitly in Firestore (generic version).
    /// </summary>
    public static PropertyBuilder<TProperty> PersistNullValues<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        propertyBuilder.Metadata.SetAnnotation(PersistNullValuesAnnotation, true);
        return propertyBuilder;
    }

    /// <summary>
    /// Checks if the property is configured to persist null values.
    /// </summary>
    public static bool IsPersistNullValuesEnabled(this IProperty property)
    {
        return property.FindAnnotation(PersistNullValuesAnnotation)?.Value is true;
    }

    // ============= GEOPOINT (ComplexProperty) =============

    /// <summary>
    /// Marca un ComplexProperty como GeoPoint de Firestore.
    /// Necesario porque GeoPoint requiere configuración especial para detectar Latitude/Longitude.
    /// </summary>
    public static ComplexPropertyBuilder HasGeoPoint(this ComplexPropertyBuilder propertyBuilder)
    {
        propertyBuilder.Metadata.SetAnnotation("Firestore:IsGeoPoint", true);
        return propertyBuilder;
    }

    // ============= REFERENCE (ComplexProperty) =============

    /// <summary>
    /// Marca una propiedad de navegación dentro de un ComplexType como DocumentReference.
    /// Esto permite referencias a entidades desde dentro de ComplexTypes/Value Objects.
    /// </summary>
    /// <example>
    /// entity.ComplexProperty(e => e.Direccion, direccion =>
    /// {
    ///     direccion.Reference(d => d.SucursalCercana);
    /// });
    /// </example>
    public static ComplexPropertyBuilder<TComplex> Reference<TComplex, TRelated>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, TRelated?>> navigationExpression)
        where TRelated : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        // Obtener lista existente de referencias o crear nueva
        var existingRefs = builder.Metadata.FindAnnotation("Firestore:NestedReferences")?.Value as List<string>
            ?? new List<string>();

        // Agregar la nueva referencia si no existe
        if (!existingRefs.Contains(propertyName))
        {
            existingRefs.Add(propertyName);
        }

        builder.Metadata.SetAnnotation("Firestore:NestedReferences", existingRefs);

        return builder;
    }

    // ============= MAPOF (ComplexProperty) =============

    /// <summary>
    /// Configura una propiedad como un Map (diccionario) embebido dentro de un ComplexProperty.
    /// </summary>
    /// <typeparam name="TComplex">Tipo del ComplexType</typeparam>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TValue">Tipo de los valores del diccionario</typeparam>
    /// <param name="builder">El ComplexPropertyBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public static ComplexPropertyBuilder<TComplex> MapOf<TComplex, TKey, TValue>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, IReadOnlyDictionary<TKey, TValue>>> propertyExpression)
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var existingMaps = builder.Metadata.FindAnnotation("Firestore:NestedMaps")?.Value as List<string>
            ?? new List<string>();

        if (!existingMaps.Contains(propertyName))
        {
            existingMaps.Add(propertyName);
        }

        builder.Metadata.SetAnnotation("Firestore:NestedMaps", existingMaps);

        return builder;
    }

    /// <summary>
    /// Configura una propiedad como un Map (diccionario) embebido dentro de un ComplexProperty con configuración de elementos.
    /// </summary>
    /// <typeparam name="TComplex">Tipo del ComplexType</typeparam>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TValue">Tipo de los valores del diccionario</typeparam>
    /// <param name="builder">El ComplexPropertyBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <param name="configure">Acción para configurar los elementos del diccionario</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public static ComplexPropertyBuilder<TComplex> MapOf<TComplex, TKey, TValue>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, IReadOnlyDictionary<TKey, TValue>>> propertyExpression,
        Action<MapOfElementBuilder<TValue>> configure)
        where TValue : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var existingMaps = builder.Metadata.FindAnnotation("Firestore:NestedMaps")?.Value as List<string>
            ?? new List<string>();

        if (!existingMaps.Contains(propertyName))
        {
            existingMaps.Add(propertyName);
        }

        builder.Metadata.SetAnnotation("Firestore:NestedMaps", existingMaps);

        // Crear el element builder para la configuración
        var elementBuilder = new MapOfElementBuilder<TValue>(
            GetContainingEntityType(builder.Metadata),
            $"{builder.Metadata.Name}.{propertyName}");

        configure(elementBuilder);

        return builder;
    }

    // ============= ARRAYOF (ComplexProperty) =============

    /// <summary>
    /// Configura una propiedad como un array embebido dentro de un ComplexProperty.
    /// </summary>
    /// <typeparam name="TComplex">Tipo del ComplexType</typeparam>
    /// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
    /// <param name="builder">El ComplexPropertyBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public static ComplexPropertyBuilder<TComplex> ArrayOf<TComplex, TElement>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, IEnumerable<TElement>>> propertyExpression)
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var existingArrays = builder.Metadata.FindAnnotation("Firestore:NestedArrays")?.Value as List<string>
            ?? new List<string>();

        if (!existingArrays.Contains(propertyName))
        {
            existingArrays.Add(propertyName);
        }

        builder.Metadata.SetAnnotation("Firestore:NestedArrays", existingArrays);

        return builder;
    }

    /// <summary>
    /// Configura una propiedad como un array embebido dentro de un ComplexProperty con configuración de elementos.
    /// </summary>
    /// <typeparam name="TComplex">Tipo del ComplexType</typeparam>
    /// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
    /// <param name="builder">El ComplexPropertyBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <param name="configure">Acción para configurar los elementos del array</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public static ComplexPropertyBuilder<TComplex> ArrayOf<TComplex, TElement>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, IEnumerable<TElement>>> propertyExpression,
        Action<ArrayOfElementBuilder<TElement>> configure)
        where TElement : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var existingArrays = builder.Metadata.FindAnnotation("Firestore:NestedArrays")?.Value as List<string>
            ?? new List<string>();

        if (!existingArrays.Contains(propertyName))
        {
            existingArrays.Add(propertyName);
        }

        builder.Metadata.SetAnnotation("Firestore:NestedArrays", existingArrays);

        // Crear el element builder para la configuración
        var elementBuilder = new ArrayOfElementBuilder<TElement>(
            GetContainingEntityType(builder.Metadata),
            $"{builder.Metadata.Name}.{propertyName}");

        configure(elementBuilder);

        return builder;
    }
}