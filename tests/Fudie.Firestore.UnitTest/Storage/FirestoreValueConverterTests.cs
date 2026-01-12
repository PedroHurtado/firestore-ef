using Fudie.Firestore.EntityFrameworkCore.Storage;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Storage;

/// <summary>
/// Tests for FirestoreValueConverter implementation.
/// Verifies bidirectional conversion between CLR types and Firestore types.
/// </summary>
public class FirestoreValueConverterTests
{
    private readonly IFirestoreValueConverter _converter = new FirestoreValueConverter();

    public enum TestStatus { Pending, Active, Completed }

    #region ToFirestore Tests - CLR to Firestore

    [Fact]
    public void ToFirestore_Decimal_ReturnsDouble()
    {
        // Firestore doesn't support decimal, must convert to double
        var result = _converter.ToFirestore(123.45m);

        result.Should().BeOfType<double>();
        result.Should().Be(123.45d);
    }

    [Fact]
    public void ToFirestore_Enum_ReturnsString()
    {
        // Firestore stores enums as strings
        var result = _converter.ToFirestore(TestStatus.Active);

        result.Should().BeOfType<string>();
        result.Should().Be("Active");
    }

    [Fact]
    public void ToFirestore_DateTime_ReturnsUtc()
    {
        // Firestore stores timestamps in UTC
        var localTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Local);
        var result = _converter.ToFirestore(localTime);

        result.Should().BeOfType<DateTime>();
        ((DateTime)result!).Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToFirestore_Int_ReturnsUnchanged()
    {
        // int is natively supported
        var result = _converter.ToFirestore(42);

        result.Should().Be(42);
    }

    [Fact]
    public void ToFirestore_Long_ReturnsUnchanged()
    {
        // long is natively supported
        var result = _converter.ToFirestore(123456789L);

        result.Should().Be(123456789L);
    }

    [Fact]
    public void ToFirestore_Double_ReturnsUnchanged()
    {
        // double is natively supported
        var result = _converter.ToFirestore(3.14159d);

        result.Should().Be(3.14159d);
    }

    [Fact]
    public void ToFirestore_String_ReturnsUnchanged()
    {
        // string is natively supported
        var result = _converter.ToFirestore("hello");

        result.Should().Be("hello");
    }

    [Fact]
    public void ToFirestore_Bool_ReturnsUnchanged()
    {
        // bool is natively supported
        var result = _converter.ToFirestore(true);

        result.Should().Be(true);
    }

    [Fact]
    public void ToFirestore_Null_ReturnsNull()
    {
        var result = _converter.ToFirestore(null);

        result.Should().BeNull();
    }

    [Fact]
    public void ToFirestore_ListOfDecimals_ReturnsListOfDoubles()
    {
        // List<decimal> must be converted to array of doubles
        var decimals = new List<decimal> { 1.1m, 2.2m, 3.3m };

        var result = _converter.ToFirestore(decimals);

        result.Should().BeAssignableTo<IEnumerable<object>>();
        var array = ((IEnumerable<object>)result!).ToArray();
        array.Should().HaveCount(3);
        array[0].Should().BeOfType<double>();
        array[0].Should().Be(1.1d);
    }

    [Fact]
    public void ToFirestore_ListOfEnums_ReturnsListOfStrings()
    {
        // List<enum> must be converted to array of strings
        var enums = new List<TestStatus> { TestStatus.Pending, TestStatus.Active };

        var result = _converter.ToFirestore(enums);

        result.Should().BeAssignableTo<IEnumerable<object>>();
        var array = ((IEnumerable<object>)result!).ToArray();
        array.Should().HaveCount(2);
        array[0].Should().Be("Pending");
        array[1].Should().Be("Active");
    }

    [Fact]
    public void ToFirestore_IntWithEnumType_ReturnsEnumString()
    {
        // When EF Core parameterizes an enum, it may pass an int value
        // The enumType hint allows conversion to the enum name string
        var result = _converter.ToFirestore(1, typeof(TestStatus)); // 1 = Active

        result.Should().BeOfType<string>();
        result.Should().Be("Active");
    }

    [Fact]
    public void ToFirestore_LongWithEnumType_ReturnsEnumString()
    {
        // Same for long values
        var result = _converter.ToFirestore(2L, typeof(TestStatus)); // 2 = Completed

        result.Should().BeOfType<string>();
        result.Should().Be("Completed");
    }

    #endregion

    #region FromFirestore Tests - Firestore to CLR

    [Fact]
    public void FromFirestore_DoubleToDecimal_ReturnsDecimal()
    {
        var result = _converter.FromFirestore(123.45d, typeof(decimal));

        result.Should().BeOfType<decimal>();
        result.Should().Be(123.45m);
    }

    [Fact]
    public void FromFirestore_StringToEnum_ReturnsEnum()
    {
        var result = _converter.FromFirestore("Active", typeof(TestStatus));

        result.Should().BeOfType<TestStatus>();
        result.Should().Be(TestStatus.Active);
    }

    [Fact]
    public void FromFirestore_StringToEnum_CaseInsensitive()
    {
        var result = _converter.FromFirestore("active", typeof(TestStatus));

        result.Should().Be(TestStatus.Active);
    }

    [Fact]
    public void FromFirestore_IntToInt_ReturnsUnchanged()
    {
        var result = _converter.FromFirestore(42, typeof(int));

        result.Should().Be(42);
    }

    [Fact]
    public void FromFirestore_LongToInt_ConvertsCorrectly()
    {
        // Firestore may return long for integers
        var result = _converter.FromFirestore(42L, typeof(int));

        result.Should().Be(42);
    }

    [Fact]
    public void FromFirestore_StringToString_ReturnsUnchanged()
    {
        var result = _converter.FromFirestore("hello", typeof(string));

        result.Should().Be("hello");
    }

    [Fact]
    public void FromFirestore_Null_ReturnsNull()
    {
        var result = _converter.FromFirestore(null, typeof(string));

        result.Should().BeNull();
    }

    [Fact]
    public void FromFirestore_ArrayOfDoublesToListOfDecimals_ReturnsListOfDecimals()
    {
        var doubles = new object[] { 1.1d, 2.2d, 3.3d };

        var result = _converter.FromFirestore(doubles, typeof(List<decimal>));

        result.Should().BeOfType<List<decimal>>();
        var list = (List<decimal>)result!;
        list.Should().HaveCount(3);
        list[0].Should().Be(1.1m);
    }

    [Fact]
    public void FromFirestore_ArrayOfStringsToListOfEnums_ReturnsListOfEnums()
    {
        var strings = new object[] { "Pending", "Active" };

        var result = _converter.FromFirestore(strings, typeof(List<TestStatus>));

        result.Should().BeOfType<List<TestStatus>>();
        var list = (List<TestStatus>)result!;
        list.Should().HaveCount(2);
        list[0].Should().Be(TestStatus.Pending);
        list[1].Should().Be(TestStatus.Active);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToFirestore_NullableDecimal_WithValue_ReturnsDouble()
    {
        decimal? value = 99.99m;
        var result = _converter.ToFirestore(value);

        result.Should().Be(99.99d);
    }

    [Fact]
    public void ToFirestore_NullableDecimal_Null_ReturnsNull()
    {
        decimal? value = null;
        var result = _converter.ToFirestore(value);

        result.Should().BeNull();
    }

    [Fact]
    public void FromFirestore_DoubleToNullableDecimal_ReturnsDecimal()
    {
        var result = _converter.FromFirestore(50.5d, typeof(decimal?));

        result.Should().Be(50.5m);
    }

    #endregion
}
