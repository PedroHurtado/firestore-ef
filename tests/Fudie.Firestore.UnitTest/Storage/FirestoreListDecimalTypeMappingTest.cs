using Firestore.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreListDecimalTypeMappingTest
{
    [Fact]
    public void Constructor_WithListDecimal_ShouldCreateMapping()
    {
        // Arrange & Act
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));

        // Assert
        Assert.Equal(typeof(List<decimal>), mapping.ClrType);
    }

    [Fact]
    public void Constructor_WithListNullableDecimal_ShouldCreateMapping()
    {
        // Arrange & Act
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal?>));

        // Assert
        Assert.Equal(typeof(List<decimal?>), mapping.ClrType);
    }

    [Fact]
    public void Constructor_ShouldHaveConverter()
    {
        // Arrange & Act
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));

        // Assert
        Assert.NotNull(mapping.Converter);
    }

    [Fact]
    public void Constructor_ShouldHaveComparer()
    {
        // Arrange & Act
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));

        // Assert
        Assert.NotNull(mapping.Comparer);
    }

    [Fact]
    public void Converter_ShouldConvertListDecimalToListDouble()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var converter = mapping.Converter!;
        var input = new List<decimal> { 1.1m, 2.2m, 3.3m };

        // Act
        var result = converter.ConvertToProvider(input) as List<double>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1d, result[0], precision: 10);
        Assert.Equal(2.2d, result[1], precision: 10);
        Assert.Equal(3.3d, result[2], precision: 10);
    }

    [Fact]
    public void Converter_ShouldConvertListDoubleToListDecimal()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var converter = mapping.Converter!;
        var input = new List<double> { 1.1d, 2.2d, 3.3d };

        // Act
        var result = converter.ConvertFromProvider(input) as List<decimal>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1m, result[0]);
        Assert.Equal(2.2m, result[1]);
        Assert.Equal(3.3m, result[2]);
    }

    [Fact]
    public void Converter_ShouldConvertEmptyList()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var converter = mapping.Converter!;
        var input = new List<decimal>();

        // Act
        var result = converter.ConvertToProvider(input) as List<double>;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Converter_NullableDecimal_ShouldConvertWithNulls()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal?>));
        var converter = mapping.Converter!;
        var input = new List<decimal?> { 1.1m, null, 3.3m };

        // Act
        var result = converter.ConvertToProvider(input) as List<double?>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1d, result[0]);
        Assert.Null(result[1]);
        Assert.Equal(3.3d, result[2]);
    }

    [Fact]
    public void Converter_NullableDecimal_ShouldConvertFromProviderWithNulls()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal?>));
        var converter = mapping.Converter!;
        var input = new List<double?> { 1.1d, null, 3.3d };

        // Act
        var result = converter.ConvertFromProvider(input) as List<decimal?>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1m, result[0]);
        Assert.Null(result[1]);
        Assert.Equal(3.3m, result[2]);
    }

    [Fact]
    public void Comparer_ShouldReturnTrue_ForEqualLists()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var comparer = mapping.Comparer;
        var list1 = new List<decimal> { 1.1m, 2.2m, 3.3m };
        var list2 = new List<decimal> { 1.1m, 2.2m, 3.3m };

        // Act
        var result = comparer.Equals(list1, list2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Comparer_ShouldReturnFalse_ForDifferentLists()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var comparer = mapping.Comparer;
        var list1 = new List<decimal> { 1.1m, 2.2m, 3.3m };
        var list2 = new List<decimal> { 1.1m, 2.2m, 4.4m };

        // Act
        var result = comparer.Equals(list1, list2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Comparer_ShouldReturnFalse_ForDifferentSizedLists()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var comparer = mapping.Comparer;
        var list1 = new List<decimal> { 1.1m, 2.2m };
        var list2 = new List<decimal> { 1.1m, 2.2m, 3.3m };

        // Act
        var result = comparer.Equals(list1, list2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstance()
    {
        // Arrange
        var original = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));

        // Act
        var cloned = original.WithComposedConverter(null);

        // Assert
        Assert.NotNull(cloned);
        Assert.IsType<FirestoreListDecimalTypeMapping>(cloned);
        Assert.Equal(original.ClrType, cloned.ClrType);
    }

    [Fact]
    public void Constructor_WithUnsupportedType_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new FirestoreListDecimalTypeMapping(typeof(List<int>)));
    }

    [Fact]
    public void Converter_ShouldPreserveRoundTrip()
    {
        // Arrange
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));
        var converter = mapping.Converter!;
        var original = new List<decimal> { 123.456m, 789.012m, 0.001m };

        // Act
        var toDouble = converter.ConvertToProvider(original);
        var backToDecimal = converter.ConvertFromProvider(toDouble) as List<decimal>;

        // Assert
        Assert.NotNull(backToDecimal);
        Assert.Equal(original.Count, backToDecimal.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i], backToDecimal[i]);
        }
    }
}
