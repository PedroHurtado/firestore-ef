using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreTypeMappingSourceTest
{
    public enum TestStatus
    {
        Active,
        Inactive
    }

    [Fact]
    public void FirestoreTypeMappingSource_ShouldInheritFromTypeMappingSource()
    {
        // Assert
        Assert.True(typeof(TypeMappingSource).IsAssignableFrom(typeof(FirestoreTypeMappingSource)));
    }

    [Fact]
    public void FirestoreTypeMappingSource_ShouldHaveFindMappingMethod()
    {
        // Assert
        var methods = typeof(FirestoreTypeMappingSource).GetMethods()
            .Where(m => m.Name == "FindMapping");
        Assert.NotEmpty(methods);
    }

    [Fact]
    public void FirestoreTypeMappingSource_Constructor_ShouldTakeTypeMappingSourceDependencies()
    {
        // Assert
        var constructors = typeof(FirestoreTypeMappingSource).GetConstructors();
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(TypeMappingSourceDependencies), parameters[0].ParameterType);
    }

    [Fact]
    public void FirestoreDecimalTypeMapping_ShouldExist()
    {
        // Assert
        Assert.True(typeof(RelationalTypeMapping).IsAssignableFrom(typeof(FirestoreDecimalTypeMapping)));
    }

    [Fact]
    public void FirestoreEnumTypeMapping_ShouldExist()
    {
        // Assert
        Assert.True(typeof(RelationalTypeMapping).IsAssignableFrom(typeof(FirestoreEnumTypeMapping)));
    }

    [Fact]
    public void FirestoreListDecimalTypeMapping_ShouldExist()
    {
        // Assert
        Assert.True(typeof(CoreTypeMapping).IsAssignableFrom(typeof(FirestoreListDecimalTypeMapping)));
    }

    [Fact]
    public void FirestoreListEnumTypeMapping_ShouldExist()
    {
        // Assert
        Assert.True(typeof(CoreTypeMapping).IsAssignableFrom(typeof(FirestoreListEnumTypeMapping)));
    }

    [Fact]
    public void FirestoreDecimalTypeMapping_Constructor_ShouldBeParameterless()
    {
        // Assert
        var constructor = typeof(FirestoreDecimalTypeMapping).GetConstructor(Type.EmptyTypes);
        Assert.NotNull(constructor);
    }

    [Fact]
    public void FirestoreEnumTypeMapping_Constructor_ShouldTakeEnumType()
    {
        // Assert
        var constructor = typeof(FirestoreEnumTypeMapping).GetConstructor(new[] { typeof(Type) });
        Assert.NotNull(constructor);
    }

    [Fact]
    public void FirestoreListDecimalTypeMapping_Constructor_ShouldTakeClrType()
    {
        // Assert
        var constructor = typeof(FirestoreListDecimalTypeMapping).GetConstructor(new[] { typeof(Type) });
        Assert.NotNull(constructor);
    }

    [Fact]
    public void FirestoreListEnumTypeMapping_Constructor_ShouldTakeClrTypeAndEnumType()
    {
        // Assert
        var constructor = typeof(FirestoreListEnumTypeMapping).GetConstructor(new[] { typeof(Type), typeof(Type) });
        Assert.NotNull(constructor);
    }

    [Fact]
    public void FirestoreDecimalTypeMapping_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var mapping = new FirestoreDecimalTypeMapping();

        // Assert
        Assert.NotNull(mapping);
        Assert.Equal(typeof(decimal), mapping.ClrType);
        Assert.Equal("number", mapping.StoreType);
    }

    [Fact]
    public void FirestoreEnumTypeMapping_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var mapping = new FirestoreEnumTypeMapping(typeof(TestStatus));

        // Assert
        Assert.NotNull(mapping);
        Assert.Equal(typeof(TestStatus), mapping.ClrType);
        Assert.Equal("string", mapping.StoreType);
    }

    [Fact]
    public void FirestoreListDecimalTypeMapping_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var mapping = new FirestoreListDecimalTypeMapping(typeof(List<decimal>));

        // Assert
        Assert.NotNull(mapping);
        Assert.Equal(typeof(List<decimal>), mapping.ClrType);
    }

    [Fact]
    public void FirestoreListEnumTypeMapping_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var mapping = new FirestoreListEnumTypeMapping(typeof(List<TestStatus>), typeof(TestStatus));

        // Assert
        Assert.NotNull(mapping);
        Assert.Equal(typeof(List<TestStatus>), mapping.ClrType);
    }
}
