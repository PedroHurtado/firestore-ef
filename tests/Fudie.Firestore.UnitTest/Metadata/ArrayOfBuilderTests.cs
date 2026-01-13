using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para ArrayOfBuilder y ArrayOfEntityTypeBuilderExtensions.
/// Fase 1: Verifica que la sintaxis fluent funciona y las anotaciones se registran correctamente.
/// </summary>
public class ArrayOfBuilderTests
{
    #region Test Entities - Simplificadas para Fase 1

    // Entidad principal con arrays simples
    private class Tienda
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public List<HorarioAtencion> Horarios { get; set; } = new();
        public List<Ubicacion> Ubicaciones { get; set; } = new();
        public List<Etiqueta> Etiquetas { get; set; } = new();
        public List<Seccion> Secciones { get; set; } = new();
    }

    // Entidad para referencias
    private class Etiqueta
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    // ComplexType simple
    private class HorarioAtencion
    {
        public string Dia { get; set; } = default!;
        public TimeSpan Apertura { get; set; }
        public TimeSpan Cierre { get; set; }
    }

    // ComplexType con Lat/Lng para GeoPoints
    private class Ubicacion
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // ComplexType con array anidado
    private class Seccion
    {
        public string Nombre { get; set; } = default!;
        public List<Producto> Productos { get; set; } = new();
    }

    private class Producto
    {
        public string Nombre { get; set; } = default!;
        public decimal Precio { get; set; }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Crea un ModelBuilder simple para tests.
    /// Para Fase 1 solo necesitamos verificar que las anotaciones se registran.
    /// </summary>
    private static ModelBuilder CreateSimpleModelBuilder()
    {
        return new ModelBuilder();
    }

    private IMutableEntityType ConfigureArrayOfEmbedded()
    {
        var modelBuilder = CreateSimpleModelBuilder();
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Secciones);
            entity.ArrayOf(e => e.Horarios);
        });
        return modelBuilder.Model.FindEntityType(typeof(Tienda))!;
    }

    private IMutableEntityType ConfigureArrayOfGeoPoints()
    {
        var modelBuilder = CreateSimpleModelBuilder();
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Secciones);
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });
        return modelBuilder.Model.FindEntityType(typeof(Tienda))!;
    }

    private IMutableEntityType ConfigureArrayOfReferences()
    {
        var modelBuilder = CreateSimpleModelBuilder();
        modelBuilder.Entity<Etiqueta>(); // Registrar entidad referenciada
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Secciones);
            entity.ArrayOf(e => e.Etiquetas).AsReferences();
        });
        return modelBuilder.Model.FindEntityType(typeof(Tienda))!;
    }

    private IMutableEntityType ConfigureArrayOfNested()
    {
        var modelBuilder = CreateSimpleModelBuilder();
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Etiquetas);
            entity.ArrayOf(e => e.Secciones, seccion =>
            {
                seccion.ArrayOf(s => s.Productos);
            });
        });
        return modelBuilder.Model.FindEntityType(typeof(Tienda))!;
    }

    #endregion

    #region Fase 1 Tests - Sintaxis Fluent

    [Fact]
    public void ArrayOf_Simple_ShouldCompileAndSetEmbeddedAnnotation()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        tiendaType.Should().NotBeNull();
        var arrayType = tiendaType.GetArrayOfType("Horarios");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.Embedded);
    }

    [Fact]
    public void ArrayOf_Simple_ShouldSetElementClrType()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        var elementType = tiendaType.GetArrayOfElementClrType("Horarios");
        elementType.Should().Be(typeof(HorarioAtencion));
    }

    [Fact]
    public void ArrayOf_AsGeoPoints_ShouldSetGeoPointAnnotation()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfGeoPoints();

        // Assert
        var arrayType = tiendaType.GetArrayOfType("Ubicaciones");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);
    }

    [Fact]
    public void ArrayOf_AsReferences_ShouldSetReferenceAnnotation()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfReferences();

        // Assert
        var arrayType = tiendaType.GetArrayOfType("Etiquetas");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.Reference);
    }

    [Fact]
    public void ArrayOf_Nested_ShouldCompileAndSetAnnotation()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfNested();

        // Assert
        tiendaType.GetArrayOfType("Secciones")
            .Should().Be(ArrayOfAnnotations.ArrayType.Embedded);
    }

    [Fact]
    public void ArrayOf_IsArrayOf_ShouldReturnTrueForConfiguredProperty()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        tiendaType.IsArrayOf("Horarios").Should().BeTrue();
        tiendaType.IsArrayOf("Nombre").Should().BeFalse();
    }

    [Fact]
    public void ArrayOf_IsArrayOfEmbedded_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        tiendaType.IsArrayOfEmbedded("Horarios").Should().BeTrue();
        tiendaType.IsArrayOfGeoPoint("Horarios").Should().BeFalse();
        tiendaType.IsArrayOfReference("Horarios").Should().BeFalse();
    }

    [Fact]
    public void ArrayOf_IsArrayOfGeoPoint_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfGeoPoints();

        // Assert
        tiendaType.IsArrayOfGeoPoint("Ubicaciones").Should().BeTrue();
        tiendaType.IsArrayOfEmbedded("Ubicaciones").Should().BeFalse();
    }

    [Fact]
    public void ArrayOf_IsArrayOfReference_ShouldReturnCorrectValue()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfReferences();

        // Assert
        tiendaType.IsArrayOfReference("Etiquetas").Should().BeTrue();
        tiendaType.IsArrayOfEmbedded("Etiquetas").Should().BeFalse();
    }

    #endregion

    #region Shadow Property Tests - Change Tracking

    [Fact]
    public void ArrayOf_ShouldCreateShadowPropertyForChangeTracking()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        var shadowPropertyName = ArrayOfBuilder<Tienda, HorarioAtencion>.GetShadowPropertyName("Horarios");
        var shadowProperty = tiendaType.FindProperty(shadowPropertyName);

        shadowProperty.Should().NotBeNull();
        shadowProperty!.ClrType.Should().Be(typeof(string));
        shadowProperty.IsShadowProperty().Should().BeTrue();
    }

    [Fact]
    public void ArrayOf_ShadowProperty_ShouldHaveTrackerAnnotation()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfEmbedded();

        // Assert
        var shadowPropertyName = ArrayOfBuilder<Tienda, HorarioAtencion>.GetShadowPropertyName("Horarios");
        var shadowProperty = tiendaType.FindProperty(shadowPropertyName);

        var trackerFor = shadowProperty?.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
        trackerFor.Should().Be("Horarios");
    }

    [Fact]
    public void ArrayOf_GetShadowPropertyName_ShouldReturnCorrectFormat()
    {
        // Act
        var result = ArrayOfBuilder<Tienda, HorarioAtencion>.GetShadowPropertyName("Horarios");

        // Assert
        result.Should().Be("__Horarios_Json");
    }

    [Fact]
    public void ArrayOf_AsGeoPoints_ShouldAlsoCreateShadowProperty()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfGeoPoints();

        // Assert
        var shadowPropertyName = ArrayOfBuilder<Tienda, Ubicacion>.GetShadowPropertyName("Ubicaciones");
        var shadowProperty = tiendaType.FindProperty(shadowPropertyName);

        shadowProperty.Should().NotBeNull();
        var trackerFor = shadowProperty?.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
        trackerFor.Should().Be("Ubicaciones");
    }

    [Fact]
    public void ArrayOf_AsReferences_ShouldAlsoCreateShadowProperty()
    {
        // Arrange & Act
        var tiendaType = ConfigureArrayOfReferences();

        // Assert
        var shadowPropertyName = ArrayOfBuilder<Tienda, Etiqueta>.GetShadowPropertyName("Etiquetas");
        var shadowProperty = tiendaType.FindProperty(shadowPropertyName);

        shadowProperty.Should().NotBeNull();
        var trackerFor = shadowProperty?.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
        trackerFor.Should().Be("Etiquetas");
    }

    #endregion
}
