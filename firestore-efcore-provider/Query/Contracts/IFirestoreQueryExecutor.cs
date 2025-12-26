using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Interfaz para el ejecutor de queries de Firestore.
    /// Permite mockear y testear el ejecutor de queries.
    /// </summary>
    public interface IFirestoreQueryExecutor
    {
        /// <summary>
        /// Ejecuta una query y retorna entidades deserializadas con navegaciones cargadas.
        /// </summary>
        IAsyncEnumerable<T> ExecuteQueryAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            DbContext dbContext,
            bool isTracking,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Ejecuta una query por ID y retorna la entidad deserializada con navegaciones cargadas.
        /// </summary>
        Task<T?> ExecuteIdQueryAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            DbContext dbContext,
            bool isTracking,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Ejecuta una agregación (Count, Sum, Average, Min, Max, Any).
        /// </summary>
        Task<T> ExecuteAggregationAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            CancellationToken cancellationToken = default);
    }
}
