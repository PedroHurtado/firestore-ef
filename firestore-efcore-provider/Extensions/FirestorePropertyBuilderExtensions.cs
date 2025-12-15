using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Firestore.EntityFrameworkCore.Extensions;

public static class FirestorePropertyBuilderExtensions
{
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