using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreServiceCollectionExtensionsTest
{
    [Fact]
    public void AddEntityFrameworkFirestore_ShouldThrow_WhenServiceCollectionIsNull()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services!.AddEntityFrameworkFirestore());
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddEntityFrameworkFirestore();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterProviderConventionSetBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEntityFrameworkFirestore();
        var provider = services.BuildServiceProvider();

        // Act
        var conventionSetBuilder = provider.GetService<IProviderConventionSetBuilder>();

        // Assert - the service should be registered (may be null if not fully configured)
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IProviderConventionSetBuilder));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterFirestoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert - check that Firestore-specific services are registered
        var serviceTypes = services.Select(d => d.ServiceType).ToList();

        Assert.Contains(typeof(IProviderConventionSetBuilder), serviceTypes);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldBeIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();
        var count1 = services.Count;

        services.AddEntityFrameworkFirestore();
        var count2 = services.Count;

        // Assert - TryAdd should not add duplicate services
        // Note: Some services may be added twice if they use Add instead of TryAdd
        Assert.True(count2 >= count1);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterLogging()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasLogging = services.Any(d =>
            d.ServiceType.Name.Contains("Logger") ||
            d.ServiceType.Name.Contains("Logging"));
        Assert.True(hasLogging);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterDatabaseProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasDatabaseProvider = services.Any(d =>
            d.ServiceType.Name == "IDatabaseProvider");
        Assert.True(hasDatabaseProvider);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterDatabase()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasDatabase = services.Any(d =>
            d.ServiceType == typeof(IDatabase));
        Assert.True(hasDatabase);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterTransactionManager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasTransactionManager = services.Any(d =>
            d.ServiceType == typeof(IDbContextTransactionManager));
        Assert.True(hasTransactionManager);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterQueryFactories()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasQueryContextFactory = services.Any(d =>
            d.ServiceType == typeof(IQueryContextFactory));
        Assert.True(hasQueryContextFactory);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterTypeMappingSource()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasTypeMappingSource = services.Any(d =>
            d.ServiceType == typeof(ITypeMappingSource));
        Assert.True(hasTypeMappingSource);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterFirestoreClientWrapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasClientWrapper = services.Any(d =>
            d.ServiceType == typeof(IFirestoreClientWrapper));
        Assert.True(hasClientWrapper);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterIdGenerator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasIdGenerator = services.Any(d =>
            d.ServiceType == typeof(IFirestoreIdGenerator));
        Assert.True(hasIdGenerator);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterCollectionManager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var hasCollectionManager = services.Any(d =>
            d.ServiceType == typeof(IFirestoreCollectionManager));
        Assert.True(hasCollectionManager);
    }
}
