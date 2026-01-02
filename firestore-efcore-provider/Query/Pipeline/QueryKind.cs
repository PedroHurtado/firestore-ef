namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Defines the type of query being executed through the pipeline.
/// Used by handlers to determine if they should process or skip the query.
/// </summary>
public enum QueryKind
{
    /// <summary>
    /// Query that returns complete entities: ToList(), First(), Single()
    /// </summary>
    Entity,

    /// <summary>
    /// Aggregation query: Count(), Sum(), Average(), Min(), Max()
    /// </summary>
    Aggregation,

    /// <summary>
    /// Query with projection: Select(x => new { x.Name })
    /// </summary>
    Projection,

    /// <summary>
    /// Query that returns bool: Any(), All(), Contains()
    /// </summary>
    Predicate
}
