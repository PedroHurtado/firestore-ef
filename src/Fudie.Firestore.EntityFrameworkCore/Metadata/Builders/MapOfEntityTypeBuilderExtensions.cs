// Archivo: Metadata/Builders/MapOfEntityTypeBuilderExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Métodos de extensión para configurar Maps (diccionarios) embebidos en documentos de Firestore.
/// </summary>
public static class MapOfEntityTypeBuilderExtensions
{
    /// <summary>
    /// Configura una propiedad como un Map (diccionario) embebido en el documento de Firestore.
    /// Cada clave del diccionario se convierte en un campo del Map y cada valor en un objeto anidado.
    /// </summary>
    /// <typeparam name="TEntity">Tipo de la entidad</typeparam>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TElement">Tipo de los valores del diccionario</typeparam>
    /// <param name="builder">El EntityTypeBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <returns>Un MapOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Restaurant&gt;(entity =&gt;
    /// {
    ///     // Map de DayOfWeek a DaySchedule
    ///     entity.MapOf(e =&gt; e.WeeklyHours);
    /// });
    /// </code>
    /// </example>
    public static MapOfBuilder<TEntity, TKey, TElement> MapOf<TEntity, TKey, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IReadOnlyDictionary<TKey, TElement>>> propertyExpression)
        where TEntity : class
        where TElement : class
    {
        return new MapOfBuilder<TEntity, TKey, TElement>(builder, propertyExpression);
    }

    /// <summary>
    /// Configura una propiedad como un Map (diccionario) embebido con configuración de elementos.
    /// Permite configurar propiedades, referencias, arrays y maps anidados dentro de los valores.
    /// </summary>
    /// <typeparam name="TEntity">Tipo de la entidad</typeparam>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TElement">Tipo de los valores del diccionario</typeparam>
    /// <param name="builder">El EntityTypeBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <param name="configure">Acción para configurar los elementos del diccionario</param>
    /// <returns>Un MapOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Restaurant&gt;(entity =&gt;
    /// {
    ///     // Map con configuración de elementos
    ///     entity.MapOf(e =&gt; e.WeeklyHours, day =&gt;
    ///     {
    ///         day.Property(d =&gt; d.IsClosed);
    ///         day.ArrayOf(d =&gt; d.TimeSlots, ts =&gt;
    ///         {
    ///             ts.Property(t =&gt; t.Open);
    ///             ts.Property(t =&gt; t.Close);
    ///         });
    ///     });
    /// });
    /// </code>
    /// </example>
    public static MapOfBuilder<TEntity, TKey, TElement> MapOf<TEntity, TKey, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IReadOnlyDictionary<TKey, TElement>>> propertyExpression,
        Action<MapOfElementBuilder<TElement>> configure)
        where TEntity : class
        where TElement : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var mapBuilder = new MapOfBuilder<TEntity, TKey, TElement>(builder, propertyExpression);
        var elementBuilder = new MapOfElementBuilder<TElement>(builder.Metadata, propertyName);

        configure(elementBuilder);

        // TODO: Almacenar configuración de elementBuilder en anotaciones

        return mapBuilder;
    }
}
