using Grpc.Core;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Exceptions;

public class FirestoreExceptionClassifierTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreExceptionClassifier_Is_Static_Class()
    {
        typeof(FirestoreExceptionClassifier).IsAbstract.Should().BeTrue();
        typeof(FirestoreExceptionClassifier).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void FirestoreExceptionClassifier_Has_IsTransient_Method()
    {
        var method = typeof(FirestoreExceptionClassifier).GetMethod("IsTransient");

        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
        method.ReturnType.Should().Be(typeof(bool));
    }

    [Fact]
    public void IsTransient_Accepts_RpcException_Parameter()
    {
        var method = typeof(FirestoreExceptionClassifier).GetMethod("IsTransient");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(RpcException));
    }

    #endregion

    #region Transient Classification Tests

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Aborted)]
    public void IsTransient_Returns_True_For_Transient_Status_Codes(StatusCode statusCode)
    {
        // Arrange
        var exception = new RpcException(new Status(statusCode, "Test"));

        // Act
        var result = FirestoreExceptionClassifier.IsTransient(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.Unimplemented)]
    public void IsTransient_Returns_False_For_Non_Transient_Status_Codes(StatusCode statusCode)
    {
        // Arrange
        var exception = new RpcException(new Status(statusCode, "Test"));

        // Act
        var result = FirestoreExceptionClassifier.IsTransient(exception);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
