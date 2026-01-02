namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class IQueryPipelineHandlerTests
{
    [Fact]
    public void IQueryPipelineHandler_Is_Interface()
    {
        typeof(IQueryPipelineHandler).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IQueryPipelineHandler_Has_HandleAsync_Method()
    {
        var method = typeof(IQueryPipelineHandler).GetMethod("HandleAsync");

        method.Should().NotBeNull();
    }

    [Fact]
    public void HandleAsync_Returns_Task_Of_PipelineResult()
    {
        var method = typeof(IQueryPipelineHandler).GetMethod("HandleAsync");

        method!.ReturnType.Should().Be(typeof(Task<PipelineResult>));
    }

    [Fact]
    public void HandleAsync_Has_PipelineContext_Parameter()
    {
        var method = typeof(IQueryPipelineHandler).GetMethod("HandleAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCountGreaterOrEqualTo(1);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
        parameters[0].Name.Should().Be("context");
    }

    [Fact]
    public void HandleAsync_Has_PipelineDelegate_Parameter()
    {
        var method = typeof(IQueryPipelineHandler).GetMethod("HandleAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCountGreaterOrEqualTo(2);
        parameters[1].ParameterType.Should().Be(typeof(PipelineDelegate));
        parameters[1].Name.Should().Be("next");
    }

    [Fact]
    public void HandleAsync_Has_CancellationToken_Parameter()
    {
        var method = typeof(IQueryPipelineHandler).GetMethod("HandleAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(3);
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[2].Name.Should().Be("cancellationToken");
    }
}
