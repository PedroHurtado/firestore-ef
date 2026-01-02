using Firestore.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

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
    public void LogQueryHandler_Constructor_Accepts_DiagnosticsLogger()
    {
        var constructors = typeof(LogQueryHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IDiagnosticsLogger<DbLoggerCategory.Query>));
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
    public async Task HandleAsync_Logs_When_Query_Present()
    {
        // Arrange
        var mockLogger = CreateMockDiagnosticsLogger(shouldLog: true);
        var handler = new LogQueryHandler(mockLogger.Object);
        var context = CreateContext(withResolvedQuery: true);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert - verify ShouldLog was called (logging was attempted)
        mockLogger.Verify(l => l.ShouldLog(It.IsAny<EventDefinitionBase>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleAsync_Does_Not_Log_When_Query_Absent()
    {
        // Arrange
        var mockLogger = CreateMockDiagnosticsLogger(shouldLog: true);
        var handler = new LogQueryHandler(mockLogger.Object);
        var context = CreateContext(withResolvedQuery: false);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert - logging should not be attempted when no query
        mockLogger.Verify(l => l.ShouldLog(It.IsAny<EventDefinitionBase>()), Times.Never);
    }

    #endregion

    private static LogQueryHandler CreateHandler()
    {
        var logger = CreateMockDiagnosticsLogger(shouldLog: false).Object;
        return new LogQueryHandler(logger);
    }

    private static Mock<IDiagnosticsLogger<DbLoggerCategory.Query>> CreateMockDiagnosticsLogger(bool shouldLog)
    {
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Query>>();
        var mockLoggingOptions = new Mock<ILoggingOptions>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockInnerLogger = new Mock<ILogger>();

        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockInnerLogger.Object);

        mockLogger
            .Setup(l => l.Definitions)
            .Returns(new FirestoreLoggingDefinitions());

        mockLogger
            .Setup(l => l.Options)
            .Returns(mockLoggingOptions.Object);

        mockLogger
            .Setup(l => l.Logger)
            .Returns(mockInnerLogger.Object);

        mockLogger
            .Setup(l => l.ShouldLog(It.IsAny<EventDefinitionBase>()))
            .Returns(shouldLog);

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
