using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Infrastructure.Internal
{
    public class FirestoreClientWrapper : IFirestoreClientWrapper, IDisposable
    {
        private readonly FirestoreDb _db;
        private readonly ILogger<FirestoreClientWrapper> _logger;
        private readonly FirestoreOptionsExtension _options;
        private bool _disposed;

        public FirestoreClientWrapper(
            IDbContextOptions contextOptions,
            ILogger<FirestoreClientWrapper> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _options = contextOptions.FindExtension<FirestoreOptionsExtension>()
                ?? throw new InvalidOperationException("FirestoreOptionsExtension no encontrada.");

            _db = InitializeFirestoreDb();

            _logger.LogInformation(
                "Cliente de Firestore inicializado para proyecto: {ProjectId}",
                _options.ProjectId);
        }

        public FirestoreDb Database => _db;

        private FirestoreDb InitializeFirestoreDb()
        {
            try
            {
                var builder = new FirestoreDbBuilder
                {
                    ProjectId = _options.ProjectId,
                    DatabaseId = _options.DatabaseId ?? "(default)",
                    // Detectar automáticamente si hay un emulador configurado
                    EmulatorDetection = EmulatorDetection.EmulatorOrProduction
                };

                if (!string.IsNullOrEmpty(_options.CredentialsPath))
                {
                    if (!File.Exists(_options.CredentialsPath))
                        throw new FileNotFoundException($"Credenciales no encontradas: {_options.CredentialsPath}");

                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _options.CredentialsPath);
                }

                return builder.Build();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar Firestore");
                throw;
            }
        }

        public async Task<DocumentSnapshot> GetDocumentAsync(
            string collection, string documentId, CancellationToken cancellationToken = default)
        {
            var docRef = _db.Collection(collection).Document(documentId);
            return await docRef.GetSnapshotAsync(cancellationToken);
        }

        public async Task<bool> DocumentExistsAsync(
            string collection, string documentId, CancellationToken cancellationToken = default)
        {
            var snapshot = await GetDocumentAsync(collection, documentId, cancellationToken);
            return snapshot.Exists;
        }

        public async Task<QuerySnapshot> GetCollectionAsync(
            string collection, CancellationToken cancellationToken = default)
        {
            return await _db.Collection(collection).GetSnapshotAsync(cancellationToken);
        }

        public async Task<WriteResult> SetDocumentAsync(
            string collection, string documentId, Dictionary<string, object> data,
            CancellationToken cancellationToken = default)
        {
            var docRef = _db.Collection(collection).Document(documentId);
            return await docRef.SetAsync(data, cancellationToken: cancellationToken);
        }

        public async Task<WriteResult> UpdateDocumentAsync(
            string collection, string documentId, Dictionary<string, object> data,
            CancellationToken cancellationToken = default)
        {
            var docRef = _db.Collection(collection).Document(documentId);
            return await docRef.SetAsync(data, SetOptions.MergeAll, cancellationToken);
        }

        public async Task<WriteResult> DeleteDocumentAsync(
            string collection, string documentId, CancellationToken cancellationToken = default)
        {
            var docRef = _db.Collection(collection).Document(documentId);
            return await docRef.DeleteAsync(cancellationToken: cancellationToken);
        }

        public async Task<QuerySnapshot> ExecuteQueryAsync(
            Google.Cloud.Firestore.Query query, CancellationToken cancellationToken = default)
        {
            return await query.GetSnapshotAsync(cancellationToken);
        }

        public async Task<T> RunTransactionAsync<T>(
            Func<Transaction, Task<T>> callback, 
            CancellationToken cancellationToken = default)
        {
            return await _db.RunTransactionAsync(callback, cancellationToken: cancellationToken);
        }

        public WriteBatch CreateBatch()
        {
            return _db.StartBatch();
        }

        public CollectionReference GetCollection(string collection)
        {
            return _db.Collection(collection);
        }

        public DocumentReference GetDocument(string collection, string documentId)
        {
            return _db.Collection(collection).Document(documentId);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
