// Archivo: Metadata/Builders/MapOfElementBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para configurar elementos dentro de un Map embebido.
/// Permite configurar propiedades, referencias, arrays anidados y maps anidados dentro del valor del diccionario.
/// </summary>
/// <typeparam name="TElement">Tipo del valor del diccionario</typeparam>
public class MapOfElementBuilder<TElement>
    where TElement : class
{
    private readonly IMutableEntityType _parentEntityType;
    private readonly string _parentPropertyName;
    private readonly List<MapOfNestedProperty> _nestedProperties = new();
    private readonly List<MapOfNestedReference> _nestedReferences = new();
    private readonly List<MapOfNestedArray> _nestedArrays = new();
    private readonly List<MapOfNestedMap> _nestedMaps = new();

    internal MapOfElementBuilder(IMutableEntityType parentEntityType, string parentPropertyName)
    {
        _parentEntityType = parentEntityType;
        _parentPropertyName = parentPropertyName;
    }

    /// <summary>
    /// Configura una propiedad escalar del elemento.
    /// </summary>
    /// <typeparam name="TProperty">Tipo de la propiedad</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> Property<TProperty>(
        Expression<Func<TElement, TProperty>> propertyExpression)
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedProperties.Add(new MapOfNestedProperty(memberInfo.Name, typeof(TProperty)));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como una referencia a otra entidad.
    /// </summary>
    /// <typeparam name="TRef">Tipo de la entidad referenciada</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad de referencia</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> Reference<TRef>(
        Expression<Func<TElement, TRef?>> propertyExpression)
        where TRef : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedReferences.Add(new MapOfNestedReference(memberInfo.Name, typeof(TRef)));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array anidado.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression)
        where TNested : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedArrays.Add(new MapOfNestedArray(memberInfo.Name, typeof(TNested), null));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array anidado con configuración adicional.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <param name="configure">Acción para configurar los elementos del array anidado</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression,
        Action<ArrayOfElementBuilder<TNested>> configure)
        where TNested : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var nestedBuilder = new ArrayOfElementBuilder<TNested>(_parentEntityType, $"{_parentPropertyName}.{memberInfo.Name}");
        configure(nestedBuilder);
        _nestedArrays.Add(new MapOfNestedArray(memberInfo.Name, typeof(TNested), nestedBuilder));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un diccionario anidado (Map de Maps).
    /// </summary>
    /// <typeparam name="TNestedKey">Tipo de la clave del diccionario anidado</typeparam>
    /// <typeparam name="TNestedElement">Tipo del valor del diccionario anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> MapOf<TNestedKey, TNestedElement>(
        Expression<Func<TElement, IReadOnlyDictionary<TNestedKey, TNestedElement>>> propertyExpression)
        where TNestedElement : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedMaps.Add(new MapOfNestedMap(memberInfo.Name, typeof(TNestedKey), typeof(TNestedElement), null));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un diccionario anidado con configuración adicional.
    /// </summary>
    /// <typeparam name="TNestedKey">Tipo de la clave del diccionario anidado</typeparam>
    /// <typeparam name="TNestedElement">Tipo del valor del diccionario anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <param name="configure">Acción para configurar los elementos del diccionario anidado</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> MapOf<TNestedKey, TNestedElement>(
        Expression<Func<TElement, IReadOnlyDictionary<TNestedKey, TNestedElement>>> propertyExpression,
        Action<MapOfElementBuilder<TNestedElement>> configure)
        where TNestedElement : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var nestedBuilder = new MapOfElementBuilder<TNestedElement>(_parentEntityType, $"{_parentPropertyName}.{memberInfo.Name}");
        configure(nestedBuilder);
        _nestedMaps.Add(new MapOfNestedMap(memberInfo.Name, typeof(TNestedKey), typeof(TNestedElement), nestedBuilder));
        return this;
    }

    /// <summary>
    /// Ignora una propiedad del elemento para que no se serialice a Firestore.
    /// Útil para propiedades calculadas (getters sin backing field).
    /// </summary>
    /// <typeparam name="TProperty">Tipo de la propiedad a ignorar</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad a ignorar</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public MapOfElementBuilder<TElement> Ignore<TProperty>(
        Expression<Func<TElement, TProperty>> propertyExpression)
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _parentEntityType.AddMapOfIgnoredProperty(_parentPropertyName, memberInfo.Name);
        return this;
    }

    /// <summary>
    /// Obtiene las propiedades escalares configuradas
    /// </summary>
    internal IReadOnlyList<MapOfNestedProperty> NestedProperties => _nestedProperties;

    /// <summary>
    /// Obtiene las referencias anidadas configuradas
    /// </summary>
    internal IReadOnlyList<MapOfNestedReference> NestedReferences => _nestedReferences;

    /// <summary>
    /// Obtiene los arrays anidados configurados
    /// </summary>
    internal IReadOnlyList<MapOfNestedArray> NestedArrays => _nestedArrays;

    /// <summary>
    /// Obtiene los maps anidados configurados
    /// </summary>
    internal IReadOnlyList<MapOfNestedMap> NestedMaps => _nestedMaps;
}

/// <summary>
/// Representa una propiedad escalar dentro de un elemento de Map
/// </summary>
internal sealed record MapOfNestedProperty(string PropertyName, Type PropertyType);

/// <summary>
/// Representa una referencia anidada dentro de un elemento de Map
/// </summary>
internal sealed record MapOfNestedReference(string PropertyName, Type ReferencedType);

/// <summary>
/// Representa un array anidado dentro de un elemento de Map
/// </summary>
internal sealed record MapOfNestedArray(string PropertyName, Type ElementType, object? NestedBuilder);

/// <summary>
/// Representa un Map anidado dentro de un elemento de Map
/// </summary>
internal sealed record MapOfNestedMap(string PropertyName, Type KeyType, Type ElementType, object? NestedBuilder);
