using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para ArrayOfBuilder.
/// El Builder SOLO cambia el tipo de array detectado por convención.
/// La convención se encarga de: detectar arrays, crear anotaciones, crear shadow properties.
/// El Builder se encarga de: cambiar el tipo (AsGeoPoints, AsReferences, AsPrimitive).
/// </summary>
public class ArrayOfBuilderTests
{
    #region Test Entities

    private class Tienda
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public List<HorarioAtencion> Horarios { get; set; } = new();
        public List<Ubicacion> Ubicaciones { get; set; } = new();
        public List<Etiqueta> Etiquetas { get; set; } = new();
        public List<int> Puntuaciones { get; set; } = new();
    }

    private class Etiqueta
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    private class HorarioAtencion
    {
        public string Dia { get; set; } = default!;
        public TimeSpan Apertura { get; set; }
        public TimeSpan Cierre { get; set; }
    }

    private class Ubicacion
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    #endregion

    #region Helper Methods

    private static ModelBuilder CreateSimpleModelBuilder()
    {
        return new ModelBuilder();
    }

    #endregion

    #region AsGeoPoints Tests

    [Fact]
    public void AsGeoPoints_ShouldChangeTypeToGeoPoint()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();

        // Act
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });

        // Assert
        var tiendaType = modelBuilder.Model.FindEntityType(typeof(Tienda))!;
        var arrayType = tiendaType.GetArrayOfType("Ubicaciones");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);
    }

    [Fact]
    public void AsGeoPoints_ShouldReturnBuilderForFluent()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();
        ArrayOfBuilder<Tienda, Ubicacion>? builder = null;

        // Act
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            builder = entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ArrayOfBuilder<Tienda, Ubicacion>>();
    }

    #endregion

    #region AsReferences Tests

    [Fact]
    public void AsReferences_ShouldChangeTypeToReference()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();

        // Act
        modelBuilder.Entity<Etiqueta>(); // Registrar entidad referenciada
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Puntuaciones);
            entity.ArrayOf(e => e.Etiquetas).AsReferences();
        });

        // Assert
        var tiendaType = modelBuilder.Model.FindEntityType(typeof(Tienda))!;
        var arrayType = tiendaType.GetArrayOfType("Etiquetas");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.Reference);
    }

    [Fact]
    public void AsReferences_ShouldReturnBuilderForFluent()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();
        ArrayOfBuilder<Tienda, Etiqueta>? builder = null;

        // Act
        modelBuilder.Entity<Etiqueta>();
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Puntuaciones);
            builder = entity.ArrayOf(e => e.Etiquetas).AsReferences();
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ArrayOfBuilder<Tienda, Etiqueta>>();
    }

    #endregion

    #region AsPrimitive Tests

    [Fact]
    public void AsPrimitive_ShouldChangeTypeToPrimitive()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();

        // Act
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            // Horarios sería detectado como Embedded por convención,
            // pero podemos forzarlo a Primitive con el Builder
            entity.ArrayOf(e => e.Horarios).AsPrimitive();
        });

        // Assert
        var tiendaType = modelBuilder.Model.FindEntityType(typeof(Tienda))!;
        var arrayType = tiendaType.GetArrayOfType("Horarios");
        arrayType.Should().Be(ArrayOfAnnotations.ArrayType.Primitive);
    }

    [Fact]
    public void AsPrimitive_ShouldReturnBuilderForFluent()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();
        ArrayOfBuilder<Tienda, HorarioAtencion>? builder = null;

        // Act
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            builder = entity.ArrayOf(e => e.Horarios).AsPrimitive();
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ArrayOfBuilder<Tienda, HorarioAtencion>>();
    }

    #endregion

    #region PropertyName Tests

    [Fact]
    public void Builder_ShouldExtractCorrectPropertyName()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();
        string? extractedPropertyName = null;

        // Act
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Ubicaciones);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            var builder = entity.ArrayOf(e => e.Horarios);
            extractedPropertyName = builder.PropertyName;
        });

        // Assert
        extractedPropertyName.Should().Be("Horarios");
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void Builder_ShouldSupportFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();

        // Act - Verificar que el encadenamiento compila y funciona
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            // El último método en la cadena gana
            entity.ArrayOf(e => e.Ubicaciones)
                .AsGeoPoints();  // Este es el tipo final
        });

        // Assert
        var tiendaType = modelBuilder.Model.FindEntityType(typeof(Tienda))!;
        tiendaType.GetArrayOfType("Ubicaciones").Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);
    }

    [Fact]
    public void Builder_LastMethodInChainWins()
    {
        // Arrange
        var modelBuilder = CreateSimpleModelBuilder();

        // Act - El último método debería sobrescribir los anteriores
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.Ignore(e => e.Horarios);
            entity.Ignore(e => e.Etiquetas);
            entity.Ignore(e => e.Puntuaciones);
            entity.ArrayOf(e => e.Ubicaciones)
                .AsPrimitive()
                .AsGeoPoints();  // Este gana
        });

        // Assert
        var tiendaType = modelBuilder.Model.FindEntityType(typeof(Tienda))!;
        tiendaType.GetArrayOfType("Ubicaciones").Should().Be(ArrayOfAnnotations.ArrayType.GeoPoint);
    }

    #endregion
}
