using Firestore.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.UnitTest.Extensions;

/// <summary>
/// Unit tests for PersistNullValues extension method.
/// </summary>
public class PersistNullValuesExtensionTest
{
    [Fact]
    public void PersistNullValues_SetsAnnotationCorrectly()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NullableField).PersistNullValues();
        });

        var model = modelBuilder.FinalizeModel();
        var entityType = model.FindEntityType(typeof(TestEntity))!;
        var property = entityType.FindProperty(nameof(TestEntity.NullableField))!;

        // Act
        var hasPersistNullValues = property.IsPersistNullValuesEnabled();

        // Assert
        hasPersistNullValues.Should().BeTrue();
    }

    [Fact]
    public void Property_WithoutPersistNullValues_ReturnsFalse()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NullableField); // Explicitly configure but without PersistNullValues
        });

        var model = modelBuilder.FinalizeModel();
        var entityType = model.FindEntityType(typeof(TestEntity))!;
        var property = entityType.FindProperty(nameof(TestEntity.NullableField))!;

        // Act
        var hasPersistNullValues = property.IsPersistNullValuesEnabled();

        // Assert
        hasPersistNullValues.Should().BeFalse();
    }

    [Fact]
    public void PersistNullValues_GenericVersion_SetsAnnotationCorrectly()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property<string?>(e => e.NullableField).PersistNullValues();
        });

        var model = modelBuilder.FinalizeModel();
        var entityType = model.FindEntityType(typeof(TestEntity))!;
        var property = entityType.FindProperty(nameof(TestEntity.NullableField))!;

        // Act
        var hasPersistNullValues = property.IsPersistNullValuesEnabled();

        // Assert
        hasPersistNullValues.Should().BeTrue();
    }

    private class TestEntity
    {
        public string? Id { get; set; }
        public string? NullableField { get; set; }
    }
}
