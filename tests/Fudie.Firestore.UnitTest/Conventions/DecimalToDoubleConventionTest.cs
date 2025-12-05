namespace Fudie.Firestore.UnitTest.Conventions;

public class DecimalToDoubleConventionTest
{
    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_To_Decimal_Property()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(decimal));

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_To_Nullable_Decimal_Property()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(decimal?));

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_To_Double_Property()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(double));

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_To_Int_Property()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(int));

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_To_String_Property()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(string));

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Override_Existing_Converter()
    {
        var convention = new DecimalToDoubleConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(decimal), hasExistingConverter: true);

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Uses_CastingConverter_For_Decimal()
    {
        var convention = new DecimalToDoubleConvention();
        ValueConverter? capturedConverter = null;
        var (propertyBuilder, context, _) = CreatePropertyBuilderMock(typeof(decimal), captureConverter: c => capturedConverter = c);

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        capturedConverter.Should().NotBeNull();
        capturedConverter!.GetType().Should().Be(typeof(CastingConverter<decimal, double>));
    }

    [Fact]
    public void ProcessPropertyAdded_Uses_CastingConverter_For_NullableDecimal()
    {
        var convention = new DecimalToDoubleConvention();
        ValueConverter? capturedConverter = null;
        var (propertyBuilder, context, _) = CreatePropertyBuilderMock(typeof(decimal?), captureConverter: c => capturedConverter = c);

        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        capturedConverter.Should().NotBeNull();
        capturedConverter!.GetType().Should().Be(typeof(CastingConverter<decimal?, double?>));
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

        // Setup HasConversion to track if it was called
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
