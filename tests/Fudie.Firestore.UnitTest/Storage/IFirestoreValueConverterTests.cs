using Firestore.EntityFrameworkCore.Storage;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Storage;

/// <summary>
/// Tests for the IFirestoreValueConverter interface contract.
/// Documents the expected behavior that any implementation must provide.
/// </summary>
public class IFirestoreValueConverterTests
{
    #region Interface Contract Tests

    [Fact]
    public void IFirestoreValueConverter_Should_Have_ToFirestore_Method()
    {
        // Documents that ToFirestore converts CLR values to Firestore-compatible types
        // decimal → double, enum → string, DateTime → UTC
        var method = typeof(IFirestoreValueConverter).GetMethod("ToFirestore");

        method.Should().NotBeNull("IFirestoreValueConverter must have ToFirestore method");
        method!.ReturnType.Should().Be(typeof(object), "ToFirestore returns object");
        method.GetParameters().Should().HaveCount(1, "ToFirestore takes a single value parameter");
    }

    [Fact]
    public void IFirestoreValueConverter_Should_Have_FromFirestore_Method()
    {
        // Documents that FromFirestore converts Firestore values back to CLR types
        // double → decimal, string → enum
        var method = typeof(IFirestoreValueConverter).GetMethod("FromFirestore");

        method.Should().NotBeNull("IFirestoreValueConverter must have FromFirestore method");
        method!.ReturnType.Should().Be(typeof(object), "FromFirestore returns object");
        method.GetParameters().Should().HaveCount(2, "FromFirestore takes value and target type");
    }

    [Fact]
    public void IFirestoreValueConverter_Should_Have_Two_Methods()
    {
        // Documents that IFirestoreValueConverter centralizes bidirectional conversion
        typeof(IFirestoreValueConverter).GetMethods()
            .Should().HaveCount(2, "IFirestoreValueConverter has ToFirestore and FromFirestore methods");
    }

    #endregion
}
