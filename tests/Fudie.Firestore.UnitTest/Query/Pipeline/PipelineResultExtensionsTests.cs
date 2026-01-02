namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineResultExtensionsTests
{
    #region MaterializeAsync Tests

    [Fact]
    public async Task MaterializeAsync_Converts_Streaming_To_Materialized()
    {
        // Arrange
        var context = CreateContext();
        var items = new object[] { "a", "b", "c" };
        var streaming = new PipelineResult.Streaming(items.ToAsyncEnumerable(), context);

        // Act
        var materialized = await streaming.MaterializeAsync(CancellationToken.None);

        // Assert
        materialized.Should().BeOfType<PipelineResult.Materialized>();
        materialized.Items.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task MaterializeAsync_Preserves_Context()
    {
        // Arrange
        var context = CreateContext();
        var streaming = new PipelineResult.Streaming(
            Array.Empty<object>().ToAsyncEnumerable(),
            context);

        // Act
        var materialized = await streaming.MaterializeAsync(CancellationToken.None);

        // Assert
        materialized.Context.Should().BeSameAs(context);
    }

    #endregion

    #region ToStreaming Tests

    [Fact]
    public async Task ToStreaming_Converts_Materialized_To_Streaming()
    {
        // Arrange
        var context = CreateContext();
        var items = new object[] { "x", "y", "z" };
        var materialized = new PipelineResult.Materialized(items, context);

        // Act
        var streaming = materialized.ToStreaming();

        // Assert
        streaming.Should().BeOfType<PipelineResult.Streaming>();
        var resultItems = await streaming.Items.ToListAsync();
        resultItems.Should().BeEquivalentTo(items);
    }

    [Fact]
    public void ToStreaming_Preserves_Context()
    {
        // Arrange
        var context = CreateContext();
        var materialized = new PipelineResult.Materialized(Array.Empty<object>(), context);

        // Act
        var streaming = materialized.ToStreaming();

        // Assert
        streaming.Context.Should().BeSameAs(context);
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
