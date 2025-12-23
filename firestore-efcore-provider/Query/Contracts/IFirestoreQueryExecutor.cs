using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Query;
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
        /// Ejecuta una FirestoreQueryExpression y retorna los documentos resultantes.
        /// </summary>
        Task<QuerySnapshot> ExecuteQueryAsync(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ejecuta una query por ID usando GetDocumentAsync.
        /// </summary>
        Task<DocumentSnapshot?> ExecuteIdQueryAsync(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ejecuta una agregación (Count, Sum, Average, Min, Max, Any).
        /// </summary>
        Task<T> ExecuteAggregationAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evalúa una expresión entera en runtime usando el QueryContext.
        /// Usado para Limit (Take) y Skip cuando son expresiones parametrizadas.
        /// </summary>
        int EvaluateIntExpression(
            System.Linq.Expressions.Expression expression,
            QueryContext queryContext);

        /// <summary>
        /// Obtiene los documentos de una subcollection.
        /// </summary>
        Task<QuerySnapshot> GetSubCollectionAsync(
            string parentDocPath,
            string subCollectionName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene un documento por su referencia.
        /// </summary>
        Task<DocumentSnapshot> GetDocumentByReferenceAsync(
            string docPath,
            CancellationToken cancellationToken = default);
    }
}