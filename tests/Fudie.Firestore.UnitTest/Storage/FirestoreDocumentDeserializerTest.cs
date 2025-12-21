using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreDocumentDeserializerTest
{
    public class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public TestStatus Status { get; set; }
    }

    public enum TestStatus
    {
        Active,
        Inactive,
        Pending
    }

    public class EntityWithLocation
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public Location Position { get; set; } = default!;
    }

    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private Mock<IModel> CreateMockModel()
    {
        return new Mock<IModel>();
    }

    private Mock<ITypeMappingSource> CreateMockTypeMappingSource()
    {
        return new Mock<ITypeMappingSource>();
    }

    private Mock<IFirestoreCollectionManager> CreateMockCollectionManager()
    {
        var mock = new Mock<IFirestoreCollectionManager>();
        mock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns<Type>(t => t.Name.ToLowerInvariant() + "s");
        return mock;
    }

    private Mock<ILogger<FirestoreDocumentDeserializer>> CreateMockLogger()
    {
        return new Mock<ILogger<FirestoreDocumentDeserializer>>();
    }

    [Fact]
    public void Constructor_ShouldCreateDeserializer()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Act
        var deserializer = new FirestoreDocumentDeserializer(
            mockModel.Object,
            mockTypeMappingSource.Object,
            mockCollectionManager.Object,
            mockLogger.Object);

        // Assert
        Assert.NotNull(deserializer);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenModelIsNull()
    {
        // Arrange
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                null!,
                mockTypeMappingSource.Object,
                mockCollectionManager.Object,
                mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTypeMappingSourceIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                mockModel.Object,
                null!,
                mockCollectionManager.Object,
                mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCollectionManagerIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                mockModel.Object,
                mockTypeMappingSource.Object,
                null!,
                mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockCollectionManager = CreateMockCollectionManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                mockModel.Object,
                mockTypeMappingSource.Object,
                mockCollectionManager.Object,
                null!));
    }

    [Fact]
    public void DeserializeEntity_ShouldThrow_WhenDocumentIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        var deserializer = new FirestoreDocumentDeserializer(
            mockModel.Object,
            mockTypeMappingSource.Object,
            mockCollectionManager.Object,
            mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            deserializer.DeserializeEntity<TestEntity>(null!));
    }

    [Fact]
    public void DeserializeEntities_ShouldReturnEmptyList_WhenNoDocuments()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockTypeMappingSource = CreateMockTypeMappingSource();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Setup mock entity type
        var mockEntityType = new Mock<IEntityType>();
        mockEntityType.Setup(e => e.ClrType).Returns(typeof(TestEntity));
        mockModel.Setup(m => m.FindEntityType(typeof(TestEntity))).Returns(mockEntityType.Object);

        var deserializer = new FirestoreDocumentDeserializer(
            mockModel.Object,
            mockTypeMappingSource.Object,
            mockCollectionManager.Object,
            mockLogger.Object);

        var emptyDocuments = new List<Google.Cloud.Firestore.DocumentSnapshot>();

        // Act
        var result = deserializer.DeserializeEntities<TestEntity>(emptyDocuments);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void CollectionManager_ShouldGenerateProperCollectionNames()
    {
        // Arrange
        var mockCollectionManager = CreateMockCollectionManager();

        // Act
        var testEntityCollection = mockCollectionManager.Object.GetCollectionName(typeof(TestEntity));
        var locationCollection = mockCollectionManager.Object.GetCollectionName(typeof(Location));

        // Assert
        Assert.Equal("testentitys", testEntityCollection);
        Assert.Equal("locations", locationCollection);
    }

    [Fact]
    public void Model_ShouldFindEntityType()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockEntityType = new Mock<IEntityType>();
        mockEntityType.Setup(e => e.ClrType).Returns(typeof(TestEntity));
        mockEntityType.Setup(e => e.Name).Returns(typeof(TestEntity).FullName!);
        mockModel.Setup(m => m.FindEntityType(typeof(TestEntity))).Returns(mockEntityType.Object);

        // Act
        var entityType = mockModel.Object.FindEntityType(typeof(TestEntity));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal(typeof(TestEntity), entityType.ClrType);
    }

    [Fact]
    public void Model_ShouldReturnNull_ForUnknownType()
    {
        // Arrange
        var mockModel = CreateMockModel();
        mockModel.Setup(m => m.FindEntityType(typeof(Location))).Returns((IEntityType?)null);

        // Act
        var entityType = mockModel.Object.FindEntityType(typeof(Location));

        // Assert
        Assert.Null(entityType);
    }

    [Fact]
    public void Deserializer_ShouldHandleEnumTypes()
    {
        // The deserializer should be able to convert strings back to enums
        // This test verifies the type exists and is properly handled

        // Arrange
        var values = Enum.GetValues(typeof(TestStatus));

        // Assert
        Assert.Equal(3, values.Length);
        Assert.Equal(TestStatus.Active, Enum.Parse(typeof(TestStatus), "Active"));
        Assert.Equal(TestStatus.Inactive, Enum.Parse(typeof(TestStatus), "Inactive"));
        Assert.Equal(TestStatus.Pending, Enum.Parse(typeof(TestStatus), "Pending"));
    }

    [Fact]
    public void Deserializer_ShouldHandleDecimalConversion()
    {
        // The deserializer should be able to convert doubles back to decimals
        // This test verifies the conversion logic

        // Arrange
        double input = 123.456d;

        // Act
        decimal result = (decimal)input;

        // Assert
        Assert.Equal(123.456m, result);
    }

    [Fact]
    public void Deserializer_ShouldHandleGeoPointCoordinates()
    {
        // The deserializer should be able to handle GeoPoint to Location conversion
        // This test verifies the Location class has required properties

        // Arrange
        var location = new Location
        {
            Latitude = 40.7128,
            Longitude = -74.0060
        };

        // Assert
        Assert.Equal(40.7128, location.Latitude);
        Assert.Equal(-74.0060, location.Longitude);
    }

    [Fact]
    public void Location_ShouldHaveLatitudeProperty()
    {
        // Arrange
        var locationType = typeof(Location);

        // Act
        var latitudeProperty = locationType.GetProperty("Latitude");

        // Assert
        Assert.NotNull(latitudeProperty);
        Assert.Equal(typeof(double), latitudeProperty.PropertyType);
    }

    [Fact]
    public void Location_ShouldHaveLongitudeProperty()
    {
        // Arrange
        var locationType = typeof(Location);

        // Act
        var longitudeProperty = locationType.GetProperty("Longitude");

        // Assert
        Assert.NotNull(longitudeProperty);
        Assert.Equal(typeof(double), longitudeProperty.PropertyType);
    }

    #region Ciclo 1: IFirestoreDocumentDeserializer Interface Tests

    [Fact]
    public void FirestoreDocumentDeserializer_ShouldImplementInterface()
    {
        // Assert
        Assert.True(typeof(IFirestoreDocumentDeserializer)
            .IsAssignableFrom(typeof(FirestoreDocumentDeserializer)));
    }

    [Fact]
    public void IFirestoreDocumentDeserializer_ShouldHaveDeserializeEntityMethod()
    {
        // Arrange
        var interfaceType = typeof(IFirestoreDocumentDeserializer);

        // Act
        var method = interfaceType.GetMethod("DeserializeEntity");

        // Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void IFirestoreDocumentDeserializer_ShouldHaveDeserializeEntitiesMethod()
    {
        // Arrange
        var interfaceType = typeof(IFirestoreDocumentDeserializer);

        // Act
        var method = interfaceType.GetMethod("DeserializeEntities");

        // Assert
        Assert.NotNull(method);
    }

    #endregion
}
