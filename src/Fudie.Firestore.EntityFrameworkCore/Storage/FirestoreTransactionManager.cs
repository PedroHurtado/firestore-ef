using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreTransactionManager(IFirestoreClientWrapper firestoreClient) : IDbContextTransactionManager
    {
        private readonly IFirestoreClientWrapper _firestoreClient = firestoreClient;
        private FirestoreTransaction? _currentTransaction;

        public IDbContextTransaction? CurrentTransaction => _currentTransaction;

        public IDbContextTransaction BeginTransaction()
        {
            return BeginTransactionAsync().GetAwaiter().GetResult();
        }

        // Quitar async y usar Task.FromResult para evitar el warning
        public Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
                throw new InvalidOperationException("Ya existe una transacción activa.");

            // Firestore usa WriteBatch para operaciones atómicas
            var batch = _firestoreClient.CreateBatch();
            _currentTransaction = new FirestoreTransaction(this, batch, _firestoreClient);

            return Task.FromResult<IDbContextTransaction>(_currentTransaction);
        }

        public void CommitTransaction()
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No hay transacción activa.");

            try
            {
                _currentTransaction.Commit();
            }
            finally
            {
                _currentTransaction = null;
            }
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No hay transacción activa.");

            try
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            finally
            {
                _currentTransaction = null;
            }
        }

        public void RollbackTransaction()
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No hay transacción activa.");

            try
            {
                _currentTransaction.Rollback();
            }
            finally
            {
                _currentTransaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No hay transacción activa.");

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                _currentTransaction = null;
            }
        }

        public void ResetState()
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            ResetState();
            return Task.CompletedTask;
        }

        internal void ClearCurrentTransaction(FirestoreTransaction transaction)
        {
            if (_currentTransaction == transaction)
                _currentTransaction = null;
        }
    }
}