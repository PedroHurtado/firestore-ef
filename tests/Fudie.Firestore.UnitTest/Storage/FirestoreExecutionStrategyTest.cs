using Fudie.Firestore.EntityFrameworkCore.Storage;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreExecutionStrategyTest
{
    private ExecutionStrategyDependencies CreateDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));

        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetRequiredService<TestDbContext>();

        var mockCurrentDbContext = new Mock<ICurrentDbContext>();
        mockCurrentDbContext.Setup(c => c.Context).Returns(context);

        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Infrastructure>>();

        return new ExecutionStrategyDependencies(
            mockCurrentDbContext.Object,
            context.GetService<IDbContextOptions>(),
            mockLogger.Object);
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions options) : base(options) { }
    }

    [Fact]
    public void Constructor_ShouldCreateStrategy_WithDefaultRetryCount()
    {
        // Arrange
        var dependencies = CreateDependencies();

        // Act
        var strategy = new FirestoreExecutionStrategy(dependencies);

        // Assert
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Factory_Create_ShouldReturnFirestoreExecutionStrategy()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var factory = new FirestoreExecutionStrategyFactory(dependencies);

        // Act
        var strategy = factory.Create();

        // Assert
        Assert.IsType<FirestoreExecutionStrategy>(strategy);
    }

    [Fact]
    public void ShouldRetryOn_UnavailableRpcException_ShouldReturnTrue()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryOn_DeadlineExceededRpcException_ShouldReturnTrue()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded"));

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryOn_ResourceExhaustedRpcException_ShouldReturnTrue()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new RpcException(new Status(StatusCode.ResourceExhausted, "Resource exhausted"));

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryOn_HttpRequestException_ShouldReturnTrue()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new System.Net.Http.HttpRequestException("Network error");

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryOn_IOException_ShouldReturnTrue()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new System.IO.IOException("IO error");

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryOn_InvalidOperationException_ShouldReturnFalse()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new InvalidOperationException("Invalid operation");

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryOn_NotFoundRpcException_ShouldReturnFalse()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new RpcException(new Status(StatusCode.NotFound, "Not found"));

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryOn_PermissionDeniedRpcException_ShouldReturnFalse()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new RpcException(new Status(StatusCode.PermissionDenied, "Permission denied"));

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryOn_ArgumentException_ShouldReturnFalse()
    {
        // Arrange
        var dependencies = CreateDependencies();
        var strategy = new TestableFirestoreExecutionStrategy(dependencies);
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = strategy.TestShouldRetryOn(exception);

        // Assert
        Assert.False(result);
    }

    // Helper class to expose protected methods for testing
    private class TestableFirestoreExecutionStrategy : FirestoreExecutionStrategy
    {
        public TestableFirestoreExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies)
        {
        }

        public bool TestShouldRetryOn(Exception exception)
        {
            return ShouldRetryOn(exception);
        }
    }
}
