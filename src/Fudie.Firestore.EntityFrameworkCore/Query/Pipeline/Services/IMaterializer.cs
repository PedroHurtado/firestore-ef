using System;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Converts shaped query results into typed CLR instances.
/// All metadata (types, kinds, property names) comes from ShapedResult.
/// </summary>
public interface IMaterializer
{
    /// <summary>
    /// Materializes shaped items into typed instances.
    /// </summary>
    /// <param name="shaped">The shaped result containing TypedItems with full metadata.</param>
    /// <param name="targetType">The CLR type to materialize (entity, DTO, record, anonymous type).</param>
    /// <returns>List of materialized instances.</returns>
    List<object> Materialize(ShapedResult shaped, Type targetType);
}
