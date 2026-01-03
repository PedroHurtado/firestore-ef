namespace Fudie.Firestore.UnitTest.Query.Pipeline.Exceptions;

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
