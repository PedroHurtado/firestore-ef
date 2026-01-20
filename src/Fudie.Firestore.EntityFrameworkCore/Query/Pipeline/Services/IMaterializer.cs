using System;
using System.Collections.Generic;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts shaped query results (hierarchical dictionaries) into typed CLR instances.
/// </summary>
public interface IMaterializer
{
    /// <summary>
    /// Materializes shaped dictionaries into typed instances.
    /// </summary>
    /// <param name="shaped">The shaped result from SnapshotShaper containing hierarchical dictionaries.</param>
    /// <param name="targetType">The CLR type to materialize (entity, DTO, record, anonymous type).</param>
    /// <param name="projectedFields">Optional projection fields for mapping dictionary keys to constructor parameters.
    /// When provided, uses FieldPath as dictionary key and ResultName as constructor parameter name.</param>
    /// <returns>List of materialized instances.</returns>
    List<object> Materialize(ShapedResult shaped, Type targetType, IReadOnlyList<FirestoreProjectedField>? projectedFields = null);
}
