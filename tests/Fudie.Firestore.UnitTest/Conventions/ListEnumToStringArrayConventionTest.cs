using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Metadata.Converters;

namespace Fudie.Firestore.UnitTest.Conventions;

public class ListEnumToStringArrayConventionTest
{
    public enum TestStatus { Pending, Active, Completed }

    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_For_List_Enum()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<TestStatus>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_For_List_Nullable_Enum()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<TestStatus?>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeTrue();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_List_String()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<string>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_List_Int()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(List<int>));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Apply_Converter_For_Single_Enum()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(typeof(TestStatus));

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ProcessPropertyAdded_Does_Not_Override_Existing_Converter()
    {
        // Arrange
        var convention = new ListEnumToStringArrayConvention();
        var (propertyBuilder, context, wasCalled) = CreatePropertyBuilderMock(
            typeof(List<TestStatus>),
            hasExistingConverter: true);

        // Act
        convention.ProcessPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public void ListEnumToStringConverter_Converts_List_Enum_To_List_String()
    {
        // Arrange
        var converter = new ListEnumToStringConverter<TestStatus>();
        var input = new List<TestStatus> { TestStatus.Pending, TestStatus.Active, TestStatus.Completed };

        // Act
        var result = converter.ConvertToProvider!(input);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "Pending", "Active", "Completed" });
    }

    [Fact]
    public void ListEnumToStringConverter_Converts_List_String_To_List_Enum()
    {
        // Arrange
        var converter = new ListEnumToStringConverter<TestStatus>();
        var input = new List<string> { "Pending", "Active", "Completed" };

        // Act
        var result = converter.ConvertFromProvider!(input);

        // Assert
        result.Should().BeEquivalentTo(new List<TestStatus>
        {
            TestStatus.Pending,
            TestStatus.Active,
            TestStatus.Completed
        });
    }

    [Fact]
    public void ListNullableEnumToStringConverter_Converts_List_With_Nulls()
    {
        // Arrange
        var converter = new ListNullableEnumToStringConverter<TestStatus>();
        var input = new List<TestStatus?> { TestStatus.Pending, null, TestStatus.Completed };

        // Act
        var result = converter.ConvertToProvider!(input);

        // Assert
        result.Should().BeEquivalentTo(new List<string?> { "Pending", null, "Completed" });
    }

    [Fact]
    public void ListNullableEnumToStringConverter_Roundtrip_Preserves_Values()
    {
        // Arrange
        var converter = new ListNullableEnumToStringConverter<TestStatus>();
        var original = new List<TestStatus?> { TestStatus.Active, null, TestStatus.Pending };

        // Act
        var toProvider = converter.ConvertToProvider!(original);
        var fromProvider = converter.ConvertFromProvider!(toProvider);

        // Assert
        fromProvider.Should().BeEquivalentTo(original);
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
