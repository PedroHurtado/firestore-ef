using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreDatabaseTest
{
    public class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    public class EntityWithIntId
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Fact]
    public void FirestoreDatabase_ShouldInheritFromDatabase()
    {
        // Assert - verify that FirestoreDatabase inherits from Database
        Assert.True(typeof(Microsoft.EntityFrameworkCore.Storage.Database).IsAssignableFrom(typeof(FirestoreDatabase)));
    }

    [Fact]
    public void FirestoreDatabase_ShouldHaveSaveChangesMethod()
    {
        // Assert - verify that FirestoreDatabase has SaveChanges method
        var method = typeof(FirestoreDatabase).GetMethod("SaveChanges");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreDatabase_ShouldHaveSaveChangesAsyncMethod()
    {
        // Assert - verify that FirestoreDatabase has SaveChangesAsync method
        var method = typeof(FirestoreDatabase).GetMethod("SaveChangesAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void IdGenerator_Interface_ShouldHaveGenerateIdMethod()
    {
        // Assert - verify IFirestoreIdGenerator interface
        var method = typeof(IFirestoreIdGenerator).GetMethod("GenerateId");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void CollectionManager_Interface_ShouldHaveGetCollectionNameMethod()
    {
        // Assert - verify IFirestoreCollectionManager interface
        var method = typeof(IFirestoreCollectionManager).GetMethod("GetCollectionName");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void IdGenerator_Mock_ShouldGenerateUniqueIds()
    {
        // Arrange
        var mockIdGenerator = new Mock<IFirestoreIdGenerator>();
        mockIdGenerator.Setup(g => g.GenerateId()).Returns(() => Guid.NewGuid().ToString());
        var ids = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            ids.Add(mockIdGenerator.Object.GenerateId());
        }

        // Assert
        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public void CollectionManager_Mock_ShouldGenerateCollectionNames()
    {
        // Arrange
        var mockCollectionManager = new Mock<IFirestoreCollectionManager>();
        mockCollectionManager.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
            .Returns<Type>(t => t.Name.ToLowerInvariant() + "s");

        // Act
        var testEntityCollection = mockCollectionManager.Object.GetCollectionName(typeof(TestEntity));
        var entityWithIntIdCollection = mockCollectionManager.Object.GetCollectionName(typeof(EntityWithIntId));

        // Assert
        Assert.Equal("testentitys", testEntityCollection);
        Assert.Equal("entitywithintids", entityWithIntIdCollection);
    }

    [Fact]
    public void ClientWrapper_Interface_ShouldHaveDatabaseProperty()
    {
        // Assert - verify IFirestoreClientWrapper interface
        var property = typeof(IFirestoreClientWrapper).GetProperty("Database");
        Assert.NotNull(property);
    }

    [Fact]
    public void ClientWrapper_Interface_ShouldHaveCreateBatchMethod()
    {
        // Assert - verify IFirestoreClientWrapper interface
        var method = typeof(IFirestoreClientWrapper).GetMethod("CreateBatch");
        Assert.NotNull(method);
    }

    [Fact]
    public void ClientWrapper_Interface_ShouldHaveSetDocumentAsyncMethod()
    {
        // Assert - verify IFirestoreClientWrapper interface
        var method = typeof(IFirestoreClientWrapper).GetMethod("SetDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void ClientWrapper_Interface_ShouldHaveDeleteDocumentAsyncMethod()
    {
        // Assert - verify IFirestoreClientWrapper interface
        var method = typeof(IFirestoreClientWrapper).GetMethod("DeleteDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void DocumentSerializer_Interface_ShouldExist()
    {
        // Assert - verify IFirestoreDocumentSerializer interface exists
        Assert.True(typeof(IFirestoreDocumentSerializer).IsInterface);
    }

    [Fact]
    public void FirestoreDatabase_Constructor_ShouldHaveCorrectParameterCount()
    {
        // Assert - verify constructor parameters
        var constructors = typeof(FirestoreDatabase).GetConstructors();
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Equal(10, parameters.Length);
    }
}
