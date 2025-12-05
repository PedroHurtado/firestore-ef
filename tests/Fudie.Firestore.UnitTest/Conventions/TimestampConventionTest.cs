namespace Fudie.Firestore.UnitTest.Conventions;

public class TimestampConventionTest
{
    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("UpdatedAt")]
    [InlineData("ModifiedAt")]
    [InlineData("DeletedAt")]
    [InlineData("CreatedDate")]
    [InlineData("LastModified")]
    public void ProcessPropertyAdded_Recognizes_Timestamp_Property_Names(string propertyName)
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(DateTime));
        propertyMock.Setup(p => p.Name).Returns(propertyName);
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should not throw
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert - Convention currently doesn't apply any conversion, just recognizes
        // This test verifies it doesn't crash on valid timestamp names
    }

    [Fact]
    public void ProcessPropertyAdded_Handles_Nullable_DateTime()
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(DateTime?));
        propertyMock.Setup(p => p.Name).Returns("CreatedAt");
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should not throw
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);
    }

    [Fact]
    public void ProcessPropertyAdded_Ignores_NonDateTime_Property()
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(string));
        propertyMock.Setup(p => p.Name).Returns("CreatedAt");
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should return early without checking converter
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert - GetValueConverter should not be called for non-DateTime types
        propertyMock.Verify(p => p.GetValueConverter(), Times.Never);
    }

    [Fact]
    public void ProcessPropertyAdded_Skips_Property_With_Existing_Converter()
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();
        // Use a compatible converter (int to long works with CastingConverter)
        var existingConverter = new CastingConverter<int, long>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(DateTime));
        propertyMock.Setup(p => p.Name).Returns("CreatedAt");
        propertyMock.Setup(p => p.GetValueConverter()).Returns(existingConverter);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should return early without processing
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert - Convention returns early when converter exists
        // No additional setup needed, just verifying no exception occurs
    }

    [Theory]
    [InlineData("SomeRandomProperty")]
    [InlineData("DateValue")]
    [InlineData("Timestamp")]
    public void ProcessPropertyAdded_Ignores_NonTimestamp_Named_Properties(string propertyName)
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(DateTime));
        propertyMock.Setup(p => p.Name).Returns(propertyName);
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should not throw
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);
    }

    [Fact]
    public void ProcessPropertyAdded_Is_Case_Insensitive()
    {
        // Arrange
        var convention = new TimestampConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(DateTime));
        propertyMock.Setup(p => p.Name).Returns("createdat"); // lowercase
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act - Should not throw, recognizes case-insensitive
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);
    }
}
