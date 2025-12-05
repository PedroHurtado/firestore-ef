using Firestore.EntityFrameworkCore.Infrastructure.Internal;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreCollectionManagerTest
{
    // Test entities
    private class Customer { }
    private class Category { }
    private class Address { }
    private class Bus { }
    private class Person { }
    private class Baby { }
    private class Key { }

    [Table("custom_orders")]
    private class Order { }

    [Table("productos")]
    private class Product { }

    private readonly Mock<ILogger<FirestoreCollectionManager>> _loggerMock;
    private readonly FirestoreCollectionManager _manager;

    public FirestoreCollectionManagerTest()
    {
        _loggerMock = new Mock<ILogger<FirestoreCollectionManager>>();
        _manager = new FirestoreCollectionManager(_loggerMock.Object);
    }

    [Fact]
    public void GetCollectionName_Throws_On_Null_EntityType()
    {
        var action = () => _manager.GetCollectionName(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("entityType");
    }

    [Fact]
    public void GetCollectionName_Returns_Table_Attribute_Name_When_Present()
    {
        var result = _manager.GetCollectionName(typeof(Order));

        result.Should().Be("custom_orders");
    }

    [Fact]
    public void GetCollectionName_Returns_Table_Attribute_With_Different_Name()
    {
        var result = _manager.GetCollectionName(typeof(Product));

        result.Should().Be("productos");
    }

    [Theory]
    [InlineData(typeof(Customer), "Customers")]
    [InlineData(typeof(Address), "Addresses")]
    [InlineData(typeof(Person), "Persons")]   // Simple pluralization (not irregular)
    public void GetCollectionName_Pluralizes_Simple_Names(Type entityType, string expected)
    {
        var result = _manager.GetCollectionName(entityType);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(Category), "Categories")]
    [InlineData(typeof(Baby), "Babies")]
    public void GetCollectionName_Pluralizes_Y_Ending_After_Consonant(Type entityType, string expected)
    {
        var result = _manager.GetCollectionName(entityType);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetCollectionName_Pluralizes_Y_Ending_After_Vowel()
    {
        var result = _manager.GetCollectionName(typeof(Key));

        result.Should().Be("Keys"); // 'e' is a vowel, so just add 's'
    }

    [Fact]
    public void GetCollectionName_Pluralizes_S_Ending()
    {
        var result = _manager.GetCollectionName(typeof(Bus));

        result.Should().Be("Buses");
    }

    [Fact]
    public void GetCollectionName_Caches_Results()
    {
        // First call
        var result1 = _manager.GetCollectionName(typeof(Customer));
        // Second call (should be from cache)
        var result2 = _manager.GetCollectionName(typeof(Customer));

        result1.Should().Be(result2);
        // Verify logging was only called once (on cache miss)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetCollectionName_Returns_Same_Value_For_Same_Type()
    {
        var results = Enumerable.Range(0, 10)
            .Select(_ => _manager.GetCollectionName(typeof(Customer)))
            .ToList();

        results.Should().AllBe("Customers");
    }

    [Fact]
    public void GetCollectionName_Handles_Multiple_Types()
    {
        var customerCollection = _manager.GetCollectionName(typeof(Customer));
        var orderCollection = _manager.GetCollectionName(typeof(Order));
        var categoryCollection = _manager.GetCollectionName(typeof(Category));

        customerCollection.Should().Be("Customers");
        orderCollection.Should().Be("custom_orders");
        categoryCollection.Should().Be("Categories");
    }
}
