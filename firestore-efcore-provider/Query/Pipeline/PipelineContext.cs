using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using System;
using System.Collections.Immutable;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Immutable context that flows through the query pipeline.
/// Contains all information needed by handlers to process the query.
/// </summary>
public record PipelineContext
{
    /// <summary>
    /// The AST expression representing the query.
    /// </summary>
    public required FirestoreQueryExpression Ast { get; init; }

    /// <summary>
    /// The query context containing parameter values and model metadata.
    /// </summary>
    public required IFirestoreQueryContext QueryContext { get; init; }

    /// <summary>
    /// Whether change tracking is enabled for this query.
    /// </summary>
    public required bool IsTracking { get; init; }

    /// <summary>
    /// The expected result type of the query.
    /// </summary>
    public required Type ResultType { get; init; }

    /// <summary>
    /// The kind of query being executed.
    /// Used by handlers to determine if they should process or skip.
    /// </summary>
    public required QueryKind Kind { get; init; }

    /// <summary>
    /// The root entity type (null for anonymous projections).
    /// </summary>
    public Type? EntityType { get; init; }

    /// <summary>
    /// Metadata shared between handlers.
    /// Use extension methods WithMetadata and GetMetadata for typed access.
    /// </summary>
    public ImmutableDictionary<string, object> Metadata { get; init; }
        = ImmutableDictionary<string, object>.Empty;

    /// <summary>
    /// The resolved query (populated by ResolverHandler).
    /// </summary>
    public ResolvedFirestoreQuery? ResolvedQuery { get; init; }
}
