using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Executes resolved includes for eager loading (Include/ThenInclude).
/// Receives ResolvedInclude (already resolved by Resolver) and executes the query.
/// The IncludeLoader does NOT build AST - it uses the resolved query directly.
/// Dependencies (mediator, queryContext) are passed at runtime to avoid circular DI.
/// </summary>
public interface IIncludeLoader
{
    /// <summary>
    /// Executes a resolved include for an entity.
    /// The include is already resolved (filters, ordering, pagination evaluated).
    /// NestedIncludes are contained within ResolvedInclude for ThenInclude support.
    /// </summary>
    /// <param name="entity">The parent entity.</param>
    /// <param name="entityType">The entity type metadata.</param>
    /// <param name="resolvedInclude">The resolved include from ResolvedFirestoreQuery.</param>
    /// <param name="mediator">The pipeline mediator for executing sub-queries.</param>
    /// <param name="queryContext">The query context for the current query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LoadIncludeAsync(
        object entity,
        IEntityType entityType,
        ResolvedInclude resolvedInclude,
        IQueryPipelineMediator mediator,
        IFirestoreQueryContext queryContext,
        CancellationToken cancellationToken);
}
