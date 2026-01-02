namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class ResolverHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void ResolverHandler_Implements_IQueryPipelineHandler()
    {
        typeof(ResolverHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void ResolverHandler_Constructor_Accepts_IFirestoreAstResolver()
    {
        var constructors = typeof(ResolverHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreAstResolver));
    }

    #endregion

    #region Behavior Tests

    [Fact]
    public async Task HandleAsync_Resolves_Ast_And_Sets_ResolvedQuery()
    {
        // Arrange
        var mockResolver = new Mock<IFirestoreAstResolver>();
        var resolvedQuery = CreateResolvedQuery();
        mockResolver
            .Setup(r => r.Resolve(It.IsAny<FirestoreQueryExpression>()))
            .Returns(resolvedQuery);

        var handler = new ResolverHandler(mockResolver.Object);
        var context = CreateContext();
        PipelineContext? capturedContext = null;

        PipelineDelegate next = (ctx, ct) =>
        {
            capturedContext = ctx;
            return Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));
        };

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.ResolvedQuery.Should().BeSameAs(resolvedQuery);
    }

    [Fact]
    public async Task HandleAsync_Calls_Next_With_Updated_Context()
    {
        // Arrange
        var mockResolver = new Mock<IFirestoreAstResolver>();
        mockResolver
            .Setup(r => r.Resolve(It.IsAny<FirestoreQueryExpression>()))
            .Returns(CreateResolvedQuery());

        var handler = new ResolverHandler(mockResolver.Object);
        var context = CreateContext();
        var nextCalled = false;

        PipelineDelegate next = (ctx, ct) =>
        {
            nextCalled = true;
            return Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));
        };

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Passes_Ast_To_Resolver()
    {
        // Arrange
        var mockResolver = new Mock<IFirestoreAstResolver>();
        mockResolver
            .Setup(r => r.Resolve(It.IsAny<FirestoreQueryExpression>()))
            .Returns(CreateResolvedQuery());

        var handler = new ResolverHandler(mockResolver.Object);
        var ast = CreateAst();
        var context = CreateContext(ast);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        // Act
        await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        mockResolver.Verify(r => r.Resolve(ast), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Returns_Result_From_Next()
    {
        // Arrange
        var mockResolver = new Mock<IFirestoreAstResolver>();
        mockResolver
            .Setup(r => r.Resolve(It.IsAny<FirestoreQueryExpression>()))
            .Returns(CreateResolvedQuery());

        var handler = new ResolverHandler(mockResolver.Object);
        var context = CreateContext();
        var expectedResult = new PipelineResult.Scalar(42, context);

        PipelineDelegate next = (ctx, ct) =>
            Task.FromResult<PipelineResult>(expectedResult);

        // Act
        var result = await handler.HandleAsync(context, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    #endregion

    private static PipelineContext CreateContext(FirestoreQueryExpression? ast = null)
    {
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        return new PipelineContext
        {
            Ast = ast ?? CreateAst(),
            QueryContext = mockQueryContext.Object,
            IsTracking = false,
            ResultType = typeof(object),
            Kind = QueryKind.Entity
        };
    }

    private static FirestoreQueryExpression CreateAst()
    {
        var mockEntityType = new Mock<IEntityType>();
        mockEntityType.Setup(e => e.ClrType).Returns(typeof(object));
        return new FirestoreQueryExpression(mockEntityType.Object, "test-collection");
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