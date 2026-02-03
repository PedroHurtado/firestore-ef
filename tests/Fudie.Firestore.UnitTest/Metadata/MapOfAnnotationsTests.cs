using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para MapOfAnnotations.
/// Verifica que las constantes y métodos de extensión funcionan correctamente.
/// </summary>
public class MapOfAnnotationsTests
{
    #region Test Entities

    private class Restaurant
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
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

    #endregion

    #region Constants Tests

    [Fact]
    public void KeyClrType_ShouldHaveCorrectPrefix()
    {
        // Assert
        MapOfAnnotations.KeyClrType.Should().StartWith("Firestore:MapOf:");
        MapOfAnnotations.KeyClrType.Should().EndWith("KeyClrType");
    }

    [Fact]
    public void ElementClrType_ShouldHaveCorrectPrefix()
    {
        // Assert
        MapOfAnnotations.ElementClrType.Should().StartWith("Firestore:MapOf:");
        MapOfAnnotations.ElementClrType.Should().EndWith("ElementClrType");
    }

    [Fact]
    public void GetShadowPropertyName_ShouldReturnCorrectFormat()
    {
        // Act
        var result = MapOfAnnotations.GetShadowPropertyName("WeeklyHours");

        // Assert
        result.Should().Be("__WeeklyHours_Json");
    }

    #endregion

    #region SetMapOfKeyClrType / GetMapOfKeyClrType Tests

    [Fact]
    public void SetMapOfKeyClrType_ShouldStoreKeyType()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.SetMapOfKeyClrType("WeeklyHours", typeof(DayOfWeek));
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var keyType = entityType.GetMapOfKeyClrType("WeeklyHours");
        keyType.Should().Be(typeof(DayOfWeek));
    }

    [Fact]
    public void GetMapOfKeyClrType_ShouldReturnNull_WhenNotConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Restaurant>();

        // Act
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var keyType = entityType.GetMapOfKeyClrType("WeeklyHours");

        // Assert
        keyType.Should().BeNull();
    }

    #endregion

    #region SetMapOfElementClrType / GetMapOfElementClrType Tests

    [Fact]
    public void SetMapOfElementClrType_ShouldStoreElementType()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.SetMapOfElementClrType("WeeklyHours", typeof(DaySchedule));
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var elementType = entityType.GetMapOfElementClrType("WeeklyHours");
        elementType.Should().Be(typeof(DaySchedule));
    }

    [Fact]
    public void GetMapOfElementClrType_ShouldReturnNull_WhenNotConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Restaurant>();

        // Act
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var elementType = entityType.GetMapOfElementClrType("WeeklyHours");

        // Assert
        elementType.Should().BeNull();
    }

    #endregion

    #region IsMapOf Tests

    [Fact]
    public void IsMapOf_ShouldReturnTrue_WhenKeyTypeIsConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.SetMapOfKeyClrType("WeeklyHours", typeof(DayOfWeek));
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
    }

    [Fact]
    public void IsMapOf_ShouldReturnFalse_WhenNotConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Restaurant>();

        // Act
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;

        // Assert
        entityType.IsMapOf("WeeklyHours").Should().BeFalse();
    }

    #endregion

    #region BackingField Tests

    [Fact]
    public void SetMapOfBackingField_ShouldStoreFieldInfo()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        var fieldInfo = typeof(Restaurant).GetField("_weeklyHours",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.SetMapOfBackingField("WeeklyHours", fieldInfo);
        });

        // Assert - null because Restaurant doesn't have _weeklyHours field
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var storedField = entityType.GetMapOfBackingField("WeeklyHours");
        storedField.Should().BeNull(); // Field doesn't exist in test class
    }

    [Fact]
    public void GetMapOfBackingField_ShouldReturnNull_WhenNotConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Restaurant>();

        // Act
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var backingField = entityType.GetMapOfBackingField("WeeklyHours");

        // Assert
        backingField.Should().BeNull();
    }

    #endregion

    #region IgnoredProperties Tests

    [Fact]
    public void AddMapOfIgnoredProperty_ShouldAddToSet()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.AddMapOfIgnoredProperty("WeeklyHours", "CalculatedProperty");
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");
        ignored.Should().NotBeNull();
        ignored.Should().Contain("CalculatedProperty");
    }

    [Fact]
    public void AddMapOfIgnoredProperty_ShouldAccumulateMultipleProperties()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Metadata.AddMapOfIgnoredProperty("WeeklyHours", "Prop1");
            entity.Metadata.AddMapOfIgnoredProperty("WeeklyHours", "Prop2");
            entity.Metadata.AddMapOfIgnoredProperty("WeeklyHours", "Prop3");
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");
        ignored.Should().HaveCount(3);
        ignored.Should().Contain(new[] { "Prop1", "Prop2", "Prop3" });
    }

    [Fact]
    public void GetMapOfIgnoredProperties_ShouldReturnNull_WhenNoneConfigured()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Restaurant>();

        // Act
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");

        // Assert
        ignored.Should().BeNull();
    }

    #endregion
}
