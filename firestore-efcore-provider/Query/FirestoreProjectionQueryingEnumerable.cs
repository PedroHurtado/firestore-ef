using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Implementa IAsyncEnumerable e IEnumerable para ejecutar queries de proyección de Firestore.
    /// Usa un shaper para transformar DocumentSnapshot a tipos de proyección (DTOs, tipos anónimos).
    /// Este enumerable se usa para queries con Select() que proyectan a tipos que no son entidades.
    /// </summary>
    public class FirestoreProjectionQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
    {
        private readonly QueryContext _queryContext;
        private readonly FirestoreQueryExpression _queryExpression;
        private readonly Func<QueryContext, DocumentSnapshot, bool, T> _shaper;
        private readonly Type _entityType;
        private readonly bool _isTracking;
        private readonly IFirestoreQueryExecutor _executor;

        public FirestoreProjectionQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            Func<QueryContext, DocumentSnapshot, bool, T> shaper,
            Type entityType,
            bool isTracking,
            IFirestoreQueryExecutor executor)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _shaper = shaper ?? throw new ArgumentNullException(nameof(shaper));
            _entityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            _isTracking = isTracking;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SyncEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class SyncEnumerator : IEnumerator<T>
        {
            private readonly FirestoreProjectionQueryingEnumerable<T> _enumerable;
            private IAsyncEnumerator<DocumentSnapshot>? _asyncEnumerator;
            private bool _initialized;

            public SyncEnumerator(FirestoreProjectionQueryingEnumerable<T> enumerable)
            {
                _enumerable = enumerable;
            }

            public T Current { get; private set; } = default!;
            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                if (!_initialized)
                {
                    _asyncEnumerator = _enumerable._executor.ExecuteQueryForDocumentsAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        CancellationToken.None).GetAsyncEnumerator(CancellationToken.None);
                    _initialized = true;
                }

                // Block on async - required for synchronous enumeration
                var hasNext = _asyncEnumerator!.MoveNextAsync().AsTask().GetAwaiter().GetResult();
                if (hasNext)
                {
                    var document = _asyncEnumerator.Current;
                    Current = _enumerable._shaper(_enumerable._queryContext, document, _enumerable._isTracking);
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
            private readonly FirestoreProjectionQueryingEnumerable<T> _enumerable;
            private readonly CancellationToken _cancellationToken;
            private IAsyncEnumerator<DocumentSnapshot>? _innerEnumerator;

            public AsyncEnumerator(
                FirestoreProjectionQueryingEnumerable<T> enumerable,
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
                    _innerEnumerator = _enumerable._executor.ExecuteQueryForDocumentsAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _cancellationToken).GetAsyncEnumerator(_cancellationToken);
                }

                if (await _innerEnumerator.MoveNextAsync())
                {
                    var document = _innerEnumerator.Current;
                    Current = _enumerable._shaper(_enumerable._queryContext, document, _enumerable._isTracking);
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
