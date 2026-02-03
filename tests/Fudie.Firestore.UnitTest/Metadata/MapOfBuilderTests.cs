using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para MapOfBuilder.
/// El Builder registra los tipos de clave y elemento del diccionario en las anotaciones.
/// </summary>
public class MapOfBuilderTests
{
    #region Test Entities

    private class Restaurant
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
        public IReadOnlyDictionary<string, MenuSection> MenuSections { get; set; } = default!;
        public IReadOnlyDictionary<int, PriceLevel> PriceLevels { get; set; } = default!;
    }

    private class DaySchedule
    {
        public bool IsClosed { get; set; }
        public List<TimeSlot> TimeSlots { get; set; } = new();
    }

    private class TimeSlot
    {
        public string Open { get; set; } = default!;
        public string Close { get; set; } = default!;
    }

    private class MenuSection
    {
        public string Title { get; set; } = default!;
        public List<MenuItem> Items { get; set; } = new();
    }

    private class MenuItem
    {
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    private class PriceLevel
    {
        public string Description { get; set; } = default!;
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }

    #endregion

    #region Helper Methods

    private static ModelBuilder CreateModelBuilder()
    {
        return new ModelBuilder();
    }

    #endregion

    #region Key Type Registration Tests

    [Fact]
    public void MapOf_ShouldRegisterEnumKeyType()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var keyType = entityType.GetMapOfKeyClrType("WeeklyHours");
        keyType.Should().Be(typeof(DayOfWeek));
    }

    [Fact]
    public void MapOf_ShouldRegisterStringKeyType()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.MenuSections);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var keyType = entityType.GetMapOfKeyClrType("MenuSections");
        keyType.Should().Be(typeof(string));
    }

    [Fact]
    public void MapOf_ShouldRegisterIntKeyType()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.PriceLevels);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var keyType = entityType.GetMapOfKeyClrType("PriceLevels");
        keyType.Should().Be(typeof(int));
    }

    #endregion

    #region Element Type Registration Tests

    [Fact]
    public void MapOf_ShouldRegisterElementType()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var elementType = entityType.GetMapOfElementClrType("WeeklyHours");
        elementType.Should().Be(typeof(DaySchedule));
    }

    [Fact]
    public void MapOf_ShouldRegisterDifferentElementTypes()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.WeeklyHours);
            entity.MapOf(e => e.MenuSections);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.GetMapOfElementClrType("WeeklyHours").Should().Be(typeof(DaySchedule));
        entityType.GetMapOfElementClrType("MenuSections").Should().Be(typeof(MenuSection));
    }

    #endregion

    #region PropertyName Tests

    [Fact]
    public void Builder_ShouldExtractCorrectPropertyName()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        string? extractedPropertyName = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            var builder = entity.MapOf(e => e.WeeklyHours);
            extractedPropertyName = builder.PropertyName;
        });

        // Assert
        extractedPropertyName.Should().Be("WeeklyHours");
    }

    [Fact]
    public void Builder_ShouldExtractPropertyName_ForDifferentProperties()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        string? propertyName1 = null;
        string? propertyName2 = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.PriceLevels);
            propertyName1 = entity.MapOf(e => e.WeeklyHours).PropertyName;
            propertyName2 = entity.MapOf(e => e.MenuSections).PropertyName;
        });

        // Assert
        propertyName1.Should().Be("WeeklyHours");
        propertyName2.Should().Be("MenuSections");
    }

    #endregion

    #region IsMapOf Integration Tests

    [Fact]
    public void MapOf_ShouldMakeIsMapOfReturnTrue()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
    }

    [Fact]
    public void IsMapOf_ShouldReturnFalse_ForUnconfiguredProperty()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("MenuSections").Should().BeFalse();
    }

    #endregion

    #region Multiple MapOf Properties Tests

    [Fact]
    public void MapOf_ShouldSupportMultiplePropertiesOnSameEntity()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.MapOf(e => e.WeeklyHours);
            entity.MapOf(e => e.MenuSections);
            entity.MapOf(e => e.PriceLevels);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;

        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
        entityType.IsMapOf("MenuSections").Should().BeTrue();
        entityType.IsMapOf("PriceLevels").Should().BeTrue();

        entityType.GetMapOfKeyClrType("WeeklyHours").Should().Be(typeof(DayOfWeek));
        entityType.GetMapOfKeyClrType("MenuSections").Should().Be(typeof(string));
        entityType.GetMapOfKeyClrType("PriceLevels").Should().Be(typeof(int));
    }

    #endregion

    #region Builder Return Type Tests

    [Fact]
    public void MapOf_ShouldReturnCorrectBuilderType()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>? builder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.Ignore(e => e.PriceLevels);
            builder = entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>>();
    }

    #endregion
}
