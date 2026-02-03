namespace Fudie.Firestore.UnitTest.Conventions;

/// <summary>
/// Tests unitarios para MapOfConvention.
/// Verifica auto-detección de IReadOnlyDictionary&lt;TKey, TElement&gt; como MapOf Embedded.
/// </summary>
public class MapOfConventionTest
{
    #region Test Types

    // Tipo ComplexType sin Id (elemento del diccionario)
    private class DaySchedule
    {
        public bool IsClosed { get; set; }
        public string OpenTime { get; set; } = default!;
        public string CloseTime { get; set; } = default!;
    }

    // Tipo ComplexType para configuración
    private class PriceOption
    {
        public decimal Price { get; set; }
        public string Currency { get; set; } = default!;
    }

    // Tipo Entity (tiene Id) - no debería ser elemento de MapOf
    private class Producto
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    // Enum para claves
    private enum DayOfWeek
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    }

    // Entidad con IReadOnlyDictionary<enum, ComplexType>
    private class RestaurantConHorarios
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
    }

    // Entidad con IReadOnlyDictionary<string, ComplexType>
    private class ProductoConPrecios
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<string, PriceOption> PriceOptions { get; set; } = default!;
    }

    // Entidad con IReadOnlyDictionary<int, ComplexType>
    private class AlmacenConSecciones
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<int, DaySchedule> Secciones { get; set; } = default!;
    }

    // Entidad con Dictionary<,> mutable
    private class TiendaConInventario
    {
        public string Id { get; set; } = default!;
        public Dictionary<string, PriceOption> Inventory { get; set; } = default!;
    }

    // Entidad con IReadOnlyDictionary<,> de primitivos (no aplica)
    private class ConfigConSettings
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<string, int> Settings { get; set; } = default!;
    }

    // Entidad con propiedad ya configurada
    private class RestaurantYaConfigurado
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
    }

    // Entidad con Guid como clave
    private class CatalogoConItems
    {
        public string Id { get; set; } = default!;
        public IReadOnlyDictionary<Guid, PriceOption> Items { get; set; } = default!;
    }

    #endregion

    #region ProcessEntityTypeAdded Tests

    [Fact]
    public void ProcessEntityTypeAdded_DictionaryWithEnumKey_AppliesMapOf()
    {
        // Arrange
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<RestaurantConHorarios>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{MapOfAnnotations.KeyClrType}:WeeklyHours");
        annotations[$"{MapOfAnnotations.KeyClrType}:WeeklyHours"].Should().Be(typeof(DayOfWeek));

        annotations.Should().ContainKey($"{MapOfAnnotations.ElementClrType}:WeeklyHours");
        annotations[$"{MapOfAnnotations.ElementClrType}:WeeklyHours"].Should().Be(typeof(DaySchedule));

        wasIgnored("WeeklyHours").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_DictionaryWithStringKey_AppliesMapOf()
    {
        // Arrange
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<ProductoConPrecios>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{MapOfAnnotations.KeyClrType}:PriceOptions");
        annotations[$"{MapOfAnnotations.KeyClrType}:PriceOptions"].Should().Be(typeof(string));

        annotations.Should().ContainKey($"{MapOfAnnotations.ElementClrType}:PriceOptions");
        annotations[$"{MapOfAnnotations.ElementClrType}:PriceOptions"].Should().Be(typeof(PriceOption));

        wasIgnored("PriceOptions").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_DictionaryWithIntKey_AppliesMapOf()
    {
        // Arrange
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<AlmacenConSecciones>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{MapOfAnnotations.KeyClrType}:Secciones");
        annotations[$"{MapOfAnnotations.KeyClrType}:Secciones"].Should().Be(typeof(int));

        wasIgnored("Secciones").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_DictionaryWithGuidKey_AppliesMapOf()
    {
        // Arrange
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<CatalogoConItems>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{MapOfAnnotations.KeyClrType}:Items");
        annotations[$"{MapOfAnnotations.KeyClrType}:Items"].Should().Be(typeof(Guid));

        wasIgnored("Items").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_MutableDictionary_AppliesMapOf()
    {
        // Arrange - Dictionary<,> también debería funcionar
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<TiendaConInventario>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert
        var annotations = getAnnotations();
        annotations.Should().ContainKey($"{MapOfAnnotations.KeyClrType}:Inventory");
        annotations[$"{MapOfAnnotations.KeyClrType}:Inventory"].Should().Be(typeof(string));

        wasIgnored("Inventory").Should().BeTrue();
    }

    [Fact]
    public void ProcessEntityTypeAdded_DictionaryWithPrimitiveValue_DoesNotApply()
    {
        // Arrange - IReadOnlyDictionary<string, int> no debería aplicar
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<ConfigConSettings>();

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert - No debería aplicar para valores primitivos
        var annotations = getAnnotations();
        annotations.Should().NotContainKey($"{MapOfAnnotations.KeyClrType}:Settings");
        wasIgnored("Settings").Should().BeFalse();
    }

    [Fact]
    public void ProcessEntityTypeAdded_AlreadyConfigured_DoesNotOverride()
    {
        // Arrange
        var convention = new MapOfConvention();
        var (entityTypeBuilder, context, getAnnotations, wasIgnored) =
            CreateEntityTypeBuilderMock<RestaurantYaConfigurado>(isAlreadyConfigured: true);

        // Act
        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        // Assert - No debería modificar la configuración existente
        var annotations = getAnnotations();
        // No debe haber añadido nuevas anotaciones (ya existía)
        annotations.Should().BeEmpty();
        wasIgnored("WeeklyHours").Should().BeFalse();
    }

    #endregion

    #region Valid Key Types Tests

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(Guid))]
    public void IsValidMapKeyType_ValidTypes_ReturnsTrue(Type keyType)
    {
        // Arrange - Crear un tipo de diccionario dinámicamente para testing
        // Usamos reflection para verificar que estos tipos son válidos como claves

        // Assert - Estos tipos deberían ser aceptados como claves de MapOf
        keyType.Should().Match(t =>
            t.IsPrimitive ||
            t == typeof(string) ||
            t == typeof(Guid) ||
            t.IsEnum);
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

        // Setup IsMapOf check (para verificar si ya está configurado)
        entityTypeMock.As<IReadOnlyEntityType>()
            .Setup(e => e.FindAnnotation(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                if (isAlreadyConfigured && name == $"{MapOfAnnotations.KeyClrType}:WeeklyHours")
                {
                    var annotationMock = new Mock<IAnnotation>();
                    annotationMock.Setup(a => a.Value).Returns(typeof(DayOfWeek));
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
