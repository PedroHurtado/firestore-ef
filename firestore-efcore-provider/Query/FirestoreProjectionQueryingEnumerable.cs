using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;
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
            private IEnumerator<DocumentSnapshot>? _enumerator;
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
                    InitializeEnumerator();
                    _initialized = true;
                }

                if (_enumerator!.MoveNext())
                {
                    var document = _enumerator.Current;
                    Current = _enumerable._shaper(_enumerable._queryContext, document, _enumerable._isTracking);
                    return true;
                }

                return false;
            }

            private void InitializeEnumerator()
            {
                var executor = _enumerable._executor;

                if (_enumerable._queryExpression.IsIdOnlyQuery)
                {
                    var documentSnapshot = executor.ExecuteIdQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        CancellationToken.None).GetAwaiter().GetResult();

                    var documents = new List<DocumentSnapshot>();
                    if (documentSnapshot != null && documentSnapshot.Exists)
                    {
                        documents.Add(documentSnapshot);
                    }
                    _enumerator = documents.GetEnumerator();
                }
                else
                {
#pragma warning disable CS0618 // Obsolete - usamos el método antiguo para proyecciones
                    var snapshot = executor.ExecuteQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore CS0618

                    IEnumerable<DocumentSnapshot> documents = snapshot.Documents;

                    if (_enumerable._queryExpression.Skip.HasValue && _enumerable._queryExpression.Skip.Value > 0)
                    {
                        documents = documents.Skip(_enumerable._queryExpression.Skip.Value);
                    }
                    else if (_enumerable._queryExpression.SkipExpression != null)
                    {
                        var skipValue = executor.EvaluateIntExpression(
                            _enumerable._queryExpression.SkipExpression,
                            _enumerable._queryContext);
                        if (skipValue > 0)
                        {
                            documents = documents.Skip(skipValue);
                        }
                    }

                    _enumerator = documents.GetEnumerator();
                }
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() => _enumerator?.Dispose();
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly FirestoreProjectionQueryingEnumerable<T> _enumerable;
            private readonly CancellationToken _cancellationToken;
            private IEnumerator<DocumentSnapshot>? _enumerator;

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
                var executor = _enumerable._executor;

                if (_enumerable._queryExpression.IsIdOnlyQuery)
                {
                    var documentSnapshot = await executor.ExecuteIdQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _cancellationToken);

                    var documents = new List<DocumentSnapshot>();
                    if (documentSnapshot != null && documentSnapshot.Exists)
                    {
                        documents.Add(documentSnapshot);
                    }

                    _enumerator = documents.GetEnumerator();
                }
                else
                {
#pragma warning disable CS0618 // Obsolete - usamos el método antiguo para proyecciones
                    var snapshot = await executor.ExecuteQueryAsync(
                        _enumerable._queryExpression,
                        _enumerable._queryContext,
                        _cancellationToken);
#pragma warning restore CS0618

                    IEnumerable<DocumentSnapshot> documents = snapshot.Documents;

                    if (_enumerable._queryExpression.Skip.HasValue && _enumerable._queryExpression.Skip.Value > 0)
                    {
                        documents = documents.Skip(_enumerable._queryExpression.Skip.Value);
                    }
                    else if (_enumerable._queryExpression.SkipExpression != null)
                    {
                        var skipValue = executor.EvaluateIntExpression(
                            _enumerable._queryExpression.SkipExpression,
                            _enumerable._queryContext);
                        if (skipValue > 0)
                        {
                            documents = documents.Skip(skipValue);
                        }
                    }

                    _enumerator = documents.GetEnumerator();
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
