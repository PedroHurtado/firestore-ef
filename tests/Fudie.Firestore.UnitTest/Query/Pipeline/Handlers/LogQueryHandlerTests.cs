namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class LogQueryHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void LogQueryHandler_Implements_IQueryPipelineHandler()
    {
        typeof(LogQueryHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void LogQueryHandler_Constructor_Accepts_Logger()
    {
        var constructors = typeof(LogQueryHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ILogger<LogQueryHandler>));
    }

    #endregion

    #region Pass-Through Tests

    [Fact]
    public async Task HandleAsync_Calls_Next_And_Returns_Result()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateContext();
        var expectedResult = new PipelineResult.Empty(context);
        var nextCalled = false;

        PipelineDelegate next = (ctx, ct) =>
        {
            nextCalled = true;
            return Task.FromResult<PipelineResult>(expectedResult);
        };

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_Works_Without_ResolvedQuery()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateContext(withResolvedQuery: false);
        var expectedResult = new PipelineResult.Empty(context);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(expectedResult);

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task HandleAsync_Logs_Query_At_Debug_Level()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LogQueryHandler>>();
        mockLogger
            .Setup(l => l.IsEnabled(LogLevel.Debug))
            .Returns(true);

        var handler = new LogQueryHandler(mockLogger.Object);
        var context = CreateContext(withResolvedQuery: true);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Executing Firestore query")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Does_Not_Log_When_Debug_Disabled()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LogQueryHandler>>();
        mockLogger
            .Setup(l => l.IsEnabled(LogLevel.Debug))
            .Returns(false);

        var handler = new LogQueryHandler(mockLogger.Object);
        var context = CreateContext(withResolvedQuery: true);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    private static LogQueryHandler CreateHandler()
    {
        var logger = new Mock<ILogger<LogQueryHandler>>().Object;
        return new LogQueryHandler(logger);
    }

    private static PipelineContext CreateContext(bool withResolvedQuery = true)
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        var context = new PipelineContext
        {
            Ast = null!,
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = QueryKind.Entity
        };

        if (withResolvedQuery)
        {
            context = context with { ResolvedQuery = CreateResolvedQuery() };
        }

        return context;
    }

    private static ResolvedFirestoreQuery CreateResolvedQuery()
    {
        return new ResolvedFirestoreQuery(
            CollectionPath: "test-collection",
            EntityClrType: typeof(object),
            DocumentId: null,
            FilterResults: Array.Empty<ResolvedFilterResult>(),
            OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
            Pagination: ResolvedPaginationInfo.None,
            StartAfterCursor: null,
            Includes: Array.Empty<ResolvedInclude>(),
            AggregationType: FirestoreAggregationType.None,
            AggregationPropertyName: null,
            AggregationResultType: null,
            Projection: null,
            ReturnDefault: false,
            ReturnType: typeof(object));
    }
}
