using System;
using System.Collections.Generic;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Describes how to materialize a specific CLR type from a dictionary.
/// Cached per type to avoid repeated reflection.
/// </summary>
/// <param name="Constructor">The constructor to use for creating instances.</param>
/// <param name="ConstructorParams">Mappings from dictionary keys to constructor parameters.</param>
/// <param name="MemberSetters">Mappings from dictionary keys to properties/backing fields.</param>
public record MaterializationStrategy(
    ConstructorInfo Constructor,
    IReadOnlyList<ConstructorParamMapping> ConstructorParams,
    IReadOnlyList<MemberMapping> MemberSetters);

/// <summary>
/// Maps a dictionary key to a constructor parameter.
/// </summary>
/// <param name="DictKey">The key in the dictionary (PascalCase).</param>
/// <param name="ParamIndex">The index of the parameter in the constructor.</param>
/// <param name="TargetType">The CLR type of the parameter.</param>
public record ConstructorParamMapping(
    string DictKey,
    int ParamIndex,
    Type TargetType);

/// <summary>
/// Maps a dictionary key to a property or backing field.
/// </summary>
/// <param name="DictKey">The key in the dictionary (PascalCase).</param>
/// <param name="Member">The PropertyInfo or FieldInfo to set.</param>
/// <param name="TargetType">The CLR type of the member.</param>
public record MemberMapping(
    string DictKey,
    MemberInfo Member,
    Type TargetType);
