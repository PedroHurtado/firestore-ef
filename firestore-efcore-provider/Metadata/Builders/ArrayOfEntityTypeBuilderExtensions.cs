// Archivo: Metadata/Builders/ArrayOfEntityTypeBuilderExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Métodos de extensión para configurar arrays embebidos en documentos de Firestore.
/// </summary>
public static class ArrayOfEntityTypeBuilderExtensions
{
    /// <summary>
    /// Configura una propiedad como un array de elementos embebidos en el documento de Firestore.
    /// Por defecto se configura como Embedded (ComplexType serializado como objeto JSON).
    /// </summary>
    /// <typeparam name="TEntity">Tipo de la entidad</typeparam>
    /// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
    /// <param name="builder">El EntityTypeBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <returns>Un ArrayOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Restaurante&gt;(entity =&gt;
    /// {
    ///     // Array de ComplexType embebido
    ///     entity.ArrayOf(e =&gt; e.Horarios);
    ///
    ///     // Array de GeoPoints
    ///     entity.ArrayOf(e =&gt; e.ZonasCobertura).AsGeoPoints();
    ///
    ///     // Array de References
    ///     entity.ArrayOf(e =&gt; e.Categorias).AsReferences();
    /// });
    /// </code>
    /// </example>
    public static ArrayOfBuilder<TEntity, TElement> ArrayOf<TEntity, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression)
        where TEntity : class
        where TElement : class
    {
        return new ArrayOfBuilder<TEntity, TElement>(builder, propertyExpression);
    }

    /// <summary>
    /// Configura una propiedad como un array de elementos embebidos con configuración de elementos.
    /// Permite configurar referencias y arrays anidados dentro de los elementos.
    /// </summary>
    /// <typeparam name="TEntity">Tipo de la entidad</typeparam>
    /// <typeparam name="TElement">Tipo de los elementos del array</typeparam>
    /// <param name="builder">El EntityTypeBuilder</param>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <param name="configure">Acción para configurar los elementos del array</param>
    /// <returns>Un ArrayOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Restaurante&gt;(entity =&gt;
    /// {
    ///     // Array con Reference dentro del ComplexType
    ///     entity.ArrayOf(e =&gt; e.Certificaciones, c =&gt;
    ///     {
    ///         c.Reference(x =&gt; x.Certificador);
    ///     });
    ///
    ///     // Array anidado con Reference al final
    ///     entity.ArrayOf(e =&gt; e.Menus, menu =&gt;
    ///     {
    ///         menu.ArrayOf(m =&gt; m.Secciones, seccion =&gt;
    ///         {
    ///             seccion.ArrayOf(s =&gt; s.Items, item =&gt;
    ///             {
    ///                 item.Reference(i =&gt; i.Plato);
    ///             });
    ///         });
    ///     });
    /// });
    /// </code>
    /// </example>
    public static ArrayOfBuilder<TEntity, TElement> ArrayOf<TEntity, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression,
        Action<ArrayOfElementBuilder<TElement>> configure)
        where TEntity : class
        where TElement : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var arrayBuilder = new ArrayOfBuilder<TEntity, TElement>(builder, propertyExpression);
        var elementBuilder = new ArrayOfElementBuilder<TElement>(builder.Metadata, propertyName);

        configure(elementBuilder);

        // TODO: Fase 4/5 - Almacenar configuración de elementBuilder en anotaciones

        return arrayBuilder;
    }
}
