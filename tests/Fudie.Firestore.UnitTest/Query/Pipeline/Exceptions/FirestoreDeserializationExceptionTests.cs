namespace Fudie.Firestore.UnitTest.Query.Pipeline.Exceptions;

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
