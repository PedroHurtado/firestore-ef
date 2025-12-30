using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Interface for query context to enable testability and SOLID D compliance.
    /// Exposes only the members needed by FirestoreAstResolver.
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
    }
}
