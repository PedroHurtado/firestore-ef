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
        private readonly Func<QueryContext, DocumentSnapshot, bool, T> _shaper;
        private readonly Type _contextType;
        private readonly bool _isTracking;

        public FirestoreQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            Func<QueryContext, DocumentSnapshot, bool, T> shaper,
            Type contextType,
            bool isTracking)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _shaper = shaper ?? throw new ArgumentNullException(nameof(shaper));
            _contextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
            _isTracking = isTracking;
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
                    Current = _enumerable._shaper(_enumerable._queryContext, document, _enumerable._isTracking);
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

                // 🔥 MANEJO ESPECIAL PARA QUERIES POR ID
                // Las queries por ID usan GetDocumentAsync porque el ID es metadata del documento
                if (_enumerable._queryExpression.IsIdOnlyQuery)
                {
                    // Ejecutar query de ID que retorna un solo DocumentSnapshot
                    var documentSnapshot = await executor.ExecuteIdQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _cancellationToken);

                    // Crear lista con el documento (si existe) o lista vacía (si no existe)
                    var documents = new List<DocumentSnapshot>();
                    if (documentSnapshot != null && documentSnapshot.Exists)
                    {
                        documents.Add(documentSnapshot);
                    }

                    // Inicializar enumerador con la lista
                    _enumerator = documents.GetEnumerator();
                }
                else
                {
                    // Query normal - ejecutar y obtener QuerySnapshot
                    var snapshot = await executor.ExecuteQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _cancellationToken);

                    // Inicializar enumerador con los documentos
                    _enumerator = snapshot.Documents.GetEnumerator();
                }
            }

            public ValueTask DisposeAsync()
            {
                _enumerator?.Dispose();
                return default;
            }
        }
    }
}
