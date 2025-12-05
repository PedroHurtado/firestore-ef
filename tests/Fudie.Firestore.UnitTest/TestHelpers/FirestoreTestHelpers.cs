namespace Fudie.Firestore.UnitTest.TestHelpers;

/// <summary>
/// Helper class for creating test infrastructure similar to EF Core InMemory provider patterns.
/// </summary>
public static class FirestoreTestHelpers
{
    /// <summary>
    /// Creates a model builder with Firestore conventions applied.
    /// </summary>
    public static ModelBuilder CreateConventionBuilder()
    {
        var serviceProvider = CreateServiceProvider();
        var conventionSet = CreateConventionSet(serviceProvider);

        return new ModelBuilder(conventionSet);
    }

    /// <summary>
    /// Creates a minimal service provider for testing.
    /// </summary>
    public static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add minimal services needed for testing
        services.AddLogging();
        services.AddEntityFrameworkFirestore();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates convention set for model building.
    /// </summary>
    public static ConventionSet CreateConventionSet(IServiceProvider serviceProvider)
    {
        var dependencies = serviceProvider.GetRequiredService<ProviderConventionSetBuilderDependencies>();
        var conventionSetBuilder = new FirestoreConventionSetBuilder(dependencies);

        return conventionSetBuilder.CreateConventionSet();
    }

    /// <summary>
    /// Creates a finalized model from the given configuration action.
    /// </summary>
    public static IModel CreateModel(Action<ModelBuilder> buildAction)
    {
        var modelBuilder = CreateConventionBuilder();
        buildAction(modelBuilder);
        return modelBuilder.FinalizeModel();
    }

    /// <summary>
    /// Creates a mock IEntityType for testing.
    /// </summary>
    public static Mock<IEntityType> CreateEntityTypeMock<T>() where T : class
    {
        var mock = new Mock<IEntityType>();
        mock.Setup(e => e.ClrType).Returns(typeof(T));
        mock.Setup(e => e.Name).Returns(typeof(T).FullName!);
        return mock;
    }

    /// <summary>
    /// Creates a mock IProperty for testing.
    /// </summary>
    public static Mock<IProperty> CreatePropertyMock(string name, Type clrType)
    {
        var mock = new Mock<IProperty>();
        mock.Setup(p => p.Name).Returns(name);
        mock.Setup(p => p.ClrType).Returns(clrType);
        return mock;
    }

    /// <summary>
    /// Creates DbContextOptions configured for Firestore testing.
    /// </summary>
    public static DbContextOptions CreateTestOptions(string projectId = "test-project")
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore(projectId);
        return optionsBuilder.Options;
    }
}

/// <summary>
/// Base class for convention tests providing common setup.
/// </summary>
public abstract class ConventionTestBase
{
    protected ModelBuilder CreateModelBuilder()
    {
        return FirestoreTestHelpers.CreateConventionBuilder();
    }

    protected IModel FinalizeModel(ModelBuilder modelBuilder)
    {
        return modelBuilder.FinalizeModel();
    }
}

/// <summary>
/// Test entities for use across test classes.
/// </summary>
public static class TestEntities
{
    public class Customer
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
    }

    public class Product
    {
        public string ProductId { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    public class Order
    {
        public string Id { get; set; } = default!;
        public string CustomerId { get; set; } = default!;
        public DateTime OrderDate { get; set; }
        public decimal Total { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderLine> Lines { get; set; } = new();
    }

    public class OrderLine
    {
        public string Id { get; set; } = default!;
        public string OrderId { get; set; } = default!;
        public string ProductId { get; set; } = default!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class Store
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public Location Position { get; set; } = default!;
    }

    public class EntityWithoutId
    {
        public string Name { get; set; } = default!;
    }

    public class EntityWithCompositeKey
    {
        public string Part1 { get; set; } = default!;
        public string Part2 { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
