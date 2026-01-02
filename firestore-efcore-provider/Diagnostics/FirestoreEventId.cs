using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Firestore.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Event IDs for Firestore events that can be logged.
/// Use these with ConfigureWarnings to control logging behavior.
/// </summary>
/// <example>
/// <code>
/// optionsBuilder.ConfigureWarnings(w => w
///     .Log((FirestoreEventId.QueryExecuting, LogLevel.Debug))
///     .Ignore(FirestoreEventId.QueryExecuted));
/// </code>
/// </example>
public static class FirestoreEventId
{
    // Base ID for Firestore events (avoid collision with EF Core events)
    private const int FirestoreBaseId = 100000;

    private enum Id
    {
        // Query events (100000-100099)
        QueryExecuting = FirestoreBaseId,
        QueryExecuted,
        DocumentFetching,
        DocumentFetched,
        AggregationExecuting,
        AggregationExecuted,

        // Collection events (100100-100199)
        CollectionQuerying = FirestoreBaseId + 100,
        CollectionQueried,
    }

    private static readonly string _queryPrefix = DbLoggerCategory.Query.Name + ".";

    private static EventId MakeQueryId(Id id)
        => new((int)id, _queryPrefix + id);

    /// <summary>
    /// A Firestore query is about to be executed.
    /// </summary>
    public static readonly EventId QueryExecuting = MakeQueryId(Id.QueryExecuting);

    /// <summary>
    /// A Firestore query has been executed.
    /// </summary>
    public static readonly EventId QueryExecuted = MakeQueryId(Id.QueryExecuted);

    /// <summary>
    /// A single document is about to be fetched.
    /// </summary>
    public static readonly EventId DocumentFetching = MakeQueryId(Id.DocumentFetching);

    /// <summary>
    /// A single document has been fetched.
    /// </summary>
    public static readonly EventId DocumentFetched = MakeQueryId(Id.DocumentFetched);

    /// <summary>
    /// An aggregation query is about to be executed.
    /// </summary>
    public static readonly EventId AggregationExecuting = MakeQueryId(Id.AggregationExecuting);

    /// <summary>
    /// An aggregation query has been executed.
    /// </summary>
    public static readonly EventId AggregationExecuted = MakeQueryId(Id.AggregationExecuted);

    /// <summary>
    /// A collection query is about to be executed.
    /// </summary>
    public static readonly EventId CollectionQuerying = MakeQueryId(Id.CollectionQuerying);

    /// <summary>
    /// A collection query has been executed.
    /// </summary>
    public static readonly EventId CollectionQueried = MakeQueryId(Id.CollectionQueried);
}
