using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    public interface IFirestoreClientWrapper
    {
        FirestoreDb Database { get; }
        Task<DocumentSnapshot> GetDocumentAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<bool> DocumentExistsAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<QuerySnapshot> GetCollectionAsync(string collection, CancellationToken cancellationToken = default);
        Task<WriteResult> SetDocumentAsync(string collection, string documentId, Dictionary<string, object> data, CancellationToken cancellationToken = default);
        Task<WriteResult> UpdateDocumentAsync(string collection, string documentId, Dictionary<string, object> data, CancellationToken cancellationToken = default);
        Task<WriteResult> DeleteDocumentAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<QuerySnapshot> ExecuteQueryAsync(Google.Cloud.Firestore.Query query, CancellationToken cancellationToken = default);
        Task<T> RunTransactionAsync<T>(Func<Transaction, Task<T>> callback, CancellationToken cancellationToken = default);
        WriteBatch CreateBatch();
        CollectionReference GetCollection(string collection);
        DocumentReference GetDocument(string collection, string documentId);

        /// <summary>
        /// Ejecuta una query de agregación (Count, Sum, Average, Min, Max).
        /// Centraliza las operaciones de agregación para eliminar bypasses.
        /// </summary>
        Task<AggregateQuerySnapshot> ExecuteAggregateQueryAsync(AggregateQuery query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene los documentos de una subcollection.
        /// Centraliza la carga de subcollections para eliminar bypasses en el Visitor.
        /// </summary>
        Task<QuerySnapshot> GetSubCollectionAsync(DocumentReference parentDoc, string subCollectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene un documento por su referencia directa.
        /// Centraliza la resolución de referencias para eliminar bypasses en el Visitor.
        /// </summary>
        Task<DocumentSnapshot> GetDocumentByReferenceAsync(DocumentReference docRef, CancellationToken cancellationToken = default);
    }
}
