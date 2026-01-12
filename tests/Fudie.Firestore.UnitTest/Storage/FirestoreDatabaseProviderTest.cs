namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreDatabaseProviderTest
{
    [Fact]
    public void Name_Returns_ProviderName()
    {
        var dependencies = CreateDependencies();
        var provider = new FirestoreDatabaseProvider(dependencies);

        provider.Name.Should().Be("Fudie.Firestore.EntityFrameworkCore");
    }

    [Fact]
    public void ProviderName_Constant_Has_Correct_Value()
    {
        FirestoreDatabaseProvider.ProviderName.Should().Be("Fudie.Firestore.EntityFrameworkCore");
    }

    [Fact]
    public void IsConfigured_Returns_True_When_FirestoreExtension_Present()
    {
        var dependencies = CreateDependencies();
        var provider = new FirestoreDatabaseProvider(dependencies);
        var options = CreateOptionsWithFirestoreExtension();

        var result = provider.IsConfigured(options);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_Returns_False_When_FirestoreExtension_Not_Present()
    {
        var dependencies = CreateDependencies();
        var provider = new FirestoreDatabaseProvider(dependencies);
        var options = CreateOptionsWithoutFirestoreExtension();

        var result = provider.IsConfigured(options);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_Returns_False_When_Options_Is_Null()
    {
        var dependencies = CreateDependencies();
        var provider = new FirestoreDatabaseProvider(dependencies);

        var result = provider.IsConfigured(null!);

        result.Should().BeFalse();
    }

    private static DatabaseProviderDependencies CreateDependencies()
    {
        return new DatabaseProviderDependencies();
    }

    private static IDbContextOptions CreateOptionsWithFirestoreExtension()
    {
        var optionsMock = new Mock<IDbContextOptions>();
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("test-project");

        optionsMock
            .Setup(o => o.FindExtension<FirestoreOptionsExtension>())
            .Returns(extension);

        return optionsMock.Object;
    }

    private static IDbContextOptions CreateOptionsWithoutFirestoreExtension()
    {
        var optionsMock = new Mock<IDbContextOptions>();

        optionsMock
            .Setup(o => o.FindExtension<FirestoreOptionsExtension>())
            .Returns((FirestoreOptionsExtension?)null);

        return optionsMock.Object;
    }
}
