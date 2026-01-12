using Microsoft.EntityFrameworkCore.Storage;
using System;
using Grpc.Core;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreExecutionStrategy : ExecutionStrategy
    {
        // Error 3 y 4: Usar 'new' para ocultar intencionalmente los miembros heredados
        private new const int DefaultMaxRetryCount = 3;
        private new static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

        public FirestoreExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, DefaultMaxRetryCount, DefaultMaxDelay)
        {
        }

        protected override bool ShouldRetryOn(Exception exception)
        {
            // Error 1: Usar RpcException en lugar de FirestoreException
            if (exception is RpcException rpcEx)
            {
                return rpcEx.StatusCode == StatusCode.Unavailable ||
                       rpcEx.StatusCode == StatusCode.DeadlineExceeded ||
                       rpcEx.StatusCode == StatusCode.ResourceExhausted;
            }

            return exception is System.Net.Http.HttpRequestException ||
                   exception is System.IO.IOException;
        }

        // Error 2: GetNextDelay ya no necesita CurrentRetryCount, usar ExceptionsEncountered.Count
        protected override TimeSpan? GetNextDelay(Exception lastException)
        {
            var baseDelay = TimeSpan.FromMilliseconds(100);
            var retryCount = ExceptionsEncountered.Count;  // Usar esto en lugar de CurrentRetryCount
            var exponentialDelay = TimeSpan.FromMilliseconds(
                baseDelay.TotalMilliseconds * Math.Pow(2, retryCount));

            return exponentialDelay > MaxRetryDelay ? MaxRetryDelay : exponentialDelay;
        }
    }
}
