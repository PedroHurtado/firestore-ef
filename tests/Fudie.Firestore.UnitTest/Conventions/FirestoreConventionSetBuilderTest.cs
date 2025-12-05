using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Fudie.Firestore.UnitTest.Conventions;

public class FirestoreConventionSetBuilderTest
{
    [Fact]
    public void Constructor_Accepts_Dependencies()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();

        // Act
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void CreateConventionSet_Returns_ConventionSet()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Act
        var conventionSet = builder.CreateConventionSet();

        // Assert
        conventionSet.Should().NotBeNull();
    }

    [Fact]
    public void CreateConventionSet_Contains_EntityTypeAddedConventions()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Act
        var conventionSet = builder.CreateConventionSet();

        // Assert
        conventionSet.EntityTypeAddedConventions.Should().NotBeEmpty();
        conventionSet.EntityTypeAddedConventions
            .Should().Contain(c => c.GetType() == typeof(PrimaryKeyConvention));
        conventionSet.EntityTypeAddedConventions
            .Should().Contain(c => c.GetType() == typeof(CollectionNamingConvention));
    }

    [Fact]
    public void CreateConventionSet_Contains_PropertyAddedConventions()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Act
        var conventionSet = builder.CreateConventionSet();

        // Assert
        conventionSet.PropertyAddedConventions.Should().NotBeEmpty();
        conventionSet.PropertyAddedConventions
            .Should().Contain(c => c.GetType() == typeof(EnumToStringConvention));
        conventionSet.PropertyAddedConventions
            .Should().Contain(c => c.GetType() == typeof(DecimalToDoubleConvention));
        conventionSet.PropertyAddedConventions
            .Should().Contain(c => c.GetType() == typeof(TimestampConvention));
    }

    [Fact]
    public void CreateConventionSet_Contains_ComplexPropertyAddedConventions()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Act
        var conventionSet = builder.CreateConventionSet();

        // Assert
        conventionSet.ComplexPropertyAddedConventions.Should().NotBeEmpty();
        conventionSet.ComplexPropertyAddedConventions
            .Should().Contain(c => c.GetType() == typeof(GeoPointConvention));
        conventionSet.ComplexPropertyAddedConventions
            .Should().Contain(c => c.GetType() == typeof(ComplexTypeNavigationPropertyConvention));
    }

    [Fact]
    public void CreateConventionSet_Contains_NavigationAddedConventions()
    {
        // Arrange
        var dependencies = CreateMinimalDependencies();
        var builder = new FirestoreConventionSetBuilder(dependencies);

        // Act
        var conventionSet = builder.CreateConventionSet();

        // Assert
        conventionSet.NavigationAddedConventions.Should().NotBeEmpty();
        conventionSet.NavigationAddedConventions
            .Should().Contain(c => c.GetType() == typeof(DocumentReferenceNamingConvention));
    }

    private static ProviderConventionSetBuilderDependencies CreateMinimalDependencies()
    {
        // Create a minimal in-memory DbContext to get real dependencies
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();

        var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder()
            .UseInMemoryDatabase("TestDb")
            .UseInternalServiceProvider(serviceProvider);

        using var context = new DbContext(optionsBuilder.Options);

        // Get the convention set builder dependencies from the service provider
        var internalServiceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        return internalServiceProvider.GetRequiredService<ProviderConventionSetBuilderDependencies>();
    }
}
