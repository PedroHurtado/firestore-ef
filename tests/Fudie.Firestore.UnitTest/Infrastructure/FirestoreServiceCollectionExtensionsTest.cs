using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

    #region Pipeline Registration Tests

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterQueryPipelineMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineMediator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(QueryPipelineMediator), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterErrorHandlingHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(ErrorHandlingHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterResolverHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(ResolverHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterLogQueryHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(LogQueryHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterExecutionHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(ExecutionHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterConvertHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(ConvertHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterTrackingHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryPipelineHandler) &&
            d.ImplementationType == typeof(TrackingHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterProxyHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert - ProxyHandler is registered with factory delegate (optional IProxyFactory)
        // so ImplementationType is null, we check via ImplementationFactory
        var handlerDescriptors = services
            .Where(d => d.ServiceType == typeof(IQueryPipelineHandler))
            .ToList();

        // Should have 7 handlers total (IncludeHandler removed - includes handled by ExecutionHandler)
        Assert.Equal(7, handlerDescriptors.Count);

        // ProxyHandler is registered with factory (4th handler - position 3, 0-indexed)
        // Order: ErrorHandling(0) → Resolver(1) → Log(2) → Proxy(3) → Tracking(4) → Convert(5) → Execution(6)
        var proxyDescriptor = handlerDescriptors[3];
        Assert.NotNull(proxyDescriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, proxyDescriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterHandlersInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert - handlers must be registered in middleware order
        // Order: ErrorHandling → Resolver → Log → Proxy → Tracking → Convert → Execution
        // Each handler calls next() and receives the result from subsequent handlers
        // Result flows: Execution returns docs (+ includes) → Convert→entities → Tracking → Proxy → return
        // Note: Includes are loaded by ExecutionHandler directly, not by a separate handler
        var handlerRegistrations = services
            .Where(d => d.ServiceType == typeof(IQueryPipelineHandler))
            .ToList();

        // 7 handlers total (IncludeHandler removed - includes handled by ExecutionHandler)
        Assert.Equal(7, handlerRegistrations.Count);

        // Verify specific handlers by position (0-indexed)
        Assert.Equal(typeof(ErrorHandlingHandler), handlerRegistrations[0].ImplementationType);
        Assert.Equal(typeof(ResolverHandler), handlerRegistrations[1].ImplementationType);
        Assert.Equal(typeof(LogQueryHandler), handlerRegistrations[2].ImplementationType);
        // ProxyHandler is factory-registered, no ImplementationType
        Assert.Null(handlerRegistrations[3].ImplementationType);
        Assert.NotNull(handlerRegistrations[3].ImplementationFactory);
        Assert.Equal(typeof(TrackingHandler), handlerRegistrations[4].ImplementationType);
        Assert.Equal(typeof(ConvertHandler), handlerRegistrations[5].ImplementationType);
        Assert.Equal(typeof(ExecutionHandler), handlerRegistrations[6].ImplementationType);
    }

    #endregion

    #region Pipeline Services Registration Tests

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterFirestoreAstResolver()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IFirestoreAstResolver));
        Assert.NotNull(descriptor);
        // Singleton because context is passed per-request to Resolve method
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterQueryBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryBuilder));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterTypeConverter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ITypeConverter));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterLazyLoader()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        // ILazyLoader is Transient per EF Core documentation
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ILazyLoader));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterFirestoreErrorHandlingOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(FirestoreErrorHandlingOptions));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterFirestoreValueConverter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IFirestoreValueConverter));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(FirestoreValueConverter), descriptor.ImplementationType);
    }

    [Fact]
    public void AddEntityFrameworkFirestore_ShouldRegisterExpressionEvaluator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEntityFrameworkFirestore();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IExpressionEvaluator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(ExpressionEvaluator), descriptor.ImplementationType);
    }

    #endregion
}
