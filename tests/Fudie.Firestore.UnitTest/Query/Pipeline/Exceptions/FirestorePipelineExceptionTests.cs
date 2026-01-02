namespace Fudie.Firestore.UnitTest.Query.Pipeline.Exceptions;

public class FirestorePipelineExceptionTests
{
    #region Base Exception Tests

    [Fact]
    public void FirestorePipelineException_Is_Abstract()
    {
        typeof(FirestorePipelineException).IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void FirestorePipelineException_Inherits_From_Exception()
    {
        typeof(FirestorePipelineException).Should().BeDerivedFrom<Exception>();
    }

    [Fact]
    public void FirestorePipelineException_Has_Context_Property()
    {
        var property = typeof(FirestorePipelineException).GetProperty("Context");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(PipelineContext));
    }

    #endregion
}

public class FirestoreQueryExecutionExceptionTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreQueryExecutionException_Inherits_From_FirestorePipelineException()
    {
        typeof(FirestoreQueryExecutionException)
            .Should().BeDerivedFrom<FirestorePipelineException>();
    }

    [Fact]
    public void FirestoreQueryExecutionException_Has_Collection_Property()
    {
        var property = typeof(FirestoreQueryExecutionException).GetProperty("Collection");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void FirestoreQueryExecutionException_Has_IsTransient_Property()
    {
        var property = typeof(FirestoreQueryExecutionException).GetProperty("IsTransient");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(bool));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void FirestoreQueryExecutionException_Constructor_Sets_Properties()
    {
        // Arrange
        var context = CreateContext();
        var innerException = new InvalidOperationException("Inner");

        // Act
        var exception = new FirestoreQueryExecutionException(
            "Test message",
            context,
            "users",
            isTransient: true,
            innerException);

        // Assert
        exception.Message.Should().Be("Test message");
        exception.Context.Should().BeSameAs(context);
        exception.Collection.Should().Be("users");
        exception.IsTransient.Should().BeTrue();
        exception.InnerException.Should().BeSameAs(innerException);
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

public class FirestoreDeserializationExceptionTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreDeserializationException_Inherits_From_FirestorePipelineException()
    {
        typeof(FirestoreDeserializationException)
            .Should().BeDerivedFrom<FirestorePipelineException>();
    }

    [Fact]
    public void FirestoreDeserializationException_Has_DocumentId_Property()
    {
        var property = typeof(FirestoreDeserializationException).GetProperty("DocumentId");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void FirestoreDeserializationException_Has_TargetType_Property()
    {
        var property = typeof(FirestoreDeserializationException).GetProperty("TargetType");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Type));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void FirestoreDeserializationException_Constructor_Sets_Properties()
    {
        // Arrange
        var context = CreateContext();
        var innerException = new FormatException("Bad format");

        // Act
        var exception = new FirestoreDeserializationException(
            "Deserialization failed",
            context,
            "doc-123",
            typeof(string),
            innerException);

        // Assert
        exception.Message.Should().Be("Deserialization failed");
        exception.Context.Should().BeSameAs(context);
        exception.DocumentId.Should().Be("doc-123");
        exception.TargetType.Should().Be(typeof(string));
        exception.InnerException.Should().BeSameAs(innerException);
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
