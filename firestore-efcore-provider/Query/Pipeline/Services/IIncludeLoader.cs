using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Executes resolved includes for eager loading (Include/ThenInclude).
/// Receives ResolvedInclude (already resolved by Resolver) and executes the query.
/// The IncludeLoader does NOT build AST - it uses the resolved query directly.
/// </summary>
public interface IIncludeLoader
{
    /// <summary>
    /// Executes a resolved include for an entity.
    /// The include is already resolved (filters, ordering, pagination evaluated).
    /// Only needs to parametrize with the parent entity's FK value.
    /// </summary>
    /// <param name="entity">The parent entity.</param>
    /// <param name="entityType">The entity type metadata.</param>
    /// <param name="resolvedInclude">The resolved include from ResolvedFirestoreQuery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LoadIncludeAsync(
        object entity,
        IEntityType entityType,
        ResolvedInclude resolvedInclude,
        CancellationToken cancellationToken);
}
