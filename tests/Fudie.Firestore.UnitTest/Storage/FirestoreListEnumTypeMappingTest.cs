using Firestore.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreListEnumTypeMappingTest
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
        High
    }

    [Fact]
    public void Constructor_WithListEnum_ShouldCreateMapping()
    {
        // Arrange & Act
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));

        // Assert
        Assert.Equal(typeof(List<TestStatus>), mapping.ClrType);
    }

    [Fact]
    public void Constructor_WithListNullableEnum_ShouldCreateMapping()
    {
        // Arrange & Act
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus?>), typeof(TestStatus));

        // Assert
        Assert.Equal(typeof(List<TestStatus?>), mapping.ClrType);
    }

    [Fact]
    public void Constructor_ShouldHaveConverter()
    {
        // Arrange & Act
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));

        // Assert
        Assert.NotNull(mapping.Converter);
    }

    [Fact]
    public void Constructor_ShouldHaveComparer()
    {
        // Arrange & Act
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));

        // Assert
        Assert.NotNull(mapping.Comparer);
    }

    [Fact]
    public void Converter_ShouldConvertListEnumToListString()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var input = new List<TestStatus> { TestStatus.Pending, TestStatus.Active, TestStatus.Completed };

        // Act
        var result = converter.ConvertToProvider(input) as List<string>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Pending", result[0]);
        Assert.Equal("Active", result[1]);
        Assert.Equal("Completed", result[2]);
    }

    [Fact]
    public void Converter_ShouldConvertListStringToListEnum()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var input = new List<string> { "Pending", "Active", "Completed" };

        // Act
        var result = converter.ConvertFromProvider(input) as List<TestStatus>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(TestStatus.Pending, result[0]);
        Assert.Equal(TestStatus.Active, result[1]);
        Assert.Equal(TestStatus.Completed, result[2]);
    }

    [Fact]
    public void Converter_ShouldConvertEmptyList()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var input = new List<TestStatus>();

        // Act
        var result = converter.ConvertToProvider(input) as List<string>;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Converter_NullableEnum_ShouldConvertWithNulls()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus?>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var input = new List<TestStatus?> { TestStatus.Pending, null, TestStatus.Completed };

        // Act
        var result = converter.ConvertToProvider(input) as List<string?>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Pending", result[0]);
        Assert.Null(result[1]);
        Assert.Equal("Completed", result[2]);
    }

    [Fact]
    public void Converter_NullableEnum_ShouldConvertFromProviderWithNulls()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus?>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var input = new List<string?> { "Pending", null, "Completed" };

        // Act
        var result = converter.ConvertFromProvider(input) as List<TestStatus?>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(TestStatus.Pending, result[0]);
        Assert.Null(result[1]);
        Assert.Equal(TestStatus.Completed, result[2]);
    }

    [Fact]
    public void Comparer_ShouldReturnTrue_ForEqualLists()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var comparer = mapping.Comparer;
        var list1 = new List<TestStatus> { TestStatus.Pending, TestStatus.Active };
        var list2 = new List<TestStatus> { TestStatus.Pending, TestStatus.Active };

        // Act
        var result = comparer.Equals(list1, list2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Comparer_ShouldReturnFalse_ForDifferentLists()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var comparer = mapping.Comparer;
        var list1 = new List<TestStatus> { TestStatus.Pending, TestStatus.Active };
        var list2 = new List<TestStatus> { TestStatus.Pending, TestStatus.Completed };

        // Act
        var result = comparer.Equals(list1, list2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstance()
    {
        // Arrange
        var original = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));

        // Act
        var cloned = original.WithComposedConverter(null);

        // Assert
        Assert.NotNull(cloned);
        Assert.IsType<FirestoreListEnumTypeMapping>(cloned);
        Assert.Equal(original.ClrType, cloned.ClrType);
    }

    [Fact]
    public void Constructor_ShouldWorkWithDifferentEnumTypes()
    {
        // Arrange & Act
        var statusMapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var priorityMapping = new FirestoreListEnumTypeMapping(typeof(List<Priority>), typeof(Priority));

        // Assert
        Assert.Equal(typeof(List<TestStatus>), statusMapping.ClrType);
        Assert.Equal(typeof(List<Priority>), priorityMapping.ClrType);
    }

    [Fact]
    public void Converter_ShouldPreserveRoundTrip()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var original = new List<TestStatus> { TestStatus.Active, TestStatus.Completed, TestStatus.Pending };

        // Act
        var toString = converter.ConvertToProvider(original);
        var backToEnum = converter.ConvertFromProvider(toString) as List<TestStatus>;

        // Assert
        Assert.NotNull(backToEnum);
        Assert.Equal(original.Count, backToEnum.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i], backToEnum[i]);
        }
    }

    [Fact]
    public void EnumListConverter_ShouldConvertAllEnumValues()
    {
        // Arrange
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));
        var converter = mapping.Converter!;
        var allValues = Enum.GetValues(typeof(TestStatus)).Cast<TestStatus>().ToList();

        // Act
        var result = converter.ConvertToProvider(allValues) as List<string>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(allValues.Count, result.Count);
        foreach (var status in allValues)
        {
            Assert.Contains(status.ToString(), result);
        }
    }
}
