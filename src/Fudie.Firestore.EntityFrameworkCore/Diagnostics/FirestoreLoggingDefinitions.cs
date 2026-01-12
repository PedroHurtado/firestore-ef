using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fudie.Firestore.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Caching for Firestore event definitions.
/// </summary>
public class FirestoreLoggingDefinitions : LoggingDefinitions
{
    /// <summary>
    /// Cache for QueryExecuting event.
    /// </summary>
    public EventDefinitionBase? LogQueryExecuting;

    /// <summary>
    /// Cache for QueryExecuted event.
    /// </summary>
    public EventDefinitionBase? LogQueryExecuted;

    /// <summary>
    /// Cache for DocumentFetching event.
    /// </summary>
    public EventDefinitionBase? LogDocumentFetching;

    /// <summary>
    /// Cache for DocumentFetched event.
    /// </summary>
    public EventDefinitionBase? LogDocumentFetched;

    /// <summary>
    /// Cache for AggregationExecuting event.
    /// </summary>
    public EventDefinitionBase? LogAggregationExecuting;

    /// <summary>
    /// Cache for AggregationExecuted event.
    /// </summary>
    public EventDefinitionBase? LogAggregationExecuted;

    /// <summary>
    /// Cache for CollectionQuerying event.
    /// </summary>
    public EventDefinitionBase? LogCollectionQuerying;

    /// <summary>
    /// Cache for CollectionQueried event.
    /// </summary>
    public EventDefinitionBase? LogCollectionQueried;

    /// <summary>
    /// Cache for formatted query message event.
    /// </summary>
    public EventDefinitionBase? LogQueryMessage;

    /// <summary>
    /// Cache for command executed event (Insert, Update, Delete).
    /// </summary>
    public EventDefinitionBase? LogCommandExecuted;
}
