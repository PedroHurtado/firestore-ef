using Microsoft.EntityFrameworkCore;
using Google.Cloud.Firestore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Fudie.Firestore.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.EntityFrameworkCore.Extensions
{
    public static class FirestoreTransactionExtensions
    {
        public static async Task<T> ExecuteInTransactionAsync<T>(
            this DbContext context,
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public static async Task ExecuteInTransactionAsync(
            this DbContext context,
            Func<Task> operation,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // Cambiar a WriteBatch en lugar de Transaction
        public static WriteBatch? GetFirestoreBatch(this DbContext context)
        {
            var transaction = context.Database.CurrentTransaction;
            
            if (transaction is FirestoreTransaction firestoreTransaction)
            {
                return firestoreTransaction.NativeBatch;  // Cambiar a NativeBatch
            }

            return null;
        }
    }
}