namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class QueryPipelineMediatorTests
{
    #region Class Structure Tests

    [Fact]
    public void QueryPipelineMediator_Implements_IQueryPipelineMediator()
    {
        typeof(QueryPipelineMediator)
            .Should().Implement<IQueryPipelineMediator>();
    }

    [Fact]
    public void QueryPipelineMediator_Constructor_Accepts_Handlers()
    {
        var constructors = typeof(QueryPipelineMediator).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IEnumerable<IQueryPipelineHandler>));
    }

    #endregion

    #region Pipeline Execution Tests

    private class PassThroughHandler : IQueryPipelineHandler
    {
        public int CallOrder { get; private set; }
        private static int _callCounter;

        public Task<PipelineResult> HandleAsync(
            PipelineContext context,
            PipelineDelegate next,
            CancellationToken cancellationToken)
        {
            CallOrder = ++_callCounter;
            return next(context, cancellationToken);
        }

        public static void ResetCounter() => _callCounter = 0;
    }

    private class TerminalHandler : IQueryPipelineHandler
    {
        private readonly PipelineResult _result;

        public TerminalHandler(PipelineResult result)
        {
            _result = result;
        }

        public Task<PipelineResult> HandleAsync(
            PipelineContext context,
            PipelineDelegate next,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Executes_Handlers_In_Order()
    {
        // Arrange
        PassThroughHandler.ResetCounter();
        var handler1 = new PassThroughHandler();
        var handler2 = new PassThroughHandler();
        var handler3 = new PassThroughHandler();
        var context = CreateContext();

        var terminalResult = new PipelineResult.Empty(context);
        var terminalHandler = new TerminalHandler(terminalResult);

        var mediator = new QueryPipelineMediator(new IQueryPipelineHandler[]
        {
            handler1, handler2, handler3, terminalHandler
        });

        // Act
        await mediator.ExecuteAsync<object>(context, CancellationToken.None).ToListAsync();

        // Assert
        handler1.CallOrder.Should().Be(1);
        handler2.CallOrder.Should().Be(2);
        handler3.CallOrder.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Empty_When_Result_Is_Empty()
    {
        // Arrange
        var context = CreateContext();
        var emptyResult = new PipelineResult.Empty(context);
        var handler = new TerminalHandler(emptyResult);

        var mediator = new QueryPipelineMediator(new[] { handler });

        // Act
        var results = await mediator.ExecuteAsync<object>(context, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Yields_Items_From_Streaming_Result()
    {
        // Arrange
        var context = CreateContext();
        var items = new object[] { "item1", "item2", "item3" };
        var streamingResult = new PipelineResult.Streaming(
            items.ToAsyncEnumerable(),
            context);
        var handler = new TerminalHandler(streamingResult);

        var mediator = new QueryPipelineMediator(new[] { handler });

        // Act
        var results = await mediator.ExecuteAsync<object>(context, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task ExecuteAsync_Yields_Items_From_Materialized_Result()
    {
        // Arrange
        var context = CreateContext();
        var items = new object[] { "item1", "item2" };
        var materializedResult = new PipelineResult.Materialized(items, context);
        var handler = new TerminalHandler(materializedResult);

        var mediator = new QueryPipelineMediator(new[] { handler });

        // Act
        var results = await mediator.ExecuteAsync<object>(context, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task ExecuteAsync_Yields_Single_Value_From_Scalar_Result()
    {
        // Arrange
        var context = CreateContext();
        var scalarValue = 42;
        var scalarResult = new PipelineResult.Scalar(scalarValue, context);
        var handler = new TerminalHandler(scalarResult);

        var mediator = new QueryPipelineMediator(new[] { handler });

        // Act
        var results = await mediator.ExecuteAsync<int>(context, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_Respects_Cancellation()
    {
        // Arrange
        var context = CreateContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var items = AsyncEnumerable.Create<object>(async (yield, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            await yield.ReturnAsync("item1");
        });

        var streamingResult = new PipelineResult.Streaming(items, context);
        var handler = new TerminalHandler(streamingResult);

        var mediator = new QueryPipelineMediator(new[] { handler });

        // Act & Assert
        var action = async () =>
            await mediator.ExecuteAsync<object>(context, cts.Token).ToListAsync();

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Empty Handlers Tests

    [Fact]
    public async Task ExecuteAsync_Returns_Empty_When_No_Handlers()
    {
        // Arrange
        var context = CreateContext();
        var mediator = new QueryPipelineMediator(Array.Empty<IQueryPipelineHandler>());

        // Act
        var results = await mediator.ExecuteAsync<object>(context, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    private static PipelineContext CreateContext()
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        return new PipelineContext
        {
            Ast = null!,
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = QueryKind.Entity
        };
    }
}

internal static class AsyncEnumerable
{
    public static IAsyncEnumerable<T> Create<T>(
        Func<IAsyncYielder<T>, CancellationToken, Task> producer)
    {
        return new AsyncEnumerableImpl<T>(producer);
    }

    private class AsyncEnumerableImpl<T> : IAsyncEnumerable<T>
    {
        private readonly Func<IAsyncYielder<T>, CancellationToken, Task> _producer;

        public AsyncEnumerableImpl(Func<IAsyncYielder<T>, CancellationToken, Task> producer)
        {
            _producer = producer;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumeratorImpl<T>(_producer, cancellationToken);
        }
    }

    private class AsyncEnumeratorImpl<T> : IAsyncEnumerator<T>
    {
        private readonly Func<IAsyncYielder<T>, CancellationToken, Task> _producer;
        private readonly CancellationToken _cancellationToken;
        private readonly Queue<T> _items = new();
        private bool _completed;
        private Task? _producerTask;

        public AsyncEnumeratorImpl(
            Func<IAsyncYielder<T>, CancellationToken, Task> producer,
            CancellationToken cancellationToken)
        {
            _producer = producer;
            _cancellationToken = cancellationToken;
        }

        public T Current { get; private set; } = default!;

        public async ValueTask<bool> MoveNextAsync()
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (_producerTask == null)
            {
                var yielder = new Yielder(_items);
                _producerTask = _producer(yielder, _cancellationToken);
            }

            if (_items.Count > 0)
            {
                Current = _items.Dequeue();
                return true;
            }

            if (!_completed)
            {
                await _producerTask;
                _completed = true;
            }

            if (_items.Count > 0)
            {
                Current = _items.Dequeue();
                return true;
            }

            return false;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private class Yielder : IAsyncYielder<T>
        {
            private readonly Queue<T> _queue;

            public Yielder(Queue<T> queue) => _queue = queue;

            public Task ReturnAsync(T value)
            {
                _queue.Enqueue(value);
                return Task.CompletedTask;
            }
        }
    }
}

internal interface IAsyncYielder<in T>
{
    Task ReturnAsync(T value);
}
