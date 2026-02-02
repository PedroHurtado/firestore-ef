// Archivo: Metadata/Builders/SubCollectionElementBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para configurar elementos dentro de una subcollection.
/// Permite configurar referencias y arrays embebidos dentro de documentos de subcollection.
/// </summary>
/// <typeparam name="TElement">Tipo del elemento de la subcollection</typeparam>
public class SubCollectionElementBuilder<TElement>
    where TElement : class
{
    private readonly IMutableEntityType _elementEntityType;
    private readonly List<SubCollectionNestedReference> _nestedReferences = [];
    private readonly List<SubCollectionNestedArrayOf> _nestedArrays = [];

    internal SubCollectionElementBuilder(IMutableEntityType elementEntityType)
    {
        _elementEntityType = elementEntityType;
    }

    /// <summary>
    /// Proporciona acceso al EntityTypeBuilder subyacente para configuraciones avanzadas.
    /// Permite usar Ignore(), Property(), ComplexProperty(), y cualquier otra configuración de EF Core.
    /// </summary>
    /// <param name="configure">Acción que recibe el EntityTypeBuilder para configurar la entidad</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    /// <example>
    /// <code>
    /// entity.SubCollection(e => e.Categories, category =>
    /// {
    ///     category.Entity(builder =>
    ///     {
    ///         builder.Ignore(c => c.ComputedProperty);
    ///         builder.Property(c => c.Name).IsRequired();
    ///     });
    /// });
    /// </code>
    /// </example>
    public SubCollectionElementBuilder<TElement> Entity(
        Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TElement>> configure)
    {
#pragma warning disable EF1001 // Internal EF Core API usage
        var entityTypeBuilder = new Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TElement>(_elementEntityType);
#pragma warning restore EF1001
        configure(entityTypeBuilder);
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como una referencia a otra entidad.
    /// Se almacenará como DocumentReference en Firestore.
    /// </summary>
    /// <typeparam name="TRef">Tipo de la entidad referenciada</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad de referencia</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    /// <example>
    /// <code>
    /// entity.SubCollection(e => e.Pedidos, pedido =>
    /// {
    ///     pedido.Reference(p => p.Cliente);
    /// });
    /// </code>
    /// </example>
    public SubCollectionElementBuilder<TElement> Reference<TRef>(
        Expression<Func<TElement, TRef?>> propertyExpression)
        where TRef : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        // Buscar la navegación y marcarla como DocumentReference
        var navigation = _elementEntityType.FindNavigation(propertyName);
        if (navigation != null)
        {
            navigation.SetAnnotation("Firestore:DocumentReference", true);
        }

        _nestedReferences.Add(new SubCollectionNestedReference(propertyName, typeof(TRef)));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array embebido.
    /// Se almacenará como array de objetos dentro del documento de la subcollection.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <returns>Un ArrayOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// entity.SubCollection(e => e.Pedidos, pedido =>
    /// {
    ///     pedido.ArrayOf(p => p.Lineas);
    /// });
    /// </code>
    /// </example>
    public ArrayOfBuilder<TElement, TNested> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression)
        where TNested : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        _nestedArrays.Add(new SubCollectionNestedArrayOf(propertyName, typeof(TNested), null));

        // Crear el ArrayOfBuilder usando el EntityType de la subcollection
#pragma warning disable EF1001 // Internal EF Core API usage
        var entityTypeBuilder = new Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TElement>(_elementEntityType);
#pragma warning restore EF1001

        return new ArrayOfBuilder<TElement, TNested>(entityTypeBuilder, propertyExpression);
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array embebido con configuración adicional.
    /// Permite configurar referencias y arrays anidados dentro de los elementos del array.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <param name="configure">Acción para configurar los elementos del array</param>
    /// <returns>Un ArrayOfBuilder para configuración adicional</returns>
    /// <example>
    /// <code>
    /// entity.SubCollection(e => e.Pedidos, pedido =>
    /// {
    ///     pedido.ArrayOf(p => p.Lineas, linea =>
    ///     {
    ///         linea.Reference(l => l.Producto);
    ///     });
    /// });
    /// </code>
    /// </example>
    public ArrayOfBuilder<TElement, TNested> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression,
        Action<ArrayOfElementBuilder<TNested>> configure)
        where TNested : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

#pragma warning disable EF1001 // Internal EF Core API usage
        var entityTypeBuilder = new Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TElement>(_elementEntityType);
#pragma warning restore EF1001

        var arrayBuilder = new ArrayOfBuilder<TElement, TNested>(entityTypeBuilder, propertyExpression);
        var elementBuilder = new ArrayOfElementBuilder<TNested>(_elementEntityType, propertyName);

        configure(elementBuilder);

        _nestedArrays.Add(new SubCollectionNestedArrayOf(propertyName, typeof(TNested), elementBuilder));

        return arrayBuilder;
    }

    /// <summary>
    /// Obtiene las referencias anidadas configuradas
    /// </summary>
    internal IReadOnlyList<SubCollectionNestedReference> NestedReferences => _nestedReferences;

    /// <summary>
    /// Obtiene los arrays anidados configurados
    /// </summary>
    internal IReadOnlyList<SubCollectionNestedArrayOf> NestedArrays => _nestedArrays;
}

/// <summary>
/// Representa una referencia anidada dentro de un elemento de subcollection
/// </summary>
internal sealed record SubCollectionNestedReference(string PropertyName, Type ReferencedType);

/// <summary>
/// Representa un array anidado dentro de un elemento de subcollection
/// </summary>
internal sealed record SubCollectionNestedArrayOf(string PropertyName, Type ElementType, object? NestedBuilder);
