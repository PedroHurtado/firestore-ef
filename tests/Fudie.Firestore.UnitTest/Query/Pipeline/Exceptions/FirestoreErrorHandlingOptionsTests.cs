namespace Fudie.Firestore.UnitTest.Query.Pipeline.Exceptions;

public class FirestoreErrorHandlingOptionsTests
{
    #region Property Tests

    [Fact]
    public void FirestoreErrorHandlingOptions_Has_MaxRetries_Property()
    {
        var property = typeof(FirestoreErrorHandlingOptions).GetProperty("MaxRetries");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(int));
    }

    [Fact]
    public void FirestoreErrorHandlingOptions_Has_InitialDelay_Property()
    {
        var property = typeof(FirestoreErrorHandlingOptions).GetProperty("InitialDelay");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(TimeSpan));
    }

    [Fact]
    public void FirestoreErrorHandlingOptions_Has_GetDelay_Method()
    {
        var method = typeof(FirestoreErrorHandlingOptions).GetMethod("GetDelay");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(TimeSpan));
        method.GetParameters().Should().HaveCount(1);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(int));
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void FirestoreErrorHandlingOptions_Has_Default_MaxRetries()
    {
        var options = new FirestoreErrorHandlingOptions();

        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void FirestoreErrorHandlingOptions_Has_Default_InitialDelay()
    {
        var options = new FirestoreErrorHandlingOptions();

        options.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    #endregion

    #region GetDelay Tests

    [Fact]
    public void GetDelay_Returns_InitialDelay_For_First_Attempt()
    {
        var options = new FirestoreErrorHandlingOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };

        var delay = options.GetDelay(1);

        delay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void GetDelay_Applies_Exponential_Backoff()
    {
        var options = new FirestoreErrorHandlingOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };

        var delay1 = options.GetDelay(1);
        var delay2 = options.GetDelay(2);
        var delay3 = options.GetDelay(3);

        // Exponential backoff: delay * 2^(attempt-1)
        delay1.Should().Be(TimeSpan.FromMilliseconds(100));
        delay2.Should().Be(TimeSpan.FromMilliseconds(200));
        delay3.Should().Be(TimeSpan.FromMilliseconds(400));
    }

    #endregion
}
