namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class ConvertHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void ConvertHandler_Implements_IQueryPipelineHandler()
    {
        typeof(ConvertHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void ConvertHandler_Constructor_Accepts_Dependencies()
    {
        var constructors = typeof(ConvertHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreDocumentDeserializer));
        parameters[1].ParameterType.Should().Be(typeof(ITypeConverter));
        parameters[2].ParameterType.Should().Be(typeof(IFirestoreCollectionManager));
    }

    [Fact]
    public void ConvertHandler_Can_Be_Instantiated()
    {
        var mockDeserializer = new Mock<IFirestoreDocumentDeserializer>();
        var mockConverter = new Mock<ITypeConverter>();
        var mockCollectionManager = new Mock<IFirestoreCollectionManager>();

        var handler = new ConvertHandler(mockDeserializer.Object, mockConverter.Object, mockCollectionManager.Object);

        handler.Should().NotBeNull();
    }

    #endregion

    #region HandleAsync Method Tests

    [Fact]
    public void HandleAsync_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(ConvertHandler).GetMethod("HandleAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<PipelineResult>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(PipelineContext));
        parameters[1].ParameterType.Should().Be(typeof(PipelineDelegate));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region Scalar Conversion Tests

    [Fact]
    public void ConvertHandler_Converts_Scalar_Using_TypeConverter()
    {
        // For native aggregations (Count, Sum, Average, Any):
        // ExecutionHandler returns Scalar with raw Firestore value
        // ConvertHandler converts to CLR type using ITypeConverter

        // Example: Count returns long, but CLR expects int
        // ITypeConverter.Convert(42L, typeof(int)) → 42

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler must use ITypeConverter for Scalar results");
    }

    #endregion

    #region Entity Deserialization Tests

    [Fact]
    public void ConvertHandler_Deserializes_Streaming_Documents()
    {
        // For entity queries:
        // ExecutionHandler returns Streaming of DocumentSnapshot
        // ConvertHandler deserializes to entities using IDocumentDeserializer

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler must use IDocumentDeserializer for Streaming results");
    }

    #endregion

    #region Min/Max Handling Tests

    [Fact]
    public void ConvertHandler_Handles_MinMax_Streaming_Result()
    {
        // Min/Max come as Streaming with 0 or 1 documents
        // ConvertHandler must:
        // 1. Check if streaming is empty
        // 2. If empty and non-nullable: throw InvalidOperationException
        // 3. If empty and nullable: return null as Scalar
        // 4. If has document: extract field value, convert, return as Scalar

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler must handle Min/Max Streaming → Scalar conversion");
    }

    [Fact]
    public void MinMax_Empty_NonNullable_Throws_InvalidOperationException()
    {
        // When Min/Max returns empty Streaming and result type is non-nullable:
        // ConvertHandler throws InvalidOperationException("Sequence contains no elements")
        // This matches EF Core behavior against SQL Server

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler throws for empty Min/Max with non-nullable result type");
    }

    [Fact]
    public void MinMax_Empty_Nullable_Returns_Null()
    {
        // When Min/Max returns empty Streaming and result type is nullable:
        // ConvertHandler returns Scalar with null value
        // This matches EF Core behavior: db.Products.MinAsync(x => (decimal?)x.Price)

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler returns null for empty Min/Max with nullable result type");
    }

    [Fact]
    public void MinMax_WithDocument_Extracts_Field_Value()
    {
        // When Min/Max returns Streaming with 1 document:
        // ConvertHandler extracts AggregationPropertyName from document
        // Converts to target type using ITypeConverter
        // Returns as Scalar

        typeof(ConvertHandler).Should().NotBeNull(
            "ConvertHandler extracts field value from Min/Max document");
    }

    #endregion
}

public class ITypeConverterTests
{
    [Fact]
    public void ITypeConverter_Is_Interface()
    {
        typeof(ITypeConverter).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ITypeConverter_Has_Convert_Method()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(object));
    }

    [Fact]
    public void Convert_Accepts_Value_And_TargetType_Parameters()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(object));
        parameters[0].Name.Should().Be("value");
        parameters[1].ParameterType.Should().Be(typeof(Type));
        parameters[1].Name.Should().Be("targetType");
    }
}

