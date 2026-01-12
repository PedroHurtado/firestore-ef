using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Unified IAsyncEnumerable implementation that executes queries through the pipeline.
/// Handles both entity queries and aggregation queries through IQueryPipelineMediator.
/// </summary>
/// <remarks>
/// Unlike the legacy FirestoreQueryingEnumerable, this class:
/// - Uses the pipeline pattern instead of IFirestoreQueryExecutor
/// - Has no generic constraint (supports value types for aggregations)
/// - Handles all query kinds: Entity, Aggregation, Projection, Predicate
/// </remarks>
public class FirestorePipelineQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
{
    private readonly IQueryPipelineMediator _mediator;
    private readonly PipelineContext _context;

    public FirestorePipelineQueryingEnumerable(
        IQueryPipelineMediator mediator,
        PipelineContext context)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
        private readonly FirestorePipelineQueryingEnumerable<T> _enumerable;
        private IAsyncEnumerator<T>? _asyncEnumerator;
        private bool _initialized;

        public SyncEnumerator(FirestorePipelineQueryingEnumerable<T> enumerable)
        {
            _enumerable = enumerable;
        }

        public T Current { get; private set; } = default!;
        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_initialized)
            {
                _asyncEnumerator = _enumerable._mediator.ExecuteAsync<T>(
                    _enumerable._context,
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
        private readonly FirestorePipelineQueryingEnumerable<T> _enumerable;
        private readonly CancellationToken _cancellationToken;
        private IAsyncEnumerator<T>? _innerEnumerator;

        public AsyncEnumerator(
            FirestorePipelineQueryingEnumerable<T> enumerable,
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
                _innerEnumerator = _enumerable._mediator.ExecuteAsync<T>(
                    _enumerable._context,
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
