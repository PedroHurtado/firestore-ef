namespace Fudie.Firestore.UnitTest.Conventions;

public class DocumentReferenceNamingConventionTest
{
    [Fact]
    public void ProcessNavigationAdded_Adds_Ref_Suffix_When_No_Annotation_Exists()
    {
        // Arrange
        var convention = new DocumentReferenceNamingConvention();
        var navigationBuilderMock = new Mock<IConventionNavigationBuilder>();
        var navigationMock = new Mock<IConventionNavigation>();
        var contextMock = new Mock<IConventionContext<IConventionNavigationBuilder>>();
        string? capturedAnnotationValue = null;

        navigationMock.Setup(n => n.Name).Returns("Customer");
        navigationMock.Setup(n => n.FindAnnotation("Firestore:DocumentReferenceFieldName"))
            .Returns((IConventionAnnotation?)null);
        navigationBuilderMock.Setup(b => b.Metadata).Returns(navigationMock.Object);
        navigationBuilderMock
            .Setup(b => b.HasAnnotation(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
            .Callback<string, object?, bool>((name, value, _) =>
            {
                if (name == "Firestore:DocumentReferenceFieldName")
                    capturedAnnotationValue = value as string;
            })
            .Returns(navigationBuilderMock.Object);

        // Act
        convention.ProcessNavigationAdded(navigationBuilderMock.Object, contextMock.Object);

        // Assert
        capturedAnnotationValue.Should().Be("CustomerRef");
    }

    [Fact]
    public void ProcessNavigationAdded_Does_Not_Override_Existing_Annotation()
    {
        // Arrange
        var convention = new DocumentReferenceNamingConvention();
        var navigationBuilderMock = new Mock<IConventionNavigationBuilder>();
        var navigationMock = new Mock<IConventionNavigation>();
        var annotationMock = new Mock<IConventionAnnotation>();
        var contextMock = new Mock<IConventionContext<IConventionNavigationBuilder>>();

        navigationMock.Setup(n => n.Name).Returns("Customer");
        navigationMock.Setup(n => n.FindAnnotation("Firestore:DocumentReferenceFieldName"))
            .Returns(annotationMock.Object);
        navigationBuilderMock.Setup(b => b.Metadata).Returns(navigationMock.Object);

        // Act
        convention.ProcessNavigationAdded(navigationBuilderMock.Object, contextMock.Object);

        // Assert
        navigationBuilderMock.Verify(
            b => b.HasAnnotation(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Order", "OrderRef")]
    [InlineData("Product", "ProductRef")]
    [InlineData("Category", "CategoryRef")]
    [InlineData("User", "UserRef")]
    public void ProcessNavigationAdded_Creates_Correct_FieldName(string navigationName, string expectedFieldName)
    {
        // Arrange
        var convention = new DocumentReferenceNamingConvention();
        var navigationBuilderMock = new Mock<IConventionNavigationBuilder>();
        var navigationMock = new Mock<IConventionNavigation>();
        var contextMock = new Mock<IConventionContext<IConventionNavigationBuilder>>();
        string? capturedFieldName = null;

        navigationMock.Setup(n => n.Name).Returns(navigationName);
        navigationMock.Setup(n => n.FindAnnotation("Firestore:DocumentReferenceFieldName"))
            .Returns((IConventionAnnotation?)null);
        navigationBuilderMock.Setup(b => b.Metadata).Returns(navigationMock.Object);
        navigationBuilderMock
            .Setup(b => b.HasAnnotation("Firestore:DocumentReferenceFieldName", It.IsAny<object?>(), It.IsAny<bool>()))
            .Callback<string, object?, bool>((_, value, _) => capturedFieldName = value as string)
            .Returns(navigationBuilderMock.Object);

        // Act
        convention.ProcessNavigationAdded(navigationBuilderMock.Object, contextMock.Object);

        // Assert
        capturedFieldName.Should().Be(expectedFieldName);
    }
}
