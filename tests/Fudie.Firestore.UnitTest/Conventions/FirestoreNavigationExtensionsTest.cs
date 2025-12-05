namespace Fudie.Firestore.UnitTest.Conventions;

public class FirestoreNavigationExtensionsTest
{
    [Fact]
    public void IsSubCollection_Returns_True_When_Annotation_Is_True()
    {
        // Arrange
        var navigationMock = new Mock<IReadOnlyNavigation>();
        var annotationMock = new Mock<IAnnotation>();

        annotationMock.Setup(a => a.Value).Returns(true);
        navigationMock.Setup(n => n.FindAnnotation("Firestore:SubCollection"))
            .Returns(annotationMock.Object);

        // Act
        var result = navigationMock.Object.IsSubCollection();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSubCollection_Returns_False_When_Annotation_Is_False()
    {
        // Arrange
        var navigationMock = new Mock<IReadOnlyNavigation>();
        var annotationMock = new Mock<IAnnotation>();

        annotationMock.Setup(a => a.Value).Returns(false);
        navigationMock.Setup(n => n.FindAnnotation("Firestore:SubCollection"))
            .Returns(annotationMock.Object);

        // Act
        var result = navigationMock.Object.IsSubCollection();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSubCollection_Returns_False_When_Annotation_Is_Null()
    {
        // Arrange
        var navigationMock = new Mock<IReadOnlyNavigation>();
        navigationMock.Setup(n => n.FindAnnotation("Firestore:SubCollection"))
            .Returns((IAnnotation?)null);

        // Act
        var result = navigationMock.Object.IsSubCollection();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSubCollection_Returns_False_When_Annotation_Value_Is_Not_Bool()
    {
        // Arrange
        var navigationMock = new Mock<IReadOnlyNavigation>();
        var annotationMock = new Mock<IAnnotation>();

        annotationMock.Setup(a => a.Value).Returns("not a bool");
        navigationMock.Setup(n => n.FindAnnotation("Firestore:SubCollection"))
            .Returns(annotationMock.Object);

        // Act
        var result = navigationMock.Object.IsSubCollection();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SetIsSubCollection_Sets_Annotation_To_True()
    {
        // Arrange
        var navigationMock = new Mock<IMutableNavigation>();

        // Act
        navigationMock.Object.SetIsSubCollection(true);

        // Assert
        navigationMock.Verify(
            n => n.SetAnnotation("Firestore:SubCollection", true),
            Times.Once);
    }

    [Fact]
    public void SetIsSubCollection_Sets_Annotation_To_False()
    {
        // Arrange
        var navigationMock = new Mock<IMutableNavigation>();

        // Act
        navigationMock.Object.SetIsSubCollection(false);

        // Assert
        navigationMock.Verify(
            n => n.SetAnnotation("Firestore:SubCollection", false),
            Times.Once);
    }
}
