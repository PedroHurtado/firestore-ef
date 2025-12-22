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
    }
}