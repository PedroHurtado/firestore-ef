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
