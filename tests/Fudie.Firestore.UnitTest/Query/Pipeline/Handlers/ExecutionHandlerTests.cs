using Google.Cloud.Firestore;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class ExecutionHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void ExecutionHandler_Implements_IQueryPipelineHandler()
    {
        typeof(ExecutionHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void ExecutionHandler_Constructor_Accepts_Dependencies()
    {
        var constructors = typeof(ExecutionHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreClientWrapper));
        parameters[1].ParameterType.Should().Be(typeof(IQueryBuilder));
    }

    [Fact]
    public void ExecutionHandler_Can_Be_Instantiated()
    {
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var mockBuilder = new Mock<IQueryBuilder>();

        var handler = new ExecutionHandler(mockClient.Object, mockBuilder.Object);

        handler.Should().NotBeNull();
    }

    #endregion

    #region HandleAsync Method Tests

    [Fact]
    public void HandleAsync_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(ExecutionHandler).GetMethod("HandleAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<PipelineResult>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
        parameters[1].ParameterType.Should().Be(typeof(PipelineDelegate));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region Min/Max Query Strategy Tests

    [Fact]
    public void ExecutionHandler_Has_MinMax_Query_Method()
    {
        // Min/Max should be handled via OrderBy + Limit(1), not native aggregation
        // This verifies the implementation has a separate path for Min/Max
        var methods = typeof(ExecutionHandler)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var minMaxMethod = methods.FirstOrDefault(m => m.Name.Contains("MinMax"));

        minMaxMethod.Should().NotBeNull("Min/Max should have a dedicated execution method");
    }

    [Theory]
    [InlineData(FirestoreAggregationType.Min)]
    [InlineData(FirestoreAggregationType.Max)]
    public void MinMax_Are_Not_Native_Firestore_Aggregations(FirestoreAggregationType type)
    {
        // This test documents that Min/Max are NOT native Firestore aggregations
        // They should be implemented as OrderBy + Limit(1) queries
        var nativeAggregations = new[]
        {
            FirestoreAggregationType.Count,
            FirestoreAggregationType.Sum,
            FirestoreAggregationType.Average
        };

        nativeAggregations.Should().NotContain(type,
            "Min/Max are implemented via OrderBy + Limit(1), not native aggregation");
    }

    #endregion
}

public class IQueryBuilderTests
{
    [Fact]
    public void IQueryBuilder_Is_Interface()
    {
        typeof(IQueryBuilder).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IQueryBuilder_Has_Build_Method()
    {
        var method = typeof(IQueryBuilder).GetMethod("Build");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Google.Cloud.Firestore.Query));
    }

    [Fact]
    public void Build_Accepts_ResolvedFirestoreQuery_Parameter()
    {
        var method = typeof(IQueryBuilder).GetMethod("Build");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
    }
}
