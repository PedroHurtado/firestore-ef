namespace Fudie.Firestore.UnitTest.Conventions;

public class ListDecimalToDoubleArrayConventionTest
{
    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_For_List_Decimal()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<decimal>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_For_List_Nullable_Decimal()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<decimal?>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_List_Double()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<double>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_List_Int()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<int>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_Single_Decimal()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(decimal));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Override_Existing_Converter()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(
            typeof(List<decimal>),
            hasExistingConverter: true);

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void Converter_Converts_List_Decimal_To_List_Double_Correctly()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        ValueConverter? capturedConverter = null;
        var (propertyBuilder, context, _) = CreatePropertyBuilderMock(
            typeof(List<decimal>),
            captureConverter: c => capturedConverter = c);

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        capturedConverter.Should().NotBeNull();

        var input = new List<decimal> { 1.5m, 2.5m, 3.5m };
        var converted = capturedConverter!.ConvertToProvider!(input);
        converted.Should().BeEquivalentTo(new List<double> { 1.5, 2.5, 3.5 });
    }

    [Fact]
    public void Converter_Converts_List_Double_Back_To_List_Decimal_Correctly()
    {
        // Arrange
        var convention = new ListDecimalToDoubleArrayConvention();
        ValueConverter? capturedConverter = null;
        var (propertyBuilder, context, _) = CreatePropertyBuilderMock(
            typeof(List<decimal>),
            captureConverter: c => capturedConverter = c);

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        capturedConverter.Should().NotBeNull();

        var input = new List<double> { 1.5, 2.5, 3.5 };
        var converted = capturedConverter!.ConvertFromProvider!(input);
        converted.Should().BeEquivalentTo(new List<decimal> { 1.5m, 2.5m, 3.5m });
    }

    private static (Mock<IConventionPropertyBuilder>, Mock<IConventionContext<IConventionPropertyBuilder>>, Func<bool>)
        CreatePropertyBuilderMock(Type propertyType, bool hasExistingConverter = false, Action<ValueConverter>? captureConverter = null)
    {
        bool hasConversionCalled = false;

        var propertyMock = new Mock<IConventionProperty>();
        propertyMock.Setup(p => p.ClrType).Returns(propertyType);
        propertyMock.Setup(p => p.GetValueConverter())
            .Returns(hasExistingConverter ? new CastingConverter<int, long>() : null);

        var builderMock = new Mock<IConventionPropertyBuilder>();
        builderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        builderMock
            .Setup(b => b.HasConversion(It.IsAny<ValueConverter>(), It.IsAny<bool>()))
            .Callback<ValueConverter, bool>((converter, _) =>
            {
                hasConversionCalled = true;
                captureConverter?.Invoke(converter);
            })
            .Returns(builderMock.Object);

        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        return (builderMock, contextMock, () => hasConversionCalled);
    }
}
