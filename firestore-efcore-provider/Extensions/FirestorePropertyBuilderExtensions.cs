using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Firestore.EntityFrameworkCore.Extensions;

public static class FirestorePropertyBuilderExtensions
{
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
}