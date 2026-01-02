namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class QueryPipelineHandlerBaseTests
{
    #region Class Structure Tests

    [Fact]
    public void QueryPipelineHandlerBase_Is_Abstract_Class()
    {
        typeof(QueryPipelineHandlerBase).IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void QueryPipelineHandlerBase_Implements_IQueryPipelineHandler()
    {
        typeof(QueryPipelineHandlerBase)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void QueryPipelineHandlerBase_Has_Protected_ApplicableKinds_Property()
    {
        var property = typeof(QueryPipelineHandlerBase)
            .GetProperty("ApplicableKinds", BindingFlags.NonPublic | BindingFlags.Instance);

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(QueryKind[]));
    }

    [Fact]
    public void QueryPipelineHandlerBase_Has_Protected_Abstract_HandleCoreAsync_Method()
    {
        var method = typeof(QueryPipelineHandlerBase)
            .GetMethod("HandleCoreAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void HandleCoreAsync_Returns_Task_Of_PipelineResult()
    {
        var method = typeof(QueryPipelineHandlerBase)
            .GetMethod("HandleCoreAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        method!.ReturnType.Should().Be(typeof(Task<PipelineResult>));
    }

    [Fact]
    public void HandleCoreAsync_Has_Correct_Parameters()
    {
        var method = typeof(QueryPipelineHandlerBase)
            .GetMethod("HandleCoreAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
        parameters[1].ParameterType.Should().Be(typeof(PipelineDelegate));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region Skip Logic Tests

    private class TestHandler : QueryPipelineHandlerBase
    {
        private readonly QueryKind[] _applicableKinds;
        public bool HandleCoreWasCalled { get; private set; }

        public TestHandler(params QueryKind[] applicableKinds)
        {
            _applicableKinds = applicableKinds;
        }

        protected override QueryKind[] ApplicableKinds => _applicableKinds;

        protected override Task<PipelineResult> HandleCoreAsync(
            PipelineContext context,
            PipelineDelegate next,
            CancellationToken ct)
        {
            HandleCoreWasCalled = true;
            return Task.FromResult<PipelineResult>(new PipelineResult.Empty(context));
        }
    }

    [Fact]
    public async Task HandleAsync_Calls_HandleCoreAsync_When_Kind_Is_Applicable()
    {
        // Arrange
        var handler = new TestHandler(QueryKind.Entity);
        var context = CreateContext(QueryKind.Entity);
        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        handler.HandleCoreWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Skips_To_Next_When_Kind_Is_Not_Applicable()
    {
        // Arrange
        var handler = new TestHandler(QueryKind.Entity);
        var context = CreateContext(QueryKind.Aggregation);
        var nextCalled = false;
        PipelineDelegate next = (ctx, ct) =>
        {
            nextCalled = true;
            return Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));
        };

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        handler.HandleCoreWasCalled.Should().BeFalse();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Handles_Multiple_Applicable_Kinds()
    {
        // Arrange
        var handler = new TestHandler(QueryKind.Entity, QueryKind.Projection);
        var context = CreateContext(QueryKind.Projection);
        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        handler.HandleCoreWasCalled.Should().BeTrue();
    }

    #endregion

    private static PipelineContext CreateContext(QueryKind kind)
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        return new PipelineContext
        {
            Ast = null!, // Will be mocked when needed
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = kind
        };
    }
}
