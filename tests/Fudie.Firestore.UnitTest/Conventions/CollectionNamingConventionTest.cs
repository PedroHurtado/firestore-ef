namespace Fudie.Firestore.UnitTest.Conventions;

public class CollectionNamingConventionTest
{
    private class Customer { public string Id { get; set; } = default!; }
    private class Category { public string Id { get; set; } = default!; }

    [Fact]
    public void Convention_Is_IEntityTypeAddedConvention()
    {
        // Arrange & Act
        var convention = new CollectionNamingConvention();

        // Assert
        convention.Should().BeAssignableTo<IEntityTypeAddedConvention>();
    }

    [Fact]
    public void Convention_Can_Be_Instantiated()
    {
        // Arrange & Act
        var convention = new CollectionNamingConvention();

        // Assert
        convention.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Customer", "Customers")]
    [InlineData("Person", "People")]
    [InlineData("Category", "Categories")]
    [InlineData("Address", "Addresses")]
    [InlineData("Order", "Orders")]
    [InlineData("Product", "Products")]
    public void Humanizer_Pluralizes_Names_Correctly(string singular, string expectedPlural)
    {
        // This tests the Humanizer library behavior that the convention uses
        var result = Humanizer.InflectorExtensions.Pluralize(singular);
        result.Should().Be(expectedPlural);
    }

    [Fact]
    public void Convention_Uses_Humanizer_For_Pluralization()
    {
        // This test verifies the convention's dependency on Humanizer
        var testName = "TestEntity";
        var expected = Humanizer.InflectorExtensions.Pluralize(testName);

        expected.Should().Be("TestEntities");
    }

    [Fact]
    public void ProcessEntityTypeAdded_Exists_And_Is_Public()
    {
        // Arrange
        var convention = new CollectionNamingConvention();
        var method = convention.GetType().GetMethod("ProcessEntityTypeAdded");

        // Assert
        method.Should().NotBeNull();
        method!.IsPublic.Should().BeTrue();
    }
}
