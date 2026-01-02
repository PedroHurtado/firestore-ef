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
    /// Gets the proxy type for an entity type.
    /// The proxy type is a dynamically generated class that inherits from the entity
    /// and intercepts navigation property access for lazy loading.
    /// </summary>
    /// <param name="entityType">The entity type metadata.</param>
    /// <returns>The proxy Type that should be instantiated instead of the entity type.</returns>
    Type GetProxyType(IEntityType entityType);

    /// <summary>
    /// Creates an empty proxy instance for an entity type.
    /// The proxy is created with the ILazyLoader injected for lazy loading support.
    /// Properties should be populated after creation via deserialization.
    /// </summary>
    /// <param name="entityType">The entity type metadata.</param>
    /// <returns>An empty proxy instance ready to be populated.</returns>
    object CreateProxy(IEntityType entityType);
}
