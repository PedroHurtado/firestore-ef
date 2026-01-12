using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreDecimalTypeMappingTest
{
    [Fact]
    public void Constructor_ShouldCreateMapping_WithDecimalClrType()
    {
        // Arrange & Act
        var mapping = new FirestoreDecimalTypeMapping();

        // Assert
        Assert.Equal(typeof(decimal), mapping.ClrType);
    }

    [Fact]
    public void Constructor_ShouldCreateMapping_WithNumberStoreType()
    {
        // Arrange & Act
        var mapping = new FirestoreDecimalTypeMapping();

        // Assert
        Assert.Equal("number", mapping.StoreType);
    }

    [Fact]
    public void Constructor_ShouldCreateMapping_WithDecimalToDoubleConverter()
    {
        // Arrange & Act
        var mapping = new FirestoreDecimalTypeMapping();

        // Assert
        Assert.NotNull(mapping.Converter);
        Assert.IsType<DecimalToDoubleConverter>(mapping.Converter);
    }

    [Fact]
    public void Converter_ShouldConvertDecimalToDouble()
    {
        // Arrange
        var mapping = new FirestoreDecimalTypeMapping();
        var converter = mapping.Converter!;
        decimal inputValue = 123.456m;

        // Act
        var result = converter.ConvertToProvider(inputValue);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(123.456d, (double)result!, precision: 10);
    }

    [Fact]
    public void Converter_ShouldConvertDoubleToDecimal()
    {
        // Arrange
        var mapping = new FirestoreDecimalTypeMapping();
        var converter = mapping.Converter!;
        double inputValue = 123.456d;

        // Act
        var result = converter.ConvertFromProvider(inputValue);

        // Assert
        Assert.IsType<decimal>(result);
        Assert.Equal(123.456m, (decimal)result!);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstance_WithSameParameters()
    {
        // Arrange
        var original = new FirestoreDecimalTypeMapping();

        // Act - Clone is called internally through WithComposedConverter
        var cloned = original.WithComposedConverter(null);

        // Assert
        Assert.NotNull(cloned);
        Assert.IsType<FirestoreDecimalTypeMapping>(cloned);
        Assert.Equal(original.ClrType, cloned.ClrType);
        // StoreType is only available on RelationalTypeMapping
        var clonedRelational = cloned as RelationalTypeMapping;
        Assert.NotNull(clonedRelational);
        Assert.Equal(original.StoreType, clonedRelational.StoreType);
    }

    [Fact]
    public void DbType_ShouldBeNull_BecauseFirestoreUsesDouble()
    {
        // Arrange & Act
        var mapping = new FirestoreDecimalTypeMapping();

        // Assert
        Assert.Null(mapping.DbType);
    }

    [Fact]
    public void Converter_ShouldHandleZeroValue()
    {
        // Arrange
        var mapping = new FirestoreDecimalTypeMapping();
        var converter = mapping.Converter!;

        // Act
        var toProvider = converter.ConvertToProvider(0m);
        var fromProvider = converter.ConvertFromProvider(0d);

        // Assert
        Assert.Equal(0d, toProvider);
        Assert.Equal(0m, fromProvider);
    }

    [Fact]
    public void Converter_ShouldHandleNegativeValues()
    {
        // Arrange
        var mapping = new FirestoreDecimalTypeMapping();
        var converter = mapping.Converter!;
        decimal negativeDecimal = -999.99m;

        // Act
        var toProvider = converter.ConvertToProvider(negativeDecimal);
        var fromProvider = converter.ConvertFromProvider(-999.99d);

        // Assert
        Assert.Equal(-999.99d, (double)toProvider!);
        Assert.Equal(-999.99m, (decimal)fromProvider!);
    }

    [Fact]
    public void Converter_ShouldHandleLargeValues()
    {
        // Arrange
        var mapping = new FirestoreDecimalTypeMapping();
        var converter = mapping.Converter!;
        decimal largeDecimal = 1_000_000_000.123456m;

        // Act
        var toProvider = converter.ConvertToProvider(largeDecimal);

        // Assert
        Assert.IsType<double>(toProvider);
        // Note: Some precision may be lost when converting large decimals to double
        Assert.True(Math.Abs((double)toProvider! - 1_000_000_000.123456d) < 0.001);
    }
}
