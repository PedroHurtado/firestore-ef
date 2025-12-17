using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Firestore.EntityFrameworkCore.Infrastructure;
using Google.Cloud.Firestore;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Implementa IAsyncEnumerable para ejecutar agregaciones de Firestore.
    /// Retorna un solo valor del tipo especificado (int para Count, bool para Any, etc).
    /// </summary>
    public class FirestoreAggregationQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
    {
        private readonly QueryContext _queryContext;
        private readonly FirestoreQueryExpression _queryExpression;
        private readonly Type _contextType;

        public FirestoreAggregationQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            Type contextType)
        {
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _contextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
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
            private readonly FirestoreAggregationQueryingEnumerable<T> _enumerable;
            private bool _consumed;
            private T _current = default!;

            public SyncEnumerator(FirestoreAggregationQueryingEnumerable<T> enumerable)
            {
                _enumerable = enumerable;
            }

            public T Current => _current;
            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                if (_consumed)
                    return false;

                _current = ExecuteAggregation().GetAwaiter().GetResult();
                _consumed = true;
                return true;
            }

            private async Task<T> ExecuteAggregation()
            {
                var dbContext = _enumerable._queryContext.Context;
                var serviceProvider = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)dbContext).Instance;

                var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;
                var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory))!;

                var executorLogger = loggerFactory.CreateLogger<FirestoreQueryExecutor>();
                var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

                return await executor.ExecuteAggregationAsync<T>(
                    _enumerable._queryExpression,
                    _enumerable._queryContext,
                    CancellationToken.None);
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly FirestoreAggregationQueryingEnumerable<T> _enumerable;
            private readonly CancellationToken _cancellationToken;
            private bool _consumed;
            private T _current = default!;

            public AsyncEnumerator(
                FirestoreAggregationQueryingEnumerable<T> enumerable,
                CancellationToken cancellationToken)
            {
                _enumerable = enumerable;
                _cancellationToken = cancellationToken;
            }

            public T Current => _current;

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_consumed)
                    return false;

                _current = await ExecuteAggregationAsync();
                _consumed = true;
                return true;
            }

            private async Task<T> ExecuteAggregationAsync()
            {
                var dbContext = _enumerable._queryContext.Context;
                var serviceProvider = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)dbContext).Instance;

                var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;
                var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory))!;

                var executorLogger = loggerFactory.CreateLogger<FirestoreQueryExecutor>();
                var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

                return await executor.ExecuteAggregationAsync<T>(
                    _enumerable._queryExpression,
                    _enumerable._queryContext,
                    _cancellationToken);
            }

            public ValueTask DisposeAsync() => default;
        }
    }
}
