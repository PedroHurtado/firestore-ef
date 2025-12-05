using Firestore.EntityFrameworkCore.Infrastructure;
using Google.Cloud.Firestore;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreDatabaseCreatorTest
{
    private readonly Mock<IFirestoreClientWrapper> _clientWrapperMock;
    private readonly FirestoreDatabaseCreator _creator;

    public FirestoreDatabaseCreatorTest()
    {
        _clientWrapperMock = new Mock<IFirestoreClientWrapper>();
        _creator = new FirestoreDatabaseCreator(_clientWrapperMock.Object);
    }

    #region EnsureCreated Tests

    [Fact]
    public void EnsureCreated_Always_Returns_True()
    {
        var result = _creator.EnsureCreated();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureCreatedAsync_Always_Returns_True()
    {
        var result = await _creator.EnsureCreatedAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureCreatedAsync_Does_Not_Call_Firestore()
    {
        await _creator.EnsureCreatedAsync();

        _clientWrapperMock.Verify(
            c => c.GetCollection(It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region EnsureDeleted Tests

    [Fact]
    public void EnsureDeleted_Always_Returns_False()
    {
        var result = _creator.EnsureDeleted();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDeletedAsync_Always_Returns_False()
    {
        var result = await _creator.EnsureDeletedAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDeletedAsync_Does_Not_Call_Firestore()
    {
        await _creator.EnsureDeletedAsync();

        _clientWrapperMock.Verify(
            c => c.GetCollection(It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region CanConnect Tests

    [Fact]
    public async Task CanConnectAsync_Returns_False_When_Exception_Occurs()
    {
        _clientWrapperMock
            .Setup(c => c.GetCollection("_connection_test"))
            .Throws(new Exception("Connection failed"));

        var result = await _creator.CanConnectAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public void CanConnect_Returns_False_When_Exception_Occurs()
    {
        _clientWrapperMock
            .Setup(c => c.GetCollection("_connection_test"))
            .Throws(new Exception("Connection failed"));

        var result = _creator.CanConnect();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanConnectAsync_Uses_ConnectionTest_Collection()
    {
        _clientWrapperMock
            .Setup(c => c.GetCollection("_connection_test"))
            .Throws(new Exception("Test"));

        await _creator.CanConnectAsync();

        _clientWrapperMock.Verify(
            c => c.GetCollection("_connection_test"),
            Times.Once);
    }

    [Fact]
    public async Task CanConnectAsync_Respects_CancellationToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _clientWrapperMock
            .Setup(c => c.GetCollection("_connection_test"))
            .Throws(new OperationCanceledException());

        var result = await _creator.CanConnectAsync(cts.Token);

        result.Should().BeFalse();
    }

    #endregion
}
