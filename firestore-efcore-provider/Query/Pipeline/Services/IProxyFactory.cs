using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Abstraction for creating lazy-loading proxies.
/// Wraps the EF Core Proxies IProxyFactory to avoid direct dependency on the optional package.
/// </summary>
public interface IProxyFactory
{
    /// <summary>
    /// Creates a lazy-loading proxy for an entity.
    /// </summary>
    /// <param name="entityType">The entity type metadata.</param>
    /// <param name="entity">The entity instance to wrap.</param>
    /// <returns>A proxy that wraps the entity with lazy-loading capabilities.</returns>
    object CreateLazyLoadingProxy(IEntityType entityType, object entity);
}
