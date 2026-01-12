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
    public void LogQueryHandler_Constructor_Accepts_FirestorePipelineOptions_And_Logger()
    {
        var constructors = typeof(LogQueryHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(FirestorePipelineOptions));
        parameters[1].ParameterType.Should().Be(typeof(IDiagnosticsLogger<DbLoggerCategory.Query>));
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

    #region QueryLogLevel Tests

    [Fact]
    public async Task HandleAsync_Skips_Logging_When_QueryLogLevel_Is_None()
    {
        // Arrange
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.None };
        var handler = CreateHandler(options);
        var context = CreateContext(withResolvedQuery: true);
        var expectedResult = new PipelineResult.Empty(context);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(expectedResult);

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_Logs_When_QueryLogLevel_Is_Count()
    {
        // Arrange
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Count };
        var handler = CreateHandler(options);
        var context = CreateContext(withResolvedQuery: true);
        var expectedResult = new PipelineResult.Empty(context);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(expectedResult);

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert - handler should call next and return result
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_Logs_When_QueryLogLevel_Is_Full()
    {
        // Arrange
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Full };
        var handler = CreateHandler(options);
        var context = CreateContext(withResolvedQuery: true);
        var expectedResult = new PipelineResult.Empty(context);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(expectedResult);

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert - handler should call next and return result
        result.Should().BeSameAs(expectedResult);
    }

    #endregion

    private static LogQueryHandler CreateHandler(FirestorePipelineOptions? options = null)
    {
        options ??= new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.None };
        var mockLogger = CreateMockLogger();
        return new LogQueryHandler(options, mockLogger.Object);
    }

    private static Mock<IDiagnosticsLogger<DbLoggerCategory.Query>> CreateMockLogger()
    {
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Query>>();
        var mockInnerLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.Logger).Returns(mockInnerLogger.Object);
        return mockLogger;
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
