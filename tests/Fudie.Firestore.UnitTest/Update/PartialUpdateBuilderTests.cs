using Fudie.Firestore.EntityFrameworkCore.ChangeTracking;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Fudie.Firestore.UnitTest.Update;

/// <summary>
/// Tests for change detection patterns used by PartialUpdateBuilder.
/// Verifies that EF Core properly detects modifications to simple properties,
/// complex properties, and ArrayOf properties (via shadow properties).
/// Full integration testing of PartialUpdateBuilder is done in integration tests.
/// </summary>
public class PartialUpdateBuilderTests
{
    #region Test Entities

    private class Customer
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int Age { get; set; }
        public Address Address { get; set; } = new();
        public List<OpeningHour> OpeningHours { get; set; } = new();
    }

    private class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? ZipCode { get; set; }
    }

    private class OpeningHour
    {
        public string Day { get; set; } = default!;
        public string OpenTime { get; set; } = default!;
        public string? CloseTime { get; set; }
    }

    #endregion

    #region Test DbContext

    private class TestDbContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ComplexProperty(e => e.Address).IsRequired();
                entity.ArrayOf(e => e.OpeningHours);
            });
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

    #region Helper Methods

    private static EntityEntry<Customer> SetupTrackedCustomer(TestDbContext context, Customer customer)
    {
        context.Customers.Add(customer);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Re-attach as unchanged
        context.Customers.Attach(customer);
        var entry = context.Entry(customer);

        // Initialize ArrayOf shadow properties
        InitializeArrayOfShadowProperties(entry);

        entry.State = EntityState.Unchanged;
        return entry;
    }

    private static void InitializeArrayOfShadowProperties(EntityEntry entry)
    {
        var entityType = entry.Metadata;

        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor == null) continue;

            var arrayProp = entry.Entity.GetType().GetProperty(trackerFor);
            if (arrayProp == null) continue;

            var arrayValue = arrayProp.GetValue(entry.Entity);
            var json = System.Text.Json.JsonSerializer.Serialize(arrayValue);

            var shadowProp = entry.Property(property.Name);
            shadowProp.CurrentValue = json;
            shadowProp.OriginalValue = json;
        }
    }

    #endregion

    #region Simple Property Detection Tests

    [Fact]
    public void PropertyEntry_WhenModified_ShouldHaveIsModifiedTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Phone = "+1-555-1234"
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Modify Phone
        customer.Phone = "+1-555-5678";
        entry.DetectChanges();

        // Assert
        entry.Property(nameof(Customer.Phone)).IsModified.Should().BeTrue();
        entry.Property(nameof(Customer.Name)).IsModified.Should().BeFalse();
        entry.Property(nameof(Customer.Age)).IsModified.Should().BeFalse();
    }

    [Fact]
    public void PropertyEntry_WhenSetToNull_ShouldHaveIsModifiedTrueAndNullValues()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Phone = "+1-555-1234"
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Set Phone to null
        customer.Phone = null;
        entry.DetectChanges();

        // Assert
        entry.Property(nameof(Customer.Phone)).IsModified.Should().BeTrue();
        entry.Property(nameof(Customer.Phone)).CurrentValue.Should().BeNull();
        entry.Property(nameof(Customer.Phone)).OriginalValue.Should().Be("+1-555-1234");
    }

    [Fact]
    public void PropertyEntry_WhenMultipleModified_ShouldDetectAll()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Phone = "+1-555-1234",
            Age = 30
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Modify multiple properties
        customer.Name = "Jane Doe";
        customer.Age = 31;
        entry.DetectChanges();

        // Assert
        entry.Property(nameof(Customer.Name)).IsModified.Should().BeTrue();
        entry.Property(nameof(Customer.Age)).IsModified.Should().BeTrue();
        entry.Property(nameof(Customer.Phone)).IsModified.Should().BeFalse();
    }

    [Fact]
    public void PropertyEntry_WhenNoChanges_AllShouldBeUnmodified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe"
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - No modifications
        entry.DetectChanges();

        // Assert - No non-PK properties should be modified
        entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey())
            .Any(p => p.IsModified)
            .Should().BeFalse();
    }

    #endregion

    #region Complex Property Detection Tests

    [Fact]
    public void ComplexPropertyEntry_WhenFieldModified_ShouldDetect()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Modify Street
        customer.Address.Street = "456 Broadway";
        entry.DetectChanges();

        // Assert
        var addressComplex = entry.ComplexProperty(nameof(Customer.Address));
        addressComplex.Property(nameof(Address.Street)).IsModified.Should().BeTrue();
        addressComplex.Property(nameof(Address.City)).IsModified.Should().BeFalse();
    }

    [Fact]
    public void ComplexPropertyEntry_WhenFieldSetToNull_ShouldDetect()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Set ZipCode to null
        customer.Address.ZipCode = null;
        entry.DetectChanges();

        // Assert
        var addressComplex = entry.ComplexProperty(nameof(Customer.Address));
        addressComplex.Property(nameof(Address.ZipCode)).IsModified.Should().BeTrue();
        addressComplex.Property(nameof(Address.ZipCode)).CurrentValue.Should().BeNull();
    }

    [Fact]
    public void ComplexPropertyEntry_WhenMultipleFieldsModified_ShouldDetectAll()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York"
            }
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Modify Street and City
        customer.Address.Street = "456 Broadway";
        customer.Address.City = "Brooklyn";
        entry.DetectChanges();

        // Assert
        var addressComplex = entry.ComplexProperty(nameof(Customer.Address));
        addressComplex.Property(nameof(Address.Street)).IsModified.Should().BeTrue();
        addressComplex.Property(nameof(Address.City)).IsModified.Should().BeTrue();
    }

    #endregion

    #region ArrayOf Shadow Property Detection Tests

    [Fact]
    public void ArrayOfShadowProperty_WhenElementAdded_ShouldMarkModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        var entry = SetupTrackedCustomer(context, customer);
        var shadowPropName = ArrayOfBuilder<Customer, OpeningHour>.GetShadowPropertyName("OpeningHours");

        // Act - Add new element
        customer.OpeningHours.Add(new OpeningHour { Day = "Tuesday", OpenTime = "09:00", CloseTime = "18:00" });
        ArrayOfChangeTracker.SyncArrayOfChanges(context);
        entry.DetectChanges();

        // Assert
        entry.Property(shadowPropName).IsModified.Should().BeTrue();
    }

    [Fact]
    public void ArrayOfShadowProperty_WhenElementRemoved_ShouldMarkModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" },
                new() { Day = "Tuesday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        var entry = SetupTrackedCustomer(context, customer);
        var shadowPropName = ArrayOfBuilder<Customer, OpeningHour>.GetShadowPropertyName("OpeningHours");

        // Act - Remove element
        customer.OpeningHours.RemoveAt(1);
        ArrayOfChangeTracker.SyncArrayOfChanges(context);
        entry.DetectChanges();

        // Assert
        entry.Property(shadowPropName).IsModified.Should().BeTrue();
    }

    [Fact]
    public void ArrayOfShadowProperty_WhenElementModified_ShouldMarkModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        var entry = SetupTrackedCustomer(context, customer);
        var shadowPropName = ArrayOfBuilder<Customer, OpeningHour>.GetShadowPropertyName("OpeningHours");

        // Act - Modify element
        customer.OpeningHours[0].OpenTime = "10:00";
        ArrayOfChangeTracker.SyncArrayOfChanges(context);
        entry.DetectChanges();

        // Assert
        entry.Property(shadowPropName).IsModified.Should().BeTrue();
    }

    [Fact]
    public void ArrayOfShadowProperty_WhenNoChanges_ShouldNotBeModified()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "Test Store",
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        var entry = SetupTrackedCustomer(context, customer);
        var shadowPropName = ArrayOfBuilder<Customer, OpeningHour>.GetShadowPropertyName("OpeningHours");

        // Act - No modifications, just sync
        ArrayOfChangeTracker.SyncArrayOfChanges(context);
        entry.DetectChanges();

        // Assert
        entry.Property(shadowPropName).IsModified.Should().BeFalse();
    }

    #endregion

    #region Mixed Changes Detection Tests

    [Fact]
    public void MixedChanges_ShouldDetectAllModifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var customer = new Customer
        {
            Id = "cust-1",
            Name = "John Doe",
            Phone = "+1-555-1234",
            Address = new Address { Street = "123 Main St", City = "NYC" },
            OpeningHours = new List<OpeningHour>
            {
                new() { Day = "Monday", OpenTime = "09:00", CloseTime = "18:00" }
            }
        };

        var entry = SetupTrackedCustomer(context, customer);

        // Act - Modify multiple things
        customer.Name = "Jane Doe";             // Simple property
        customer.Phone = null;                   // Set to null
        customer.Address.City = "Brooklyn";      // Complex property field
        customer.OpeningHours.Add(new OpeningHour { Day = "Tuesday", OpenTime = "09:00" }); // Array add

        ArrayOfChangeTracker.SyncArrayOfChanges(context);
        entry.DetectChanges();

        // Assert - All modifications detected
        entry.Property(nameof(Customer.Name)).IsModified.Should().BeTrue();
        entry.Property(nameof(Customer.Phone)).IsModified.Should().BeTrue();
        entry.ComplexProperty(nameof(Customer.Address)).Property(nameof(Address.City)).IsModified.Should().BeTrue();

        var shadowPropName = ArrayOfBuilder<Customer, OpeningHour>.GetShadowPropertyName("OpeningHours");
        entry.Property(shadowPropName).IsModified.Should().BeTrue();
    }

    #endregion

    #region FieldValue Tests

    [Fact]
    public void FieldValueDelete_ShouldBeSentinel()
    {
        // Assert - FieldValue.Delete is a special sentinel
        var delete = FieldValue.Delete;
        delete.Should().NotBeNull();
    }

    [Fact]
    public void FieldValueServerTimestamp_ShouldBeSentinel()
    {
        // Assert - FieldValue.ServerTimestamp is a special sentinel
        var timestamp = FieldValue.ServerTimestamp;
        timestamp.Should().NotBeNull();
    }

    [Fact]
    public void FieldValueArrayUnion_ShouldCreateSentinel()
    {
        // Arrange
        var values = new object[] { "a", "b", "c" };

        // Act
        var union = FieldValue.ArrayUnion(values);

        // Assert
        union.Should().NotBeNull();
    }

    [Fact]
    public void FieldValueArrayRemove_ShouldCreateSentinel()
    {
        // Arrange
        var values = new object[] { "a", "b" };

        // Act
        var remove = FieldValue.ArrayRemove(values);

        // Assert
        remove.Should().NotBeNull();
    }

    #endregion
}
