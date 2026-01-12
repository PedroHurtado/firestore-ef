using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreEnumTypeMappingTest
{
    public enum TestStatus
    {
        Pending,
        Active,
        Completed
    }

    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }

    [Fact]
    public void Constructor_ShouldCreateMapping_WithEnumClrType()
    {
        // Arrange & Act
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));

        // Assert
        Assert.Equal(typeof(TestStatus), mapping.ClrType);
    }

    [Fact]
    public void Constructor_ShouldCreateMapping_WithStringStoreType()
    {
        // Arrange & Act
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));

        // Assert
        Assert.Equal("string", mapping.StoreType);
    }

    [Fact]
    public void Constructor_ShouldCreateMapping_WithEnumToStringConverter()
    {
        // Arrange & Act
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));

        // Assert
        Assert.NotNull(mapping.Converter);
    }

    [Fact]
    public void Converter_ShouldConvertEnumToString()
    {
        // Arrange
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));
        var converter = mapping.Converter!;

        // Act
        var result = converter.ConvertToProvider(TestStatus.Active);

        // Assert
        Assert.Equal("Active", result);
    }

    [Fact]
    public void Converter_ShouldConvertStringToEnum()
    {
        // Arrange
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));
        var converter = mapping.Converter!;

        // Act
        var result = converter.ConvertFromProvider("Completed");

        // Assert
        Assert.Equal(TestStatus.Completed, result);
    }

    [Fact]
    public void Converter_ShouldHandleAllEnumValues()
    {
        // Arrange
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));
        var converter = mapping.Converter!;

        // Act & Assert
        foreach (TestStatus status in Enum.GetValues(typeof(TestStatus)))
        {
            var toString = converter.ConvertToProvider(status);
            var fromString = converter.ConvertFromProvider(status.ToString());

            Assert.Equal(status.ToString(), toString);
            Assert.Equal(status, fromString);
        }
    }

    [Fact]
    public void Clone_ShouldReturnNewInstance_WithSameEnumType()
    {
        // Arrange
        var original = new FirestoreEnumTypeMapping(typeof(Priority));

        // Act
        var cloned = original.WithComposedConverter(null);

        // Assert
        Assert.NotNull(cloned);
        Assert.IsType<FirestoreEnumTypeMapping>(cloned);
        Assert.Equal(original.ClrType, cloned.ClrType);
    }

    [Fact]
    public void Constructor_ShouldWorkWithDifferentEnumTypes()
    {
        // Arrange & Act
        var statusMapping = new FirestoreEnumTypeMapping(typeof(TestStatus));
        var priorityMapping = new FirestoreEnumTypeMapping(typeof(Priority));

        // Assert
        Assert.Equal(typeof(TestStatus), statusMapping.ClrType);
        Assert.Equal(typeof(Priority), priorityMapping.ClrType);
        Assert.NotEqual(statusMapping.Converter, priorityMapping.Converter);
    }

    [Fact]
    public void DbType_ShouldBeNull()
    {
        // Arrange & Act
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));

        // Assert
        Assert.Null(mapping.DbType);
    }

    [Fact]
    public void Converter_ShouldConvertFirstEnumValue()
    {
        // Arrange
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));
        var converter = mapping.Converter!;

        // Act
        var result = converter.ConvertToProvider(TestStatus.Pending);

        // Assert
        Assert.Equal("Pending", result);
    }

    [Fact]
    public void Converter_ShouldPreserveRoundTrip()
    {
        // Arrange
        var mapping = new FirestoreEnumTypeMapping(typeof(Priority));
        var converter = mapping.Converter!;
        var originalValue = Priority.Critical;

        // Act
        var stringValue = converter.ConvertToProvider(originalValue);
        var backToEnum = converter.ConvertFromProvider(stringValue);

        // Assert
        Assert.Equal(originalValue, backToEnum);
    }
}
