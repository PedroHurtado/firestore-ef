using Firestore.EntityFrameworkCore.Query.Pipeline;
using FluentAssertions;
using Google.Cloud.Firestore;
using System;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class FirestoreTypeConverterTests
{
    private readonly FirestoreTypeConverter _converter = new();

    #region Class Structure Tests

    [Fact]
    public void FirestoreTypeConverter_Implements_ITypeConverter()
    {
        typeof(FirestoreTypeConverter)
            .Should().Implement<ITypeConverter>();
    }

    [Fact]
    public void FirestoreTypeConverter_Has_Parameterless_Constructor()
    {
        var constructors = typeof(FirestoreTypeConverter).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().BeEmpty();
    }

    [Fact]
    public void FirestoreTypeConverter_Can_Be_Instantiated()
    {
        var converter = new FirestoreTypeConverter();

        converter.Should().NotBeNull();
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Convert_Null_To_ReferenceType_Returns_Null()
    {
        var result = _converter.Convert(null, typeof(string));

        result.Should().BeNull();
    }

    [Fact]
    public void Convert_Null_To_NullableValueType_Returns_Null()
    {
        var result = _converter.Convert(null, typeof(int?));

        result.Should().BeNull();
    }

    [Fact]
    public void Convert_Null_To_NonNullableValueType_Returns_Default()
    {
        var result = _converter.Convert(null, typeof(int));

        result.Should().Be(0);
    }

    [Fact]
    public void Convert_Null_To_NonNullableDecimal_Returns_Zero()
    {
        var result = _converter.Convert(null, typeof(decimal));

        result.Should().Be(0m);
    }

    [Fact]
    public void Convert_Null_To_NonNullableDateTime_Returns_Default()
    {
        var result = _converter.Convert(null, typeof(DateTime));

        result.Should().Be(default(DateTime));
    }

    #endregion

    #region Pass-Through Tests (Compatible Types)

    [Fact]
    public void Convert_String_To_String_Returns_SameValue()
    {
        var result = _converter.Convert("hello", typeof(string));

        result.Should().Be("hello");
    }

    [Fact]
    public void Convert_Int_To_Int_Returns_SameValue()
    {
        var result = _converter.Convert(42, typeof(int));

        result.Should().Be(42);
    }

    [Fact]
    public void Convert_Long_To_Long_Returns_SameValue()
    {
        var result = _converter.Convert(42L, typeof(long));

        result.Should().Be(42L);
    }

    [Fact]
    public void Convert_Double_To_Double_Returns_SameValue()
    {
        var result = _converter.Convert(3.14, typeof(double));

        result.Should().Be(3.14);
    }

    [Fact]
    public void Convert_Bool_To_Bool_Returns_SameValue()
    {
        var result = _converter.Convert(true, typeof(bool));

        result.Should().Be(true);
    }

    #endregion

    #region Long to Int Conversion Tests

    [Fact]
    public void Convert_Long_To_Int_Returns_IntValue()
    {
        var result = _converter.Convert(42L, typeof(int));

        result.Should().Be(42);
        result.Should().BeOfType<int>();
    }

    [Fact]
    public void Convert_Long_To_NullableInt_Returns_IntValue()
    {
        var result = _converter.Convert(42L, typeof(int?));

        result.Should().Be(42);
    }

    [Fact]
    public void Convert_Zero_Long_To_Int_Returns_Zero()
    {
        var result = _converter.Convert(0L, typeof(int));

        result.Should().Be(0);
    }

    [Fact]
    public void Convert_Negative_Long_To_Int_Returns_NegativeInt()
    {
        var result = _converter.Convert(-100L, typeof(int));

        result.Should().Be(-100);
    }

    #endregion

    #region Double to Decimal Conversion Tests

    [Fact]
    public void Convert_Double_To_Decimal_Returns_DecimalValue()
    {
        var result = _converter.Convert(123.45, typeof(decimal));

        result.Should().Be(123.45m);
        result.Should().BeOfType<decimal>();
    }

    [Fact]
    public void Convert_Double_To_NullableDecimal_Returns_DecimalValue()
    {
        var result = _converter.Convert(123.45, typeof(decimal?));

        result.Should().Be(123.45m);
    }

    [Fact]
    public void Convert_Zero_Double_To_Decimal_Returns_Zero()
    {
        var result = _converter.Convert(0.0, typeof(decimal));

        result.Should().Be(0m);
    }

    [Fact]
    public void Convert_Negative_Double_To_Decimal_Returns_NegativeDecimal()
    {
        var result = _converter.Convert(-99.99, typeof(decimal));

        result.Should().Be(-99.99m);
    }

    #endregion

    #region Timestamp to DateTime Conversion Tests

    [Fact]
    public void Convert_Timestamp_To_DateTime_Returns_DateTimeValue()
    {
        var timestamp = Timestamp.FromDateTime(new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc));

        var result = _converter.Convert(timestamp, typeof(DateTime));

        result.Should().BeOfType<DateTime>();
        var dateTime = (DateTime)result!;
        dateTime.Year.Should().Be(2024);
        dateTime.Month.Should().Be(6);
        dateTime.Day.Should().Be(15);
    }

    [Fact]
    public void Convert_Timestamp_To_NullableDateTime_Returns_DateTimeValue()
    {
        var timestamp = Timestamp.FromDateTime(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = _converter.Convert(timestamp, typeof(DateTime?));

        result.Should().NotBeNull();
        ((DateTime?)result)!.Value.Year.Should().Be(2024);
    }

    #endregion

    #region Timestamp to DateTimeOffset Conversion Tests

    [Fact]
    public void Convert_Timestamp_To_DateTimeOffset_Returns_DateTimeOffsetValue()
    {
        var timestamp = Timestamp.FromDateTime(new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc));

        var result = _converter.Convert(timestamp, typeof(DateTimeOffset));

        result.Should().BeOfType<DateTimeOffset>();
        var dto = (DateTimeOffset)result!;
        dto.Year.Should().Be(2024);
        dto.Month.Should().Be(6);
        dto.Day.Should().Be(15);
    }

    [Fact]
    public void Convert_Timestamp_To_NullableDateTimeOffset_Returns_DateTimeOffsetValue()
    {
        var timestamp = Timestamp.FromDateTime(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = _converter.Convert(timestamp, typeof(DateTimeOffset?));

        result.Should().NotBeNull();
        ((DateTimeOffset?)result)!.Value.Year.Should().Be(2024);
    }

    #endregion

    #region System.Convert Fallback Tests

    [Fact]
    public void Convert_Int_To_Long_Uses_SystemConvert()
    {
        var result = _converter.Convert(42, typeof(long));

        result.Should().Be(42L);
        result.Should().BeOfType<long>();
    }

    [Fact]
    public void Convert_Int_To_Double_Uses_SystemConvert()
    {
        var result = _converter.Convert(42, typeof(double));

        result.Should().Be(42.0);
        result.Should().BeOfType<double>();
    }

    [Fact]
    public void Convert_String_To_Int_Uses_SystemConvert()
    {
        var result = _converter.Convert("123", typeof(int));

        result.Should().Be(123);
    }

    [Fact]
    public void Convert_Int_To_String_Uses_SystemConvert()
    {
        var result = _converter.Convert(123, typeof(string));

        result.Should().Be("123");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Convert_MaxLong_To_Int_Wraps_Unchecked()
    {
        // C# casts are unchecked by default, so long.MaxValue wraps to -1
        var result = _converter.Convert(long.MaxValue, typeof(int));

        result.Should().Be(-1);
    }

    [Fact]
    public void Convert_LargeDouble_To_Decimal_May_Overflow()
    {
        // Very large doubles can't be represented as decimal
        var action = () => _converter.Convert(double.MaxValue, typeof(decimal));

        action.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Convert_EmptyString_To_Int_Throws()
    {
        var action = () => _converter.Convert("", typeof(int));

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Convert_InvalidString_To_Int_Throws()
    {
        var action = () => _converter.Convert("not-a-number", typeof(int));

        action.Should().Throw<FormatException>();
    }

    #endregion
}
