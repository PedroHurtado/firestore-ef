namespace Fudie.Firestore.UnitTest.Conventions;

/// <summary>
/// Tests unitarios para ArrayOfConvention.
/// Verifica auto-detección de List&lt;T&gt; como ArrayOf Embedded o GeoPoint.
/// </summary>
public class ArrayOfConventionTest
{
    #region Test Types

    // Tipo GeoPoint puro (sin Id)
    private class GeoLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // Tipo GeoPoint con Lat/Lng
    private class GeoPoint
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    // Tipo ComplexType sin Id
    private class Direccion
    {
        public string Calle { get; set; } = default!;
        public string Ciudad { get; set; } = default!;
    }

    // Tipo Entity (tiene Id)
    private class Producto
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    // Tipo Entity con {TypeName}Id
    private class Categoria
    {
        public string CategoriaId { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    // Entidad con List<GeoPoint>
    private class TiendaConUbicaciones
    {
        public string Id { get; set; } = default!;
        public List<GeoLocation> Ubicaciones { get; set; } = [];
    }

    // Entidad con List<ComplexType>
    private class TiendaConDirecciones
    {
        public string Id { get; set; } = default!;
        public List<Direccion> Direcciones { get; set; } = [];
    }

    // Entidad con List<Entity> (no debería auto-detectar)
    private class TiendaConProductos
    {
        public string Id { get; set; } = default!;
        public List<Producto> Productos { get; set; } = [];
    }

    // Entidad con List<primitivo>
    private class TiendaConTags
    {
        public string Id { get; set; } = default!;
        public List<string> Tags { get; set; } = [];
    }

    // Entidad con propiedad ya configurada
    private class TiendaYaConfigurada
    {
        public string Id { get; set; } = default!;
        public List<Direccion> Direcciones { get; set; } = [];
    }

    #endregion

    #region ProcessEntityTypeAdded Tests

    [Fact]
    public void ProcessEntityTypeAdded_ListOfGeoPoint_AppliesGeoPointType()
    {
        // Arrange
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaConUbicaciones>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{ArrayOfAnnotations.Type}:Ubicaciones");
        annotations[$"{ArrayOfAnnotations.Type}:Ubicaciones"].Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);

        annotations.Should().ContainKey($"{ArrayOfAnnotations.ElementClrType}:Ubicaciones");
        annotations[$"{ArrayOfAnnotations.ElementClrType}:Ubicaciones"].Should().Be(typeof(GeoLocation));

        wasIgnored("Ubicaciones").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_ListOfComplexType_AppliesEmbeddedType()
    {
        // Arrange
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaConDirecciones>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{ArrayOfAnnotations.Type}:Direcciones");
        annotations[$"{ArrayOfAnnotations.Type}:Direcciones"].Should().Be(ArrayOfAnnotations.ArrayType.Embedded);

        annotations.Should().ContainKey($"{ArrayOfAnnotations.ElementClrType}:Direcciones");
        annotations[$"{ArrayOfAnnotations.ElementClrType}:Direcciones"].Should().Be(typeof(Direccion));

        wasIgnored("Direcciones").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_ListOfEntity_IgnoresPropertyForLaterProcessing()
    {
        // Arrange - List<Producto> donde Producto tiene Id
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaConProductos>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert - No aplica ArrayOf annotation todavía, pero SÍ ignora la propiedad
        // para evitar que EF Core cree FK inversa. La propiedad será procesada
        // en ModelFinalizing cuando sepamos si T es una entidad registrada.
        var annotations = getAnnotations();
        annotations.Should().NotContainKey($"{ArrayOfAnnotations.Type}:Productos");
        wasIgnored("Productos").Should().BeTrue(); // Se ignora para evitar FK inversa
    }

    [Fact]
    public void ProcessEntityTypeAdded_ListOfPrimitive_DoesNotApply()
    {
        // Arrange - List<string> no debería aplicar
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaConTags>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert - No debería aplicar para tipos primitivos
        var annotations = getAnnotations();
        annotations.Should().NotContainKey($"{ArrayOfAnnotations.Type}:Tags");
        wasIgnored("Tags").Should().BeFalse();
    }

    [Fact]
    public void ProcessEntityTypeAdded_AlreadyConfigured_DoesNotOverride()
    {
        // Arrange
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaYaConfigurada>(isAlreadyConfigured: true);

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert - No debería modificar la configuración existente
        var annotations = getAnnotations();
        annotations.Should().NotContainKey($"{ArrayOfAnnotations.Type}:Direcciones");
        wasIgnored("Direcciones").Should().BeFalse();
    }

    [Fact]
    public void ProcessEntityTypeAdded_ListOfLatLngGeoPoint_AppliesGeoPointType()
    {
        // Arrange - GeoPoint con Lat/Lng en lugar de Latitude/Longitude
        var convention = new ArrayOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMockForType(typeof(TiendaConGeoPoints));

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{ArrayOfAnnotations.Type}:Puntos");
        annotations[$"{ArrayOfAnnotations.Type}:Puntos"].Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);
    }

    private class TiendaConGeoPoints
    {
        public string Id { get; set; } = default!;
        public List<GeoPoint> Puntos { get; set; } = [];
    }

    #endregion

    #region Helper Methods

    private static (
        Mock<IConventionEntityTypeBuilder>,
        Mock<IConventionContext<IConventionEntityTypeBuilder>>,
        Func<Dictionary<string, object?>>,
        Func<string, bool>)
        CreateEntityTypeBuilderMock<T>(bool isAlreadyConfigured = false)
    {
        return CreateEntityTypeBuilderMockForType(typeof(T), isAlreadyConfigured);
    }

    private static (
        Mock<IConventionEntityTypeBuilder>,
        Mock<IConventionContext<IConventionEntityTypeBuilder>>,
        Func<Dictionary<string, object?>>,
        Func<string, bool>)
        CreateEntityTypeBuilderMockForType(Type clrType, bool isAlreadyConfigured = false)
    {
        var annotations = new Dictionary<string, object?>();
        var ignoredProperties = new HashSet<string>();

        var entityTypeMock = new Mock<IConventionEntityType>();
        var entityTypeBuilderMock = new Mock<IConventionEntityTypeBuilder>();
        var contextMock = new Mock<IConventionContext<IConventionEntityTypeBuilder>>();
        var modelMock = new Mock<IConventionModel>();

        // Setup ClrType
        entityTypeMock.Setup(e => e.ClrType).Returns(clrType);
        entityTypeMock.Setup(e => e.Model).Returns(modelMock.Object);

        // Setup IsArrayOf check (para verificar si ya está configurado)
        // La clave es "Firestore:ArrayOf:Type:{propertyName}"
        // IConventionEntityType hereda de IReadOnlyEntityType, FindAnnotation retorna IAnnotation
        entityTypeMock.As<IReadOnlyEntityType>()
            .Setup(e => e.FindAnnotation(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                if (isAlreadyConfigured && name == $"{ArrayOfAnnotations.Type}:Direcciones")
                {
                    var annotationMock = new Mock<IAnnotation>();
                    annotationMock.Setup(a => a.Value).Returns(ArrayOfAnnotations.ArrayType.Embedded);
                    return annotationMock.Object;
                }
                return null;
            });

        // Setup SetAnnotation (capturamos las anotaciones que se establecen)
        var mutableEntityTypeMock = entityTypeMock.As<IMutableEntityType>();
        mutableEntityTypeMock
            .Setup(e => e.SetAnnotation(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback<string, object?>((name, value) => annotations[name] = value);

        // Setup Ignore (capturamos las propiedades ignoradas)
        entityTypeBuilderMock
            .Setup(b => b.Ignore(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((propName, _) => ignoredProperties.Add(propName))
            .Returns(entityTypeBuilderMock.Object);

        entityTypeBuilderMock.Setup(b => b.Metadata).Returns(entityTypeMock.Object);

        return (
            entityTypeBuilderMock,
            contextMock,
            () => annotations,
            prop => ignoredProperties.Contains(prop)
        );
    }

    #endregion
}
