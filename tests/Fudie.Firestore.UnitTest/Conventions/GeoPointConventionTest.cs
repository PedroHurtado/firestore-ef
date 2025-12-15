namespace Fudie.Firestore.UnitTest.Conventions;

public class GeoPointConventionTest
{
    private class LocationType
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private class LatLngType
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    private class InvalidType
    {
        public string Name { get; set; } = default!;
    }

    [Theory]
    [InlineData("Location")]
    [InlineData("Coordinates")]
    [InlineData("Position")]
    [InlineData("GeoLocation")]
    public void ProcessComplexPropertyAdded_Recognizes_GeoPoint_PropertyNames(string propertyName)
    {
        // Arrange
        var convention = new GeoPointConvention();
        var (propertyBuilder, context, annotationAdded) = CreateComplexPropertyBuilderMock(
            propertyName,
            typeof(LocationType));

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        annotationAdded().Should().BeTrue();
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Recognizes_Lat_Lng_Variants()
    {
        // Arrange
        var convention = new GeoPointConvention();
        var (propertyBuilder, context, annotationAdded) = CreateComplexPropertyBuilderMock(
            "Location",
            typeof(LatLngType));

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        annotationAdded().Should().BeTrue();
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Ignores_Type_Without_LatLong()
    {
        // Arrange
        var convention = new GeoPointConvention();
        var (propertyBuilder, context, annotationAdded) = CreateComplexPropertyBuilderMock(
            "Location",
            typeof(InvalidType));

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        annotationAdded().Should().BeFalse();
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Does_Not_Override_Existing_Annotation()
    {
        // Arrange
        var convention = new GeoPointConvention();
        var (propertyBuilder, context, annotationAdded) = CreateComplexPropertyBuilderMock(
            "Location",
            typeof(LocationType),
            hasExistingAnnotation: true);

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        annotationAdded().Should().BeFalse();
    }

    private static (Mock<IConventionComplexPropertyBuilder>, Mock<IConventionContext<IConventionComplexPropertyBuilder>>, Func<bool>)
        CreateComplexPropertyBuilderMock(string propertyName, Type complexClrType, bool hasExistingAnnotation = false)
    {
        bool geoPointAnnotationAdded = false;

        var complexPropertyMock = new Mock<IConventionComplexProperty>();
        var complexTypeMock = new Mock<IConventionComplexType>();
        var propertyBuilderMock = new Mock<IConventionComplexPropertyBuilder>();
        var contextMock = new Mock<IConventionContext<IConventionComplexPropertyBuilder>>();

        complexPropertyMock.Setup(p => p.Name).Returns(propertyName);
        complexPropertyMock.Setup(p => p.ComplexType).Returns(complexTypeMock.Object);

        if (hasExistingAnnotation)
        {
            var annotationMock = new Mock<IConventionAnnotation>();
            complexPropertyMock.Setup(p => p.FindAnnotation("Firestore:IsGeoPoint"))
                .Returns(annotationMock.Object);
        }
        else
        {
            complexPropertyMock.Setup(p => p.FindAnnotation("Firestore:IsGeoPoint"))
                .Returns((IConventionAnnotation?)null);
        }

        complexTypeMock.Setup(t => t.ClrType).Returns(complexClrType);
        complexTypeMock.Setup(t => t.GetProperties()).Returns(Array.Empty<IConventionProperty>());

        propertyBuilderMock.Setup(b => b.Metadata).Returns(complexPropertyMock.Object);
        propertyBuilderMock
            .Setup(b => b.HasAnnotation("Firestore:IsGeoPoint", It.IsAny<object?>(), It.IsAny<bool>()))
            .Callback<string, object?, bool>((name, value, _) =>
            {
                if (name == "Firestore:IsGeoPoint" && value is true)
                    geoPointAnnotationAdded = true;
            })
            .Returns(propertyBuilderMock.Object);

        return (propertyBuilderMock, contextMock, () => geoPointAnnotationAdded);
    }
}
