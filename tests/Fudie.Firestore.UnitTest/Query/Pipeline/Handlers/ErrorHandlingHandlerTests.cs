using Grpc.Core;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class ErrorHandlingHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void ErrorHandlingHandler_Implements_IQueryPipelineHandler()
    {
        typeof(ErrorHandlingHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void ErrorHandlingHandler_Constructor_Accepts_Logger_And_Options()
    {
        var constructors = typeof(ErrorHandlingHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(ILogger<ErrorHandlingHandler>));
        parameters[1].ParameterType.Should().Be(typeof(FirestoreErrorHandlingOptions));
    }

    #endregion

    #region Pass-Through Tests

    [Fact]
    public async Task HandleAsync_Passes_Through_On_Success()
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

    #endregion

    #region Retry Tests

    [Fact]
    public async Task HandleAsync_Retries_On_Transient_Error()
    {
        // Arrange
        var options = new FirestoreErrorHandlingOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1)
        };
        var handler = CreateHandler(options);
        var context = CreateContext();
        var expectedResult = new PipelineResult.Empty(context);
        var callCount = 0;

        PipelineDelegate next = (ctx, ct) =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new FirestoreQueryExecutionException(
                    "Transient error",
                    ctx,
                    "users",
                    isTransient: true);
            }
            return Task.FromResult<PipelineResult>(expectedResult);
        };

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        callCount.Should().Be(3);
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_Throws_After_Max_Retries()
    {
        // Arrange
        var options = new FirestoreErrorHandlingOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1)
        };
        var handler = CreateHandler(options);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = (ctx, ct) =>
        {
            callCount++;
            throw new FirestoreQueryExecutionException(
                "Transient error",
                ctx,
                "users",
                isTransient: true);
        };

        // Act
        var act = () => handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FirestoreQueryExecutionException>();
        callCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task HandleAsync_Does_Not_Retry_Non_Transient_Errors()
    {
        // Arrange
        var options = new FirestoreErrorHandlingOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1)
        };
        var handler = CreateHandler(options);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = (ctx, ct) =>
        {
            callCount++;
            throw new FirestoreQueryExecutionException(
                "Non-transient error",
                ctx,
                "users",
                isTransient: false);
        };

        // Act
        var act = () => handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FirestoreQueryExecutionException>();
        callCount.Should().Be(1); // No retries
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task HandleAsync_Respects_Cancellation_During_Retry()
    {
        // Arrange
        var options = new FirestoreErrorHandlingOptions
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(10)
        };
        var handler = CreateHandler(options);
        var context = CreateContext();
        var cts = new CancellationTokenSource();

        PipelineDelegate next = (ctx, ct) =>
        {
            cts.Cancel(); // Cancel after first call
            throw new FirestoreQueryExecutionException(
                "Transient error",
                ctx,
                "users",
                isTransient: true);
        };

        // Act
        var act = () => handler.HandleAsync(context, next, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    private static ErrorHandlingHandler CreateHandler(FirestoreErrorHandlingOptions? options = null)
    {
        var logger = new Mock<ILogger<ErrorHandlingHandler>>().Object;
        options ??= new FirestoreErrorHandlingOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1)
        };

        return new ErrorHandlingHandler(logger, options);
    }

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
