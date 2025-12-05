namespace Fudie.Firestore.UnitTest.TypeMapping;

public class DecimalToDoubleConverterTest
{
    [Fact]
    public void ConvertToProvider_Converts_Decimal_To_Double()
    {
        var converter = new DecimalToDoubleConverter();
        var convertToProvider = converter.ConvertToProvider;

        var result = convertToProvider!(123.45m);

        result.Should().Be(123.45d);
    }

    [Fact]
    public void ConvertFromProvider_Converts_Double_To_Decimal()
    {
        var converter = new DecimalToDoubleConverter();
        var convertFromProvider = converter.ConvertFromProvider;

        var result = convertFromProvider!(123.45d);

        result.Should().Be(123.45m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(999999.99)]
    [InlineData(-999999.99)]
    public void ConvertToProvider_Handles_Various_Values(decimal input)
    {
        var converter = new DecimalToDoubleConverter();
        var convertToProvider = converter.ConvertToProvider;

        var result = convertToProvider!(input);

        result.Should().BeOfType<double>();
        ((double)result!).Should().BeApproximately((double)input, 0.001);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-1d)]
    [InlineData(999999.99d)]
    [InlineData(-999999.99d)]
    public void ConvertFromProvider_Handles_Various_Values(double input)
    {
        var converter = new DecimalToDoubleConverter();
        var convertFromProvider = converter.ConvertFromProvider;

        var result = convertFromProvider!(input);

        result.Should().BeOfType<decimal>();
        ((decimal)result!).Should().BeApproximately((decimal)input, 0.001m);
    }

    [Fact]
    public void Roundtrip_Preserves_Value()
    {
        var converter = new DecimalToDoubleConverter();
        var original = 123.456m;

        var toProvider = converter.ConvertToProvider!(original);
        var fromProvider = converter.ConvertFromProvider!(toProvider);

        ((decimal)fromProvider!).Should().BeApproximately(original, 0.0001m);
    }

    [Fact]
    public void ModelClrType_Is_Decimal()
    {
        var converter = new DecimalToDoubleConverter();

        converter.ModelClrType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void ProviderClrType_Is_Double()
    {
        var converter = new DecimalToDoubleConverter();

        converter.ProviderClrType.Should().Be(typeof(double));
    }

    [Fact]
    public void Large_Decimal_Values_May_Lose_Precision()
    {
        var converter = new DecimalToDoubleConverter();
        var largeDecimal = 12345678901234567890.1234567890m;

        var toProvider = converter.ConvertToProvider!(largeDecimal);
        var fromProvider = converter.ConvertFromProvider!(toProvider);

        // Note: This test documents expected precision loss with very large values
        ((decimal)fromProvider!).Should().NotBe(largeDecimal,
            "very large decimal values lose precision when converted to double");
    }
}
