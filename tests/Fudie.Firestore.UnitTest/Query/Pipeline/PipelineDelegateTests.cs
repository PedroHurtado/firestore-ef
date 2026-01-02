namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class PipelineDelegateTests
{
    [Fact]
    public void PipelineDelegate_Is_Delegate_Type()
    {
        typeof(PipelineDelegate).IsSubclassOf(typeof(Delegate)).Should().BeTrue();
    }

    [Fact]
    public void PipelineDelegate_Returns_Task_Of_PipelineResult()
    {
        var invokeMethod = typeof(PipelineDelegate).GetMethod("Invoke");

        invokeMethod.Should().NotBeNull();
        invokeMethod!.ReturnType.Should().Be(typeof(Task<PipelineResult>));
    }

    [Fact]
    public void PipelineDelegate_Accepts_PipelineContext_As_First_Parameter()
    {
        var invokeMethod = typeof(PipelineDelegate).GetMethod("Invoke");
        var parameters = invokeMethod!.GetParameters();

        parameters.Should().HaveCountGreaterOrEqualTo(1);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
    }

    [Fact]
    public void PipelineDelegate_Accepts_CancellationToken_As_Second_Parameter()
    {
        var invokeMethod = typeof(PipelineDelegate).GetMethod("Invoke");
        var parameters = invokeMethod!.GetParameters();

        parameters.Should().HaveCount(2);
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
