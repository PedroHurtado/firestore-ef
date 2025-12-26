using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Implementa IAsyncEnumerable e IEnumerable para ejecutar queries de Firestore.
    /// Delega toda la ejecución, deserialización y carga de navegaciones al Executor.
    /// El Enumerable solo itera las entidades ya procesadas.
    /// </summary>
    public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T> where T : class
    {
        private readonly QueryContext _queryContext;
        private readonly FirestoreQueryExpression _queryExpression;
        private readonly DbContext _dbContext;
        private readonly bool _isTracking;
        private readonly IFirestoreQueryExecutor _executor;

        public FirestoreQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            DbContext dbContext,
            bool isTracking,
            IFirestoreQueryExecutor executor)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _isTracking = isTracking;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        /// <summary>
        /// Synchronous enumeration for lazy loading support.
        /// Blocks on async calls - use async version when possible.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return new SyncEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Synchronous enumerator that blocks on async initialization.
        /// Required for EF Core's lazy loading which uses synchronous queries.
        /// </summary>
        private sealed class SyncEnumerator : IEnumerator<T>
        {
            private readonly FirestoreQueryingEnumerable<T> _enumerable;
            private IAsyncEnumerator<T>? _asyncEnumerator;
            private bool _initialized;

            public SyncEnumerator(FirestoreQueryingEnumerable<T> enumerable)
            {
                _enumerable = enumerable;
            }

            public T Current { get; private set; } = default!;
            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                if (!_initialized)
                {
                    // El Executor maneja tanto queries normales como queries por ID
                    _asyncEnumerator = _enumerable._executor.ExecuteQueryAsync<T>(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _enumerable._dbContext,
                        _enumerable._isTracking,
                        CancellationToken.None).GetAsyncEnumerator(CancellationToken.None);
                    _initialized = true;
                }

                // Block on async - required for lazy loading
                var hasNext = _asyncEnumerator!.MoveNextAsync().AsTask().GetAwaiter().GetResult();
                if (hasNext)
                {
                    Current = _asyncEnumerator.Current;
                    return true;
                }

                return false;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
                _asyncEnumerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly FirestoreQueryingEnumerable<T> _enumerable;
            private readonly CancellationToken _cancellationToken;
            private IAsyncEnumerator<T>? _innerEnumerator;

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
                if (_innerEnumerator == null)
                {
                    // El Executor maneja tanto queries normales como queries por ID
                    // y retorna entidades ya deserializadas con navegaciones cargadas
                    _innerEnumerator = _enumerable._executor.ExecuteQueryAsync<T>(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _enumerable._dbContext,
                        _enumerable._isTracking,
                        _cancellationToken).GetAsyncEnumerator(_cancellationToken);
                }

                if (await _innerEnumerator.MoveNextAsync())
                {
                    Current = _innerEnumerator.Current;
                    return true;
                }

                return false;
            }

            public async ValueTask DisposeAsync()
            {
                if (_innerEnumerator != null)
                {
                    await _innerEnumerator.DisposeAsync();
                }
            }
        }
    }
}
