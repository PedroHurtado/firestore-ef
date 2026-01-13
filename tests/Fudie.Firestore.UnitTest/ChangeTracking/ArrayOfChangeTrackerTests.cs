using Fudie.Firestore.EntityFrameworkCore.ChangeTracking;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.UnitTest.ChangeTracking;

/// <summary>
/// Tests for ArrayOfChangeTracker helper.
/// Verifies that ArrayOf changes are properly detected and entities are marked as Modified.
/// </summary>
public class ArrayOfChangeTrackerTests
{
    #region Test Entities

    private class Store
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public List<OpeningHour> OpeningHours { get; set; } = new();
    }

    private class OpeningHour
    {
        public string Day { get; set; } = default!;
        public string OpenTime { get; set; } = default!;
        public string CloseTime { get; set; } = default!;
    }

    #endregion

    #region Test DbContext

    private class TestDbContext : DbContext
    {
        public DbSet<Store> Stores => Set<Store>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Store>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ArrayOf(e => e.OpeningHours);
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ArrayOfChangeTracker.SyncArrayOfChanges(this);
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ArrayOfChangeTracker.SyncArrayOfChanges(this);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    private static TestDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }

    #endregion

    #region SyncArrayOfChanges Tests

    [Fact]
    public void SyncArrayOfChanges_WhenArrayElementModified_ShouldMarkEntityAsModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var store = new Store
        {
            Id = "store-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        context.Stores.Add(store);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach as unchanged
        context.Stores.Attach(store);
        var originalState = context.Entry(store).State;

        // Initialize shadow property (simulating what TrackingHandler does)
        var entityType = context.Model.FindEntityType(typeof(Store))!;
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var entry = context.Entry(store);
        var initialJson = System.Text.Json.JsonSerializer.Serialize(store.OpeningHours);
        entry.Property(shadowPropName).CurrentValue = initialJson;
        entry.Property(shadowPropName).OriginalValue = initialJson;
        entry.State = EntityState.Unchanged;

        // Act - Modify array element
        store.OpeningHours[0].OpenTime = "10:00";
        ArrayOfChangeTracker.SyncArrayOfChanges(context);

        // Assert
        var newState = context.Entry(store).State;
        originalState.Should().Be(EntityState.Unchanged);
        newState.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void SyncArrayOfChanges_WhenNoChanges_ShouldKeepEntityUnchanged()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var store = new Store
        {
            Id = "store-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        context.Stores.Add(store);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach as unchanged with initialized shadow property
        context.Stores.Attach(store);
        var entityType = context.Model.FindEntityType(typeof(Store))!;
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var entry = context.Entry(store);
        var initialJson = System.Text.Json.JsonSerializer.Serialize(store.OpeningHours);
        entry.Property(shadowPropName).CurrentValue = initialJson;
        entry.Property(shadowPropName).OriginalValue = initialJson;
        entry.State = EntityState.Unchanged;

        // Act - No modifications
        ArrayOfChangeTracker.SyncArrayOfChanges(context);

        // Assert
        context.Entry(store).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public void SyncArrayOfChanges_WhenElementAdded_ShouldMarkEntityAsModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var store = new Store
        {
            Id = "store-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        context.Stores.Add(store);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach as unchanged with initialized shadow property
        context.Stores.Attach(store);
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var entry = context.Entry(store);
        var initialJson = System.Text.Json.JsonSerializer.Serialize(store.OpeningHours);
        entry.Property(shadowPropName).CurrentValue = initialJson;
        entry.Property(shadowPropName).OriginalValue = initialJson;
        entry.State = EntityState.Unchanged;

        // Act - Add new element
        store.OpeningHours.Add(new OpeningHour { Day = "Tuesday", OpenTime = "09:00", CloseTime = "18:00" });
        ArrayOfChangeTracker.SyncArrayOfChanges(context);

        // Assert
        context.Entry(store).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void SyncArrayOfChanges_WhenElementRemoved_ShouldMarkEntityAsModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var store = new Store
        {
            Id = "store-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" },
                new() { Day = "Tuesday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        context.Stores.Add(store);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach as unchanged with initialized shadow property
        context.Stores.Attach(store);
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var entry = context.Entry(store);
        var initialJson = System.Text.Json.JsonSerializer.Serialize(store.OpeningHours);
        entry.Property(shadowPropName).CurrentValue = initialJson;
        entry.Property(shadowPropName).OriginalValue = initialJson;
        entry.State = EntityState.Unchanged;

        // Act - Remove element
        store.OpeningHours.RemoveAt(1);
        ArrayOfChangeTracker.SyncArrayOfChanges(context);

        // Assert
        context.Entry(store).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void SyncArrayOfChanges_WhenEntityAlreadyModified_ShouldRemainModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var store = new Store
        {
            Id = "store-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        context.Stores.Add(store);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach and modify Name first
        context.Stores.Attach(store);
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var entry = context.Entry(store);
        var initialJson = System.Text.Json.JsonSerializer.Serialize(store.OpeningHours);
        entry.Property(shadowPropName).CurrentValue = initialJson;
        entry.Property(shadowPropName).OriginalValue = initialJson;

        store.Name = "Updated Store";
        entry.State.Should().Be(EntityState.Modified);

        // Act - Also modify array
        store.OpeningHours[0].OpenTime = "10:00";
        ArrayOfChangeTracker.SyncArrayOfChanges(context);

        // Assert
        context.Entry(store).State.Should().Be(EntityState.Modified);
    }

    #endregion

    #region Shadow Property Naming Tests

    [Fact]
    public void ShadowProperty_ShouldBeCreatedWithCorrectName()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var entityType = context.Model.FindEntityType(typeof(Store))!;

        // Act
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var shadowProperty = entityType.FindProperty(shadowPropName);

        // Assert
        shadowProperty.Should().NotBeNull();
        shadowProperty!.Name.Should().Be("__OpeningHours_Json");
    }

    [Fact]
    public void ShadowProperty_ShouldHaveCorrectAnnotation()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var entityType = context.Model.FindEntityType(typeof(Store))!;
        var shadowPropName = ArrayOfBuilder<Store, OpeningHour>.GetShadowPropertyName("OpeningHours");
        var shadowProperty = entityType.FindProperty(shadowPropName);

        // Act
        var trackerFor = shadowProperty?.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;

        // Assert
        trackerFor.Should().Be("OpeningHours");
    }

    #endregion
}
