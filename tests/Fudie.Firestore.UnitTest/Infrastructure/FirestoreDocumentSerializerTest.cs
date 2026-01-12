using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreDocumentSerializerTest
{
    public class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Age { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public TestStatus Status { get; set; }
    }

    public enum TestStatus
    {
        Active,
        Inactive
    }

    private FirestoreDocumentSerializer CreateSerializer()
    {
        var mockLogger = new Mock<ILogger<FirestoreDocumentSerializer>>();
        return new FirestoreDocumentSerializer(mockLogger.Object);
    }

    [Fact]
    public void FirestoreDocumentSerializer_ShouldImplementIFirestoreDocumentSerializer()
    {
        // Assert
        Assert.True(typeof(IFirestoreDocumentSerializer).IsAssignableFrom(typeof(FirestoreDocumentSerializer)));
    }

    [Fact]
    public void Serialize_ShouldThrow_WhenEntityIsNull()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null!));
    }

    [Fact]
    public void Serialize_ShouldReturnDictionary()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new TestEntity
        {
            Id = "123",
            Name = "Test",
            Age = 25
        };

        // Act
        var result = serializer.Serialize(entity);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object>>(result);
    }

    [Fact]
    public void Serialize_ShouldIncludeAllProperties()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new TestEntity
        {
            Id = "123",
            Name = "Test",
            Age = 25,
            Price = 99.99m,
            CreatedAt = DateTime.UtcNow,
            Status = TestStatus.Active
        };

        // Act
        var result = serializer.Serialize(entity);

        // Assert
        Assert.True(result.ContainsKey("Id"));
        Assert.True(result.ContainsKey("Name"));
        Assert.True(result.ContainsKey("Age"));
        Assert.True(result.ContainsKey("Price"));
        Assert.True(result.ContainsKey("CreatedAt"));
        Assert.True(result.ContainsKey("Status"));
    }

    [Fact]
    public void Serialize_ShouldConvertDecimalToDouble()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new TestEntity
        {
            Id = "123",
            Name = "Test",
            Price = 99.99m
        };

        // Act
        var result = serializer.Serialize(entity);

        // Assert
        Assert.IsType<double>(result["Price"]);
        Assert.Equal(99.99d, (double)result["Price"], precision: 2);
    }

    [Fact]
    public void Serialize_ShouldConvertEnumToString()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new TestEntity
        {
            Id = "123",
            Name = "Test",
            Status = TestStatus.Active
        };

        // Act
        var result = serializer.Serialize(entity);

        // Assert
        Assert.IsType<string>(result["Status"]);
        Assert.Equal("Active", result["Status"]);
    }

    [Fact]
    public void Serialize_ShouldNotIncludeNullValues()
    {
        // Arrange
        var serializer = CreateSerializer();
        var entity = new TestEntity
        {
            Id = "123",
            Name = null!
        };

        // Act
        var result = serializer.Serialize(entity);

        // Assert
        Assert.True(result.ContainsKey("Id"));
        Assert.False(result.ContainsKey("Name"));
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenDocumentIsNull()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            serializer.Deserialize<TestEntity>(null!));
    }

    [Fact]
    public void Deserialize_ShouldReturnEntity()
    {
        // Arrange
        var serializer = CreateSerializer();
        var document = new Dictionary<string, object>
        {
            { "Id", "123" },
            { "Name", "Test" },
            { "Age", 25 }
        };

        // Act
        var result = serializer.Deserialize<TestEntity>(document);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestEntity>(result);
    }

    [Fact]
    public void Deserialize_ShouldMapProperties()
    {
        // Arrange
        var serializer = CreateSerializer();
        var document = new Dictionary<string, object>
        {
            { "Id", "123" },
            { "Name", "Test" },
            { "Age", 25 }
        };

        // Act
        var result = serializer.Deserialize<TestEntity>(document);

        // Assert
        Assert.Equal("123", result.Id);
        Assert.Equal("Test", result.Name);
        Assert.Equal(25, result.Age);
    }

    [Fact]
    public void Deserialize_ShouldHandleMissingProperties()
    {
        // Arrange
        var serializer = CreateSerializer();
        var document = new Dictionary<string, object>
        {
            { "Id", "123" }
        };

        // Act
        var result = serializer.Deserialize<TestEntity>(document);

        // Assert
        Assert.Equal("123", result.Id);
        Assert.Null(result.Name);
        Assert.Equal(0, result.Age);
    }

    [Fact]
    public void Deserialize_ShouldConvertEnumFromString()
    {
        // Arrange
        var serializer = CreateSerializer();
        var document = new Dictionary<string, object>
        {
            { "Id", "123" },
            { "Status", "Inactive" }
        };

        // Act
        var result = serializer.Deserialize<TestEntity>(document);

        // Assert
        Assert.Equal(TestStatus.Inactive, result.Status);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var serializer = CreateSerializer();
        var original = new TestEntity
        {
            Id = "test-id",
            Name = "Test Name",
            Age = 30,
            Price = 123.45m,
            Status = TestStatus.Active
        };

        // Act
        var serialized = serializer.Serialize(original);
        var deserialized = serializer.Deserialize<TestEntity>(serialized);

        // Assert
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
        Assert.Equal(original.Status, deserialized.Status);
    }

    [Fact]
    public void Constructor_ShouldRequireLogger()
    {
        // Assert
        var constructors = typeof(FirestoreDocumentSerializer).GetConstructors();
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ILogger<FirestoreDocumentSerializer>), parameters[0].ParameterType);
    }
}
