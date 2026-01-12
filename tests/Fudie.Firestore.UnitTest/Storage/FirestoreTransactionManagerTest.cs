using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreTransactionManagerTest
{
    [Fact]
    public void Manager_ShouldImplementIDbContextTransactionManager()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();

        // Act
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Assert
        Assert.IsAssignableFrom<IDbContextTransactionManager>(manager);
    }

    [Fact]
    public void CurrentTransaction_ShouldBeNull_Initially()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        Assert.Null(manager.CurrentTransaction);
    }

    [Fact]
    public void CommitTransaction_WhenNoTransaction_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.CommitTransaction());
        Assert.Contains("No hay transacción activa", exception.Message);
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenNoTransaction_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CommitTransactionAsync());
        Assert.Contains("No hay transacción activa", exception.Message);
    }

    [Fact]
    public void RollbackTransaction_WhenNoTransaction_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.RollbackTransaction());
        Assert.Contains("No hay transacción activa", exception.Message);
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenNoTransaction_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RollbackTransactionAsync());
        Assert.Contains("No hay transacción activa", exception.Message);
    }

    [Fact]
    public void ResetState_WhenNoTransaction_ShouldNotThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = Record.Exception(() => manager.ResetState());
        Assert.Null(exception);
    }

    [Fact]
    public async Task ResetStateAsync_WhenNoTransaction_ShouldNotThrow()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => manager.ResetStateAsync());
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_ShouldCreateManager()
    {
        // Arrange
        var mockClient = new Mock<IFirestoreClientWrapper>();

        // Act
        var manager = new FirestoreTransactionManager(mockClient.Object);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void Manager_ErrorMessages_ShouldBeInSpanish()
    {
        // The error messages are in Spanish, verify they match expected patterns
        var mockClient = new Mock<IFirestoreClientWrapper>();
        var manager = new FirestoreTransactionManager(mockClient.Object);

        var commitException = Assert.Throws<InvalidOperationException>(() => manager.CommitTransaction());
        var rollbackException = Assert.Throws<InvalidOperationException>(() => manager.RollbackTransaction());

        Assert.Contains("transacción", commitException.Message.ToLower());
        Assert.Contains("transacción", rollbackException.Message.ToLower());
    }
}
