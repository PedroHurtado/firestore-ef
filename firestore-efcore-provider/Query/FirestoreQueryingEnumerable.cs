using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Firestore.EntityFrameworkCore.Infrastructure;

using Google.Cloud.Firestore;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Implementa IAsyncEnumerable para ejecutar queries de Firestore de forma asíncrona.
    /// Basado en el patrón de CosmosDB Provider.
    /// </summary>
    public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly QueryContext _queryContext;
        private readonly FirestoreQueryExpression _queryExpression;
        private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
        private readonly Type _contextType;

        public FirestoreQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            Func<QueryContext, DocumentSnapshot, T> shaper,
            Type contextType)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _shaper = shaper ?? throw new ArgumentNullException(nameof(shaper));
            _contextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly FirestoreQueryingEnumerable<T> _enumerable;
            private readonly CancellationToken _cancellationToken;
            private IEnumerator<DocumentSnapshot>? _enumerator;

            public AsyncEnumerator(
                FirestoreQueryingEnumerable<T> enumerable,
                CancellationToken cancellationToken)
            {
                _enumerable = enumerable;
                _cancellationToken = cancellationToken;
            }

            public T Current { get; private set; } = default!;

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_enumerator == null)
                {
                    await InitializeEnumeratorAsync();
                }

                if (_enumerator!.MoveNext())
                {
                    var document = _enumerator.Current;
                    Current = _enumerable._shaper(_enumerable._queryContext, document);
                    return true;
                }

                return false;
            }

            private async Task InitializeEnumeratorAsync()
            {
                var dbContext = _enumerable._queryContext.Context;
                var serviceProvider = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)dbContext).Instance;

                // Obtener dependencias
                var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;
                var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory))!;

                // Crear executor
                var executorLogger = loggerFactory.CreateLogger<FirestoreQueryExecutor>();
                var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

                // Ejecutar query
                var snapshot = await executor.ExecuteQueryAsync(
                        _enumerable._queryExpression,      // 1. queryExpression
                        _enumerable._queryContext,         // 2. queryContext
                        _cancellationToken);

                // Inicializar enumerador con los documentos
                _enumerator = snapshot.Documents.GetEnumerator();
            }

            public ValueTask DisposeAsync()
            {
                _enumerator?.Dispose();
                return default;
            }
        }
    }
}
