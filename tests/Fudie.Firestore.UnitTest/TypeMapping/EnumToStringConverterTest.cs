using FirestoreEnumConverter = Firestore.EntityFrameworkCore.Storage.EnumToStringConverter<Fudie.Firestore.UnitTest.TypeMapping.EnumToStringConverterTest.TestStatus>;
using FirestoreFlagsConverter = Firestore.EntityFrameworkCore.Storage.EnumToStringConverter<Fudie.Firestore.UnitTest.TypeMapping.EnumToStringConverterTest.TestFlags>;

namespace Fudie.Firestore.UnitTest.TypeMapping;

public class EnumToStringConverterTest
{
    public enum TestStatus
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    [Flags]
    public enum TestFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }

    [Fact]
    public void ConvertToProvider_Converts_Enum_To_String()
    {
        var converter = new FirestoreEnumConverter();

        var result = converter.ConvertToProvider!(TestStatus.Active);

        result.Should().Be("Active");
    }

    [Fact]
    public void ConvertFromProvider_Converts_String_To_Enum()
    {
        var converter = new FirestoreEnumConverter();

        var result = converter.ConvertFromProvider!("Completed");

        result.Should().Be(TestStatus.Completed);
    }

    [Theory]
    [InlineData(TestStatus.Pending, "Pending")]
    [InlineData(TestStatus.Active, "Active")]
    [InlineData(TestStatus.Completed, "Completed")]
    [InlineData(TestStatus.Cancelled, "Cancelled")]
    public void ConvertToProvider_Handles_All_Enum_Values(TestStatus input, string expected)
    {
        var converter = new FirestoreEnumConverter();

        var result = converter.ConvertToProvider!(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Pending", TestStatus.Pending)]
    [InlineData("Active", TestStatus.Active)]
    [InlineData("Completed", TestStatus.Completed)]
    [InlineData("Cancelled", TestStatus.Cancelled)]
    public void ConvertFromProvider_Handles_All_String_Values(string input, TestStatus expected)
    {
        var converter = new FirestoreEnumConverter();

        var result = converter.ConvertFromProvider!(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Roundtrip_Preserves_Value()
    {
        var converter = new FirestoreEnumConverter();
        var original = TestStatus.Completed;

        var toProvider = converter.ConvertToProvider!(original);
        var fromProvider = converter.ConvertFromProvider!(toProvider);

        fromProvider.Should().Be(original);
    }

    [Fact]
    public void ConvertFromProvider_Throws_On_Invalid_String()
    {
        var converter = new FirestoreEnumConverter();

        var action = () => converter.ConvertFromProvider!("InvalidValue");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConvertFromProvider_Is_Case_Sensitive()
    {
        var converter = new FirestoreEnumConverter();

        var action = () => converter.ConvertFromProvider!("active"); // lowercase

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ModelClrType_Is_Enum_Type()
    {
        var converter = new FirestoreEnumConverter();

        converter.ModelClrType.Should().Be(typeof(TestStatus));
    }

    [Fact]
    public void ProviderClrType_Is_String()
    {
        var converter = new FirestoreEnumConverter();

        converter.ProviderClrType.Should().Be(typeof(string));
    }

    [Fact]
    public void Works_With_Flags_Enum()
    {
        var converter = new FirestoreFlagsConverter();
        var flags = TestFlags.Read | TestFlags.Write;

        var toProvider = converter.ConvertToProvider!(flags);

        // Note: Combined flags result in "Read, Write" string
        toProvider.Should().Be("Read, Write");
    }

    [Fact]
    public void Roundtrip_Works_With_Flags_Enum()
    {
        var converter = new FirestoreFlagsConverter();
        var original = TestFlags.Read | TestFlags.Execute;

        var toProvider = converter.ConvertToProvider!(original);
        var fromProvider = converter.ConvertFromProvider!(toProvider);

        fromProvider.Should().Be(original);
    }
}
