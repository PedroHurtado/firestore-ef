namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class IQueryPipelineMediatorTests
{
    [Fact]
    public void IQueryPipelineMediator_Is_Interface()
    {
        typeof(IQueryPipelineMediator).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IQueryPipelineMediator_Has_ExecuteAsync_Method()
    {
        var method = typeof(IQueryPipelineMediator).GetMethod("ExecuteAsync");

        method.Should().NotBeNull();
    }

    [Fact]
    public void ExecuteAsync_Is_Generic_Method()
    {
        var method = typeof(IQueryPipelineMediator).GetMethod("ExecuteAsync");

        method!.IsGenericMethod.Should().BeTrue();
        method.GetGenericArguments().Should().HaveCount(1);
    }

    [Fact]
    public void ExecuteAsync_Returns_IAsyncEnumerable_Of_T()
    {
        var method = typeof(IQueryPipelineMediator).GetMethod("ExecuteAsync");
        var genericMethod = method!.MakeGenericMethod(typeof(object));

        genericMethod.ReturnType.Should().Be(typeof(IAsyncEnumerable<object>));
    }

    [Fact]
    public void ExecuteAsync_Has_PipelineContext_Parameter()
    {
        var method = typeof(IQueryPipelineMediator).GetMethod("ExecuteAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCountGreaterOrEqualTo(1);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
        parameters[0].Name.Should().Be("context");
    }

    [Fact]
    public void ExecuteAsync_Has_CancellationToken_Parameter()
    {
        var method = typeof(IQueryPipelineMediator).GetMethod("ExecuteAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(2);
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
