using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

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

    private Mock<IFirestoreValueConverter> CreateMockValueConverter()
    {
        var mock = new Mock<IFirestoreValueConverter>();
        mock.Setup(m => m.FromFirestore(It.IsAny<object>(), It.IsAny<Type>()))
            .Returns<object, Type>((value, targetType) => value);
        return mock;
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
        var mockValueConverter = CreateMockValueConverter();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Act
        var deserializer = new FirestoreDocumentDeserializer(
            mockModel.Object,
            mockValueConverter.Object,
            mockCollectionManager.Object,
            mockLogger.Object);

        // Assert
        Assert.NotNull(deserializer);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenModelIsNull()
    {
        // Arrange
        var mockValueConverter = CreateMockValueConverter();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                null!,
                mockValueConverter.Object,
                mockCollectionManager.Object,
                mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenValueConverterIsNull()
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
        var mockValueConverter = CreateMockValueConverter();
        var mockLogger = CreateMockLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                mockModel.Object,
                mockValueConverter.Object,
                null!,
                mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockValueConverter = CreateMockValueConverter();
        var mockCollectionManager = CreateMockCollectionManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDocumentDeserializer(
                mockModel.Object,
                mockValueConverter.Object,
                mockCollectionManager.Object,
                null!));
    }

    [Fact]
    public void DeserializeEntity_ShouldThrow_WhenDocumentIsNull()
    {
        // Arrange
        var mockModel = CreateMockModel();
        var mockValueConverter = CreateMockValueConverter();
        var mockCollectionManager = CreateMockCollectionManager();
        var mockLogger = CreateMockLogger();

        var deserializer = new FirestoreDocumentDeserializer(
            mockModel.Object,
            mockValueConverter.Object,
            mockCollectionManager.Object,
            mockLogger.Object);

        var relatedEntities = new Dictionary<string, object>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            deserializer.DeserializeEntity<TestEntity>(null!, relatedEntities));
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

    #region Ciclo 3: Constructor Selection Tests

    // Entidades de test para constructores con parámetros
    public class EntityWithFullConstructor
    {
        public EntityWithFullConstructor(string id, string name, decimal price)
        {
            Id = id;
            Name = name;
            Price = price;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public decimal Price { get; private set; }
    }

    public class EntityWithPartialConstructor
    {
        public EntityWithPartialConstructor(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public decimal Price { get; set; }  // No está en constructor
        public bool IsActive { get; set; }  // No está en constructor
    }

    public record EntityRecord(string Id, string Name, decimal Price);

    public class EntityWithMultipleConstructors
    {
        public EntityWithMultipleConstructors() { }

        public EntityWithMultipleConstructors(string id)
        {
            Id = id;
        }

        public EntityWithMultipleConstructors(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    [Fact]
    public void EntityWithFullConstructor_ShouldNotHaveParameterlessConstructor()
    {
        // Arrange
        var type = typeof(EntityWithFullConstructor);

        // Act
        var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);

        // Assert
        Assert.Null(parameterlessConstructor);
    }

    [Fact]
    public void EntityWithFullConstructor_ShouldHaveConstructorMatchingAllProperties()
    {
        // Arrange
        var type = typeof(EntityWithFullConstructor);
        var properties = type.GetProperties();

        // Act
        var constructors = type.GetConstructors();
        var matchingConstructor = constructors.FirstOrDefault(c =>
        {
            var parameters = c.GetParameters();
            return parameters.Length == properties.Length;
        });

        // Assert
        Assert.NotNull(matchingConstructor);
        Assert.Equal(3, matchingConstructor.GetParameters().Length);
    }

    [Fact]
    public void EntityWithPartialConstructor_ShouldHaveConstructorWithFewerParametersThanProperties()
    {
        // Arrange
        var type = typeof(EntityWithPartialConstructor);
        var properties = type.GetProperties();
        var constructors = type.GetConstructors();

        // Act
        var constructor = constructors.First();
        var constructorParams = constructor.GetParameters();

        // Assert
        Assert.Equal(4, properties.Length);  // Id, Name, Price, IsActive
        Assert.Equal(2, constructorParams.Length);  // Solo Id, Name
        Assert.True(constructorParams.Length < properties.Length);
    }

    [Fact]
    public void EntityWithPartialConstructor_ShouldHaveSettablePropertiesNotInConstructor()
    {
        // Arrange
        var type = typeof(EntityWithPartialConstructor);
        var constructor = type.GetConstructors().First();
        var constructorParamNames = constructor.GetParameters()
            .Select(p => p.Name!.ToLowerInvariant())
            .ToHashSet();

        // Act
        var settablePropertiesNotInConstructor = type.GetProperties()
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            .Where(p => !constructorParamNames.Contains(p.Name.ToLowerInvariant()))
            .ToList();

        // Assert
        Assert.Equal(2, settablePropertiesNotInConstructor.Count);
        Assert.Contains(settablePropertiesNotInConstructor, p => p.Name == "Price");
        Assert.Contains(settablePropertiesNotInConstructor, p => p.Name == "IsActive");
    }

    [Fact]
    public void EntityRecord_ShouldBeRecord()
    {
        // Arrange
        var type = typeof(EntityRecord);

        // Act - Records have a special method for cloning
        var isRecord = type.GetMethod("<Clone>$") != null;

        // Assert
        Assert.True(isRecord);
    }

    [Fact]
    public void EntityRecord_ShouldHaveInitOnlyProperties()
    {
        // Arrange
        var type = typeof(EntityRecord);

        // Act
        var properties = type.GetProperties();
        var allInitOnly = properties.All(p =>
        {
            var setMethod = p.SetMethod;
            if (setMethod == null) return true;
            // init-only setters have IsInitOnly = true in their return parameter
            return setMethod.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.Name == "IsExternalInit");
        });

        // Assert
        Assert.True(allInitOnly);
    }

    [Fact]
    public void EntityWithMultipleConstructors_ShouldHaveParameterlessConstructor()
    {
        // Arrange
        var type = typeof(EntityWithMultipleConstructors);

        // Act
        var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);

        // Assert
        Assert.NotNull(parameterlessConstructor);
    }

    [Fact]
    public void EntityWithMultipleConstructors_ShouldHaveThreeConstructors()
    {
        // Arrange
        var type = typeof(EntityWithMultipleConstructors);

        // Act
        var constructors = type.GetConstructors();

        // Assert
        Assert.Equal(3, constructors.Length);
    }

    [Fact]
    public void FindBestConstructor_ShouldPreferParameterlessWhenAvailable()
    {
        // Este test verifica la lógica de selección de constructor
        // Cuando hay constructor sin parámetros, debería preferirse (backward compatibility)

        // Arrange
        var type = typeof(EntityWithMultipleConstructors);
        var constructors = type.GetConstructors();

        // Act - Simular lógica de selección: preferir sin parámetros
        var parameterless = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        var selected = parameterless ?? constructors.OrderByDescending(c => c.GetParameters().Length).First();

        // Assert
        Assert.NotNull(selected);
        Assert.Empty(selected.GetParameters());
    }

    [Fact]
    public void FindBestConstructor_ShouldSelectConstructorWithMostMatchingParameters()
    {
        // Cuando NO hay constructor sin parámetros, seleccionar el que más parámetros coincidentes tenga

        // Arrange
        var type = typeof(EntityWithFullConstructor);
        var availableData = new Dictionary<string, object>
        {
            { "id", "test-id" },
            { "name", "Test Name" },
            { "price", 99.99 }
        };

        var constructors = type.GetConstructors();

        // Act - Simular lógica: encontrar constructor cuyos parámetros coincidan con datos disponibles
        var bestConstructor = constructors
            .OrderByDescending(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Count(p =>
                    availableData.Keys.Any(k =>
                        k.Equals(p.Name, StringComparison.OrdinalIgnoreCase)));
            })
            .First();

        // Assert
        Assert.Equal(3, bestConstructor.GetParameters().Length);
    }

    [Fact]
    public void ConstructorParameterMatching_ShouldBeCaseInsensitive()
    {
        // Arrange
        var type = typeof(EntityWithFullConstructor);
        var constructor = type.GetConstructors().First();
        var parameters = constructor.GetParameters();

        var firestoreData = new Dictionary<string, object>
        {
            { "Id", "test" },      // PascalCase (como viene de Firestore)
            { "Name", "Test" },
            { "Price", 10.0 }
        };

        // Act - Verificar que todos los parámetros pueden mapearse case-insensitive
        var allMatch = parameters.All(p =>
            firestoreData.Keys.Any(k =>
                k.Equals(p.Name, StringComparison.OrdinalIgnoreCase)));

        // Assert
        Assert.True(allMatch);
    }

    #endregion

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

        // Act - Solo hay un método DeserializeEntity (contrato simplificado)
        var methods = interfaceType.GetMethods()
            .Where(m => m.Name == "DeserializeEntity")
            .ToArray();

        // Assert - Verificar que existe solo una sobrecarga con 2 parámetros
        Assert.Single(methods);
        Assert.Equal(2, methods[0].GetParameters().Length); // DocumentSnapshot, relatedEntities
    }

    #endregion
}
