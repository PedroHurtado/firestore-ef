using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Firestore-specific query context that implements IFirestoreQueryContext
    /// for testability and dependency injection.
    /// </summary>
    public class FirestoreQueryContext(QueryContextDependencies dependencies)
        : QueryContext(dependencies), IFirestoreQueryContext
    {
        /// <inheritdoc />
        IReadOnlyDictionary<string, object?> IFirestoreQueryContext.ParameterValues => ParameterValues;

        /// <inheritdoc />
        IModel IFirestoreQueryContext.Model => Context.Model;

        /// <inheritdoc />
        IStateManager IFirestoreQueryContext.StateManager => Dependencies.StateManager;

        /// <inheritdoc />
        QueryContext IFirestoreQueryContext.AsQueryContext => this;
    }
}
