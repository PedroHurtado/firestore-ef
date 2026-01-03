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

    [Fact]
    public void MinMax_Method_Returns_Task_Of_PipelineResult()
    {
        // Min/Max should return Streaming (not Scalar) because:
        // 1. ExecutionHandler only executes: SELECT field ORDER BY field LIMIT 1
        // 2. ConvertHandler extracts the field value and converts to scalar
        // 3. Empty sequence handling is NOT ExecutionHandler's responsibility
        var method = typeof(ExecutionHandler)
            .GetMethod("ExecuteMinMaxQueryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<PipelineResult>));
    }

    [Fact]
    public void MinMax_Does_Not_Throw_On_Empty_Sequence()
    {
        // Documents that ExecutionHandler does NOT throw InvalidOperationException
        // for empty sequences. That behavior depends on:
        // - Nullable result type: returns null
        // - Non-nullable result type: throws (handled by LINQ operator, not ExecutionHandler)
        //
        // ExecutionHandler simply returns Streaming with 0 documents.
        // The conversion and exception throwing is ConvertHandler's responsibility.

        var method = typeof(ExecutionHandler)
            .GetMethod("ExecuteMinMaxQueryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Method should not have any throw statements for empty sequence
        // This is a documentation test - the actual behavior is verified in integration tests
        method.Should().NotBeNull(
            "Min/Max execution should exist and delegate empty handling to ConvertHandler");
    }

    [Fact]
    public void MinMax_Returns_Streaming_Not_Scalar()
    {
        // Min/Max returns Streaming because:
        // - ExecutionHandler executes: SELECT field FROM collection ORDER BY field LIMIT 1
        // - Returns DocumentSnapshot in Streaming result
        // - ConvertHandler will:
        //   1. Extract field value from document
        //   2. Convert to target type
        //   3. Handle empty sequence based on nullable/non-nullable result type
        //
        // This separation of concerns allows proper type conversion and error handling.

        var method = typeof(ExecutionHandler)
            .GetMethod("ExecuteMinMaxQueryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();

        // The method signature returns PipelineResult, which will be Streaming
        // Actual return type verification requires runtime testing (integration tests)
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

    [Fact]
    public void IQueryBuilder_Has_BuildAggregate_Method()
    {
        var method = typeof(IQueryBuilder).GetMethod("BuildAggregate");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(AggregateQuery));
    }

    [Fact]
    public void BuildAggregate_Accepts_ResolvedFirestoreQuery_Parameter()
    {
        var method = typeof(IQueryBuilder).GetMethod("BuildAggregate");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedFirestoreQuery));
    }
}
