using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para MapOfEntityTypeBuilderExtensions.
/// Verifica los métodos de extensión MapOf en EntityTypeBuilder.
/// </summary>
public class MapOfEntityTypeBuilderExtensionsTests
{
    #region Test Entities

    private class Restaurant
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
        public IReadOnlyDictionary<string, MenuSection> MenuSections { get; set; } = default!;
    }

    private class DaySchedule
    {
        public DayOfWeek DayOfWeek { get; set; }
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

    #endregion

    #region Helper Methods

    private static ModelBuilder CreateModelBuilder()
    {
        return new ModelBuilder();
    }

    #endregion

    #region Basic MapOf Extension Tests

    [Fact]
    public void MapOf_ShouldReturnMapOfBuilder()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>? builder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            builder = entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>>();
    }

    [Fact]
    public void MapOf_ShouldRegisterKeyAndElementTypes()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.WeeklyHours);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.GetMapOfKeyClrType("WeeklyHours").Should().Be(typeof(DayOfWeek));
        entityType.GetMapOfElementClrType("WeeklyHours").Should().Be(typeof(DaySchedule));
    }

    [Fact]
    public void MapOf_ShouldExtractCorrectPropertyName()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        string? propertyName = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            var builder = entity.MapOf(e => e.WeeklyHours);
            propertyName = builder.PropertyName;
        });

        // Assert
        propertyName.Should().Be("WeeklyHours");
    }

    #endregion

    #region MapOf With Configure Extension Tests

    [Fact]
    public void MapOf_WithConfigure_ShouldReturnMapOfBuilder()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>? builder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            builder = entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.IsClosed);
            });
        });

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>>();
    }

    [Fact]
    public void MapOf_WithConfigure_ShouldInvokeConfigureAction()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        var configureWasCalled = false;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                configureWasCalled = true;
            });
        });

        // Assert
        configureWasCalled.Should().BeTrue();
    }

    [Fact]
    public void MapOf_WithConfigure_ShouldPassCorrectElementBuilder()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? receivedBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                receivedBuilder = day;
            });
        });

        // Assert
        receivedBuilder.Should().NotBeNull();
        receivedBuilder.Should().BeOfType<MapOfElementBuilder<DaySchedule>>();
    }

    [Fact]
    public void MapOf_WithConfigure_ShouldRegisterKeyAndElementTypes()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.IsClosed);
            });
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.GetMapOfKeyClrType("WeeklyHours").Should().Be(typeof(DayOfWeek));
        entityType.GetMapOfElementClrType("WeeklyHours").Should().Be(typeof(DaySchedule));
    }

    #endregion

    #region Multiple Properties Tests

    [Fact]
    public void MapOf_ShouldSupportMultipleMapOfOnSameEntity()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.MapOf(e => e.WeeklyHours);
            entity.MapOf(e => e.MenuSections);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
        entityType.IsMapOf("MenuSections").Should().BeTrue();
    }

    [Fact]
    public void MapOf_MultipleProperties_ShouldHaveDifferentKeyTypes()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.MapOf(e => e.WeeklyHours);
            entity.MapOf(e => e.MenuSections);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.GetMapOfKeyClrType("WeeklyHours").Should().Be(typeof(DayOfWeek));
        entityType.GetMapOfKeyClrType("MenuSections").Should().Be(typeof(string));
    }

    [Fact]
    public void MapOf_MultipleProperties_ShouldHaveDifferentElementTypes()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.MapOf(e => e.WeeklyHours);
            entity.MapOf(e => e.MenuSections);
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.GetMapOfElementClrType("WeeklyHours").Should().Be(typeof(DaySchedule));
        entityType.GetMapOfElementClrType("MenuSections").Should().Be(typeof(MenuSection));
    }

    #endregion

    #region Original Use Case Test (from spec)

    [Fact]
    public void MapOf_OriginalUseCase_ShouldWorkAsSpecified()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act - This is the original use case from the spec
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.IsClosed);
                day.ArrayOf(d => d.TimeSlots, ts =>
                {
                    ts.Ignore(t => t.Close);
                });
            });
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
        entityType.GetMapOfKeyClrType("WeeklyHours").Should().Be(typeof(DayOfWeek));
        entityType.GetMapOfElementClrType("WeeklyHours").Should().Be(typeof(DaySchedule));
    }

    #endregion

    #region Type Inference Tests

    [Fact]
    public void MapOf_ShouldInferTypesFromExpression()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act - Types should be inferred automatically
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            var builder = entity.MapOf(e => e.WeeklyHours);

            // Assert - Builder should have correct generic types
            builder.Should().BeOfType<MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>>();
        });
    }

    [Fact]
    public void MapOf_WithConfigure_ShouldInferTypesFromExpression()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act - Types should be inferred automatically
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.MenuSections);
            var builder = entity.MapOf(e => e.WeeklyHours, day =>
            {
                // day should be MapOfElementBuilder<DaySchedule>
                day.Property(d => d.IsClosed);
            });

            // Assert - Builder should have correct generic types
            builder.Should().BeOfType<MapOfBuilder<Restaurant, DayOfWeek, DaySchedule>>();
        });
    }

    #endregion
}
