using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Interface for query context to enable testability and SOLID D compliance.
    /// Exposes members needed by pipeline handlers.
    /// </summary>
    public interface IFirestoreQueryContext
    {
        /// <summary>
        /// Dictionary of parameter values captured from the LINQ query.
        /// Used for evaluating parameterized expressions at runtime.
        /// </summary>
        IReadOnlyDictionary<string, object?> ParameterValues { get; }

        /// <summary>
        /// The model metadata for resolving navigations and entity types.
        /// </summary>
        IModel Model { get; }

        /// <summary>
        /// The state manager for entity tracking.
        /// Accessed at runtime to avoid circular DI dependencies.
        /// </summary>
        IStateManager StateManager { get; }

        /// <summary>
        /// Returns the underlying QueryContext for expression evaluation.
        /// Used when expressions contain direct references to QueryContext type.
        /// </summary>
        QueryContext AsQueryContext { get; }
    }
}
