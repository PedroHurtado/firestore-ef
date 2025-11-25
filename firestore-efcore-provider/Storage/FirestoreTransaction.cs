using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreTransaction(
        FirestoreTransactionManager transactionManager,
        WriteBatch batch,  // Cambiar de Transaction a WriteBatch
        IFirestoreClientWrapper firestoreClient) : IDbContextTransaction
    {
        private readonly FirestoreTransactionManager _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        private readonly WriteBatch _batch = batch ?? throw new ArgumentNullException(nameof(batch));  // Cambiar de Transaction a WriteBatch
        private readonly IFirestoreClientWrapper _firestoreClient = firestoreClient ?? throw new ArgumentNullException(nameof(firestoreClient));
        private bool _disposed;

        public Guid TransactionId { get; } = Guid.NewGuid();

        public WriteBatch NativeBatch => _batch;  // Cambiar nombre y tipo

        public void Commit()
        {
            CommitAsync().GetAwaiter().GetResult();
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FirestoreTransaction));

            try
            {
                await _batch.CommitAsync(cancellationToken);
            }
            finally
            {
                _transactionManager.ClearCurrentTransaction(this);
            }
        }

        public void Rollback()
        {
            // WriteBatch no tiene rollback - simplemente no se hace commit
            // Solo limpiamos el estado
            _transactionManager.ClearCurrentTransaction(this);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _transactionManager.ClearCurrentTransaction(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}