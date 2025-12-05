namespace Fudie.Firestore.UnitTest.Conventions;

public class EnumToStringConventionTest
{
    private enum TestStatus { Pending, Active, Completed }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public TestStatus Status { get; set; }
        public string Name { get; set; } = default!;
    }

    [Fact]
    public void ProcessPropertyAdded_Applies_Converter_For_Enum_Property()
    {
        // Arrange
        var convention = new EnumToStringConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(TestStatus));
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert
        propertyBuilderMock.Verify(
            b => b.HasConversion(It.IsAny<ValueConverter>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public void ProcessPropertyAdded_Skips_NonEnum_Property()
    {
        // Arrange
        var convention = new EnumToStringConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(string));
        propertyMock.Setup(p => p.GetValueConverter()).Returns((ValueConverter?)null);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert
        propertyBuilderMock.Verify(
            b => b.HasConversion(It.IsAny<ValueConverter>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public void ProcessPropertyAdded_Skips_Property_With_Existing_Converter()
    {
        // Arrange
        var convention = new EnumToStringConvention();
        var propertyBuilderMock = new Mock<IConventionPropertyBuilder>();
        var propertyMock = new Mock<IConventionProperty>();
        var contextMock = new Mock<IConventionContext<IConventionPropertyBuilder>>();
        // Use a real converter instead of mock (ValueConverter is abstract with no parameterless ctor)
        var existingConverter = new CastingConverter<int, long>();

        propertyMock.Setup(p => p.ClrType).Returns(typeof(TestStatus));
        propertyMock.Setup(p => p.GetValueConverter()).Returns(existingConverter);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(propertyMock.Object);

        // Act
        convention.ProcessPropertyAdded(propertyBuilderMock.Object, contextMock.Object);

        // Assert
        propertyBuilderMock.Verify(
            b => b.HasConversion(It.IsAny<ValueConverter>(), It.IsAny<bool>()),
            Times.Never);
    }
}
