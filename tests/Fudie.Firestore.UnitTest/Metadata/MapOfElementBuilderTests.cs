using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para MapOfElementBuilder.
/// Verifica que se pueden configurar Property, Reference, ArrayOf, MapOf e Ignore
/// dentro de los elementos del diccionario.
/// </summary>
public class MapOfElementBuilderTests
{
    #region Test Entities

    private class Restaurant
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; } = default!;
        public IReadOnlyDictionary<string, CategoryInfo> Categories { get; set; } = default!;
        public IReadOnlyDictionary<string, NestedMapContainer> NestedMaps { get; set; } = default!;
    }

    private class DaySchedule
    {
        public DayOfWeek DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public string? Notes { get; set; }
        public List<TimeSlot> TimeSlots { get; set; } = new();
        public string CalculatedDescription => IsClosed ? "Closed" : "Open";
    }

    private class TimeSlot
    {
        public string Open { get; set; } = default!;
        public string Close { get; set; } = default!;
    }

    private class CategoryInfo
    {
        public string Name { get; set; } = default!;
        public Category? Category { get; set; }
        public List<Tag> Tags { get; set; } = new();
    }

    private class Category
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private class Tag
    {
        public string Id { get; set; } = default!;
        public string Label { get; set; } = default!;
    }

    private class NestedMapContainer
    {
        public string Title { get; set; } = default!;
        public IReadOnlyDictionary<int, SubItem> SubItems { get; set; } = default!;
    }

    private class SubItem
    {
        public string Value { get; set; } = default!;
    }

    #endregion

    #region Helper Methods

    private static ModelBuilder CreateModelBuilder()
    {
        return new ModelBuilder();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Property_ShouldRegisterScalarProperty()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.IsClosed);
                elementBuilder = day;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedProperties.Should().HaveCount(1);
        elementBuilder.NestedProperties[0].PropertyName.Should().Be("IsClosed");
        elementBuilder.NestedProperties[0].PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void Property_ShouldRegisterMultipleScalarProperties()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.DayOfWeek);
                day.Property(d => d.IsClosed);
                day.Property(d => d.Notes);
                elementBuilder = day;
            });
        });

        // Assert
        elementBuilder!.NestedProperties.Should().HaveCount(3);
        elementBuilder.NestedProperties.Select(p => p.PropertyName)
            .Should().Contain(new[] { "DayOfWeek", "IsClosed", "Notes" });
    }

    [Fact]
    public void Property_ShouldReturnBuilderForFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? result = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                result = day.Property(d => d.IsClosed);
            });
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MapOfElementBuilder<DaySchedule>>();
    }

    #endregion

    #region Reference Tests

    [Fact]
    public void Reference_ShouldRegisterReferenceProperty()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<CategoryInfo>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Category>();
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.Categories, cat =>
            {
                cat.Reference(c => c.Category);
                elementBuilder = cat;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedReferences.Should().HaveCount(1);
        elementBuilder.NestedReferences[0].PropertyName.Should().Be("Category");
        elementBuilder.NestedReferences[0].ReferencedType.Should().Be(typeof(Category));
    }

    [Fact]
    public void Reference_ShouldReturnBuilderForFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<CategoryInfo>? result = null;

        // Act
        modelBuilder.Entity<Category>();
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.Categories, cat =>
            {
                result = cat.Reference(c => c.Category);
            });
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MapOfElementBuilder<CategoryInfo>>();
    }

    #endregion

    #region ArrayOf Tests

    [Fact]
    public void ArrayOf_ShouldRegisterNestedArray()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.ArrayOf(d => d.TimeSlots);
                elementBuilder = day;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedArrays.Should().HaveCount(1);
        elementBuilder.NestedArrays[0].PropertyName.Should().Be("TimeSlots");
        elementBuilder.NestedArrays[0].ElementType.Should().Be(typeof(TimeSlot));
        elementBuilder.NestedArrays[0].NestedBuilder.Should().BeNull();
    }

    [Fact]
    public void ArrayOf_WithConfigure_ShouldRegisterNestedArrayWithBuilder()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.ArrayOf(d => d.TimeSlots, ts =>
                {
                    ts.Ignore(t => t.Close);
                });
                elementBuilder = day;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedArrays.Should().HaveCount(1);
        elementBuilder.NestedArrays[0].PropertyName.Should().Be("TimeSlots");
        elementBuilder.NestedArrays[0].NestedBuilder.Should().NotBeNull();
        elementBuilder.NestedArrays[0].NestedBuilder.Should().BeOfType<ArrayOfElementBuilder<TimeSlot>>();
    }

    [Fact]
    public void ArrayOf_ShouldReturnBuilderForFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? result = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                result = day.ArrayOf(d => d.TimeSlots);
            });
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MapOfElementBuilder<DaySchedule>>();
    }

    #endregion

    #region MapOf (Nested) Tests

    [Fact]
    public void MapOf_ShouldRegisterNestedMap()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<NestedMapContainer>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.Categories);
            entity.MapOf(e => e.NestedMaps, container =>
            {
                container.MapOf(c => c.SubItems);
                elementBuilder = container;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedMaps.Should().HaveCount(1);
        elementBuilder.NestedMaps[0].PropertyName.Should().Be("SubItems");
        elementBuilder.NestedMaps[0].KeyType.Should().Be(typeof(int));
        elementBuilder.NestedMaps[0].ElementType.Should().Be(typeof(SubItem));
        elementBuilder.NestedMaps[0].NestedBuilder.Should().BeNull();
    }

    [Fact]
    public void MapOf_WithConfigure_ShouldRegisterNestedMapWithBuilder()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<NestedMapContainer>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.Categories);
            entity.MapOf(e => e.NestedMaps, container =>
            {
                container.MapOf(c => c.SubItems, sub =>
                {
                    sub.Property(s => s.Value);
                });
                elementBuilder = container;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedMaps.Should().HaveCount(1);
        elementBuilder.NestedMaps[0].NestedBuilder.Should().NotBeNull();
        elementBuilder.NestedMaps[0].NestedBuilder.Should().BeOfType<MapOfElementBuilder<SubItem>>();
    }

    [Fact]
    public void MapOf_ShouldReturnBuilderForFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<NestedMapContainer>? result = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.WeeklyHours);
            entity.Ignore(e => e.Categories);
            entity.MapOf(e => e.NestedMaps, container =>
            {
                result = container.MapOf(c => c.SubItems);
            });
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MapOfElementBuilder<NestedMapContainer>>();
    }

    #endregion

    #region Ignore Tests

    [Fact]
    public void Ignore_ShouldAddPropertyToIgnoredList()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Ignore(d => d.CalculatedDescription);
            });
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");
        ignored.Should().NotBeNull();
        ignored.Should().Contain("CalculatedDescription");
    }

    [Fact]
    public void Ignore_ShouldSupportMultipleIgnoredProperties()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Ignore(d => d.CalculatedDescription);
                day.Ignore(d => d.Notes);
            });
        });

        // Assert
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");
        ignored.Should().HaveCount(2);
        ignored.Should().Contain(new[] { "CalculatedDescription", "Notes" });
    }

    [Fact]
    public void Ignore_ShouldReturnBuilderForFluentChaining()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? result = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                result = day.Ignore(d => d.CalculatedDescription);
            });
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MapOfElementBuilder<DaySchedule>>();
    }

    #endregion

    #region Full Composition Tests

    [Fact]
    public void FullComposition_ShouldSupportAllMethodsCombined()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();
        MapOfElementBuilder<DaySchedule>? elementBuilder = null;

        // Act
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.Categories);
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.DayOfWeek)
                   .Property(d => d.IsClosed)
                   .ArrayOf(d => d.TimeSlots, ts =>
                   {
                       ts.Ignore(t => t.Close);
                   })
                   .Ignore(d => d.CalculatedDescription);
                elementBuilder = day;
            });
        });

        // Assert
        elementBuilder.Should().NotBeNull();
        elementBuilder!.NestedProperties.Should().HaveCount(2);
        elementBuilder.NestedArrays.Should().HaveCount(1);

        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        var ignored = entityType.GetMapOfIgnoredProperties("WeeklyHours");
        ignored.Should().Contain("CalculatedDescription");
    }

    [Fact]
    public void FluentChaining_ShouldWorkAcrossAllMethods()
    {
        // Arrange
        var modelBuilder = CreateModelBuilder();

        // Act & Assert - Should compile and not throw
        modelBuilder.Entity<Category>();
        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Ignore(e => e.NestedMaps);
            entity.MapOf(e => e.WeeklyHours, day =>
            {
                day.Property(d => d.IsClosed)
                   .Property(d => d.Notes)
                   .ArrayOf(d => d.TimeSlots)
                   .Ignore(d => d.CalculatedDescription);
            });
            entity.MapOf(e => e.Categories, cat =>
            {
                cat.Property(c => c.Name)
                   .Reference(c => c.Category)
                   .ArrayOf(c => c.Tags);
            });
        });

        // If we get here without exception, the fluent API works
        var entityType = modelBuilder.Model.FindEntityType(typeof(Restaurant))!;
        entityType.IsMapOf("WeeklyHours").Should().BeTrue();
        entityType.IsMapOf("Categories").Should().BeTrue();
    }

    #endregion
}
