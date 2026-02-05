// Archivo: Metadata/Builders/ArrayOfElementBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Builder para configurar elementos dentro de un array embebido.
/// Permite configurar referencias y arrays anidados dentro de ComplexTypes.
/// </summary>
/// <typeparam name="TElement">Tipo del elemento del array</typeparam>
public class ArrayOfElementBuilder<TElement>
    where TElement : class
{
    private readonly IMutableEntityType _parentEntityType;
    private readonly string _parentPropertyName;
    private readonly List<ArrayOfNestedReference> _nestedReferences = new();
    private readonly List<ArrayOfNestedArray> _nestedArrays = new();
    private readonly List<ArrayOfNestedMap> _nestedMaps = new();

    internal ArrayOfElementBuilder(IMutableEntityType parentEntityType, string parentPropertyName)
    {
        _parentEntityType = parentEntityType;
        _parentPropertyName = parentPropertyName;
    }

    /// <summary>
    /// Configura una propiedad del elemento como una referencia a otra entidad.
    /// </summary>
    /// <typeparam name="TRef">Tipo de la entidad referenciada</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad de referencia</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> Reference<TRef>(
        Expression<Func<TElement, TRef?>> propertyExpression)
        where TRef : class
    {
        // TODO: Fase 4 - Implementar registro de referencia anidada
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedReferences.Add(new ArrayOfNestedReference(memberInfo.Name, typeof(TRef)));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array anidado.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression)
        where TNested : class
    {
        // TODO: Fase 5 - Implementar registro de array anidado
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedArrays.Add(new ArrayOfNestedArray(memberInfo.Name, typeof(TNested), null));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un array anidado con configuración adicional.
    /// </summary>
    /// <typeparam name="TNested">Tipo de los elementos del array anidado</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del array</param>
    /// <param name="configure">Acción para configurar los elementos del array anidado</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression,
        Action<ArrayOfElementBuilder<TNested>> configure)
        where TNested : class
    {
        // TODO: Fase 5 - Implementar registro de array anidado con configuración
        var memberInfo = propertyExpression.GetMemberAccess();
        var nestedBuilder = new ArrayOfElementBuilder<TNested>(_parentEntityType, $"{_parentPropertyName}.{memberInfo.Name}");
        configure(nestedBuilder);
        _nestedArrays.Add(new ArrayOfNestedArray(memberInfo.Name, typeof(TNested), nestedBuilder));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un diccionario anidado (Map dentro de Array).
    /// </summary>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TValue">Tipo del valor del diccionario</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> MapOf<TKey, TValue>(
        Expression<Func<TElement, IReadOnlyDictionary<TKey, TValue>>> propertyExpression)
        where TValue : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _nestedMaps.Add(new ArrayOfNestedMap(memberInfo.Name, typeof(TKey), typeof(TValue), null));
        return this;
    }

    /// <summary>
    /// Configura una propiedad del elemento como un diccionario anidado con configuración adicional.
    /// </summary>
    /// <typeparam name="TKey">Tipo de la clave del diccionario</typeparam>
    /// <typeparam name="TValue">Tipo del valor del diccionario</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad del diccionario</param>
    /// <param name="configure">Acción para configurar los elementos del diccionario</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> MapOf<TKey, TValue>(
        Expression<Func<TElement, IReadOnlyDictionary<TKey, TValue>>> propertyExpression,
        Action<MapOfElementBuilder<TValue>> configure)
        where TValue : class
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        var nestedBuilder = new MapOfElementBuilder<TValue>(_parentEntityType, $"{_parentPropertyName}.{memberInfo.Name}");
        configure(nestedBuilder);
        _nestedMaps.Add(new ArrayOfNestedMap(memberInfo.Name, typeof(TKey), typeof(TValue), nestedBuilder));
        return this;
    }

    /// <summary>
    /// Ignora una propiedad del elemento para que no se serialice a Firestore.
    /// Útil para propiedades calculadas (getters sin backing field).
    /// </summary>
    /// <typeparam name="TProperty">Tipo de la propiedad a ignorar</typeparam>
    /// <param name="propertyExpression">Expresión que identifica la propiedad a ignorar</param>
    /// <returns>El builder para encadenamiento fluent</returns>
    public ArrayOfElementBuilder<TElement> Ignore<TProperty>(
        Expression<Func<TElement, TProperty>> propertyExpression)
    {
        var memberInfo = propertyExpression.GetMemberAccess();
        _parentEntityType.AddArrayOfIgnoredProperty(_parentPropertyName, memberInfo.Name);
        return this;
    }

    /// <summary>
    /// Obtiene las referencias anidadas configuradas
    /// </summary>
    internal IReadOnlyList<ArrayOfNestedReference> NestedReferences => _nestedReferences;

    /// <summary>
    /// Obtiene los arrays anidados configurados
    /// </summary>
    internal IReadOnlyList<ArrayOfNestedArray> NestedArrays => _nestedArrays;

    /// <summary>
    /// Obtiene los maps anidados configurados
    /// </summary>
    internal IReadOnlyList<ArrayOfNestedMap> NestedMaps => _nestedMaps;
}

/// <summary>
/// Representa una referencia anidada dentro de un elemento de array
/// </summary>
internal sealed record ArrayOfNestedReference(string PropertyName, Type ReferencedType);

/// <summary>
/// Representa un array anidado dentro de un elemento de array
/// </summary>
internal sealed record ArrayOfNestedArray(string PropertyName, Type ElementType, object? NestedBuilder);

/// <summary>
/// Representa un Map anidado dentro de un elemento de array
/// </summary>
internal sealed record ArrayOfNestedMap(string PropertyName, Type KeyType, Type ElementType, object? NestedBuilder);
