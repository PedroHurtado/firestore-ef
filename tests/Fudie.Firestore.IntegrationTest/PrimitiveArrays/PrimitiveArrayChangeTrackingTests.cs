using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;

namespace Fudie.Firestore.IntegrationTest.PrimitiveArrays;

/// <summary>
/// Tests de integración para verificar que el change tracking funciona correctamente
/// para List&lt;T&gt; de tipos primitivos: modificar, agregar y eliminar elementos.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class PrimitiveArrayChangeTrackingTests
{
    private readonly FirestoreTestFixture _fixture;

    public PrimitiveArrayChangeTrackingTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ========================================================================
    // LIST<STRING> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListString_ModifyElement_ShouldPersistChange()
    {
        // Arrange - Create entity with initial values
        var id = FirestoreTestFixture.GenerateId("ct-str");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "StringChangeTrackingTest",
                Tags = ["tag1", "tag2", "tag3"]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Tags[1] = "modified-tag2";
            await context.SaveChangesAsync();
        }

        // Assert - Verify change was persisted
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Tags.Should().HaveCount(3);
            retrieved.Tags.Should().ContainInOrder("tag1", "modified-tag2", "tag3");
        }
    }

    [Fact]
    public async Task ListString_AddElement_ShouldPersistChange()
    {
        // Arrange - Create entity with initial values
        var id = FirestoreTestFixture.GenerateId("ct-str");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "StringAddTest",
                Tags = ["tag1", "tag2"]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Add new element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Tags.Add("tag3");
            entity.Tags.Add("tag4");
            await context.SaveChangesAsync();
        }

        // Assert - Verify additions were persisted
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Tags.Should().HaveCount(4);
            retrieved.Tags.Should().ContainInOrder("tag1", "tag2", "tag3", "tag4");
        }
    }

    [Fact]
    public async Task ListString_RemoveElement_ShouldPersistChange()
    {
        // Arrange - Create entity with initial values
        var id = FirestoreTestFixture.GenerateId("ct-str");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "StringRemoveTest",
                Tags = ["tag1", "tag2", "tag3", "tag4"]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Remove elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Tags.Remove("tag2");
            entity.Tags.RemoveAt(entity.Tags.Count - 1); // Remove last element (tag4)
            await context.SaveChangesAsync();
        }

        // Assert - Verify removals were persisted
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Tags.Should().HaveCount(2);
            retrieved.Tags.Should().ContainInOrder("tag1", "tag3");
        }
    }

    [Fact]
    public async Task ListString_ClearAndReplace_ShouldPersistChange()
    {
        // Arrange - Create entity with initial values
        var id = FirestoreTestFixture.GenerateId("ct-str");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "StringClearTest",
                Tags = ["old1", "old2", "old3"]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Clear and add new elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Tags.Clear();
            entity.Tags.Add("new1");
            entity.Tags.Add("new2");
            await context.SaveChangesAsync();
        }

        // Assert - Verify clear and new additions were persisted
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Tags.Should().HaveCount(2);
            retrieved.Tags.Should().ContainInOrder("new1", "new2");
        }
    }

    [Fact]
    public async Task ListString_MultipleOperations_ShouldPersistAllChanges()
    {
        // Arrange - Create entity with initial values
        var id = FirestoreTestFixture.GenerateId("ct-str");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "StringMultiOpTest",
                Tags = ["a", "b", "c", "d"]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Multiple operations: modify, add, remove
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Tags[0] = "A"; // Modify first
            entity.Tags.Remove("c"); // Remove 'c'
            entity.Tags.Add("e"); // Add new
            entity.Tags.Insert(1, "B"); // Insert at position
            await context.SaveChangesAsync();
        }

        // Assert - Verify all changes were persisted
        // Original: [a, b, c, d] -> [A, b, c, d] -> [A, b, d] -> [A, b, d, e] -> [A, B, b, d, e]
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Tags.Should().HaveCount(5);
            retrieved.Tags.Should().ContainInOrder("A", "B", "b", "d", "e");
        }
    }

    // ========================================================================
    // LIST<INT> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListInt_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-int");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "IntChangeTrackingTest",
                Quantities = [10, 20, 30]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Quantities[1] = 200;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Quantities.Should().HaveCount(3);
            retrieved.Quantities.Should().ContainInOrder(10, 200, 30);
        }
    }

    [Fact]
    public async Task ListInt_AddElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-int");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "IntAddTest",
                Quantities = [10, 20]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Add new elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Quantities.Add(30);
            entity.Quantities.Add(40);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Quantities.Should().HaveCount(4);
            retrieved.Quantities.Should().ContainInOrder(10, 20, 30, 40);
        }
    }

    [Fact]
    public async Task ListInt_RemoveElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-int");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "IntRemoveTest",
                Quantities = [10, 20, 30, 40]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Remove elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Quantities.Remove(20);
            entity.Quantities.RemoveAt(entity.Quantities.Count - 1);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Quantities.Should().HaveCount(2);
            retrieved.Quantities.Should().ContainInOrder(10, 30);
        }
    }

    [Fact]
    public async Task ListInt_ClearAndReplace_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-int");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "IntClearTest",
                Quantities = [100, 200, 300]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Clear and add new elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Quantities.Clear();
            entity.Quantities.Add(1);
            entity.Quantities.Add(2);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Quantities.Should().HaveCount(2);
            retrieved.Quantities.Should().ContainInOrder(1, 2);
        }
    }

    // ========================================================================
    // LIST<LONG> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListLong_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-long");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "LongChangeTrackingTest",
                BigNumbers = [1000000000L, 2000000000L, 3000000000L]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.BigNumbers[1] = 9999999999L;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.BigNumbers.Should().HaveCount(3);
            retrieved.BigNumbers.Should().ContainInOrder(1000000000L, 9999999999L, 3000000000L);
        }
    }

    [Fact]
    public async Task ListLong_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-long");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "LongAddRemoveTest",
                BigNumbers = [100L, 200L, 300L]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Add and remove elements
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.BigNumbers.Remove(200L);
            entity.BigNumbers.Add(400L);
            entity.BigNumbers.Add(500L);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.BigNumbers.Should().HaveCount(4);
            retrieved.BigNumbers.Should().ContainInOrder(100L, 300L, 400L, 500L);
        }
    }

    // ========================================================================
    // LIST<DOUBLE> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListDouble_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-double");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DoubleChangeTrackingTest",
                Measurements = [1.5, 2.5, 3.5]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Measurements[1] = 99.99;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Measurements.Should().HaveCount(3);
            retrieved.Measurements[0].Should().BeApproximately(1.5, 0.001);
            retrieved.Measurements[1].Should().BeApproximately(99.99, 0.001);
            retrieved.Measurements[2].Should().BeApproximately(3.5, 0.001);
        }
    }

    [Fact]
    public async Task ListDouble_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-double");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DoubleAddRemoveTest",
                Measurements = [1.1, 2.2, 3.3]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Measurements.RemoveAt(1);
            entity.Measurements.Add(4.4);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Measurements.Should().HaveCount(3);
            retrieved.Measurements[0].Should().BeApproximately(1.1, 0.001);
            retrieved.Measurements[1].Should().BeApproximately(3.3, 0.001);
            retrieved.Measurements[2].Should().BeApproximately(4.4, 0.001);
        }
    }

    // ========================================================================
    // LIST<DECIMAL> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListDecimal_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-decimal");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DecimalChangeTrackingTest",
                Prices = [19.99m, 29.99m, 39.99m]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Prices[1] = 99.99m;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Prices.Should().HaveCount(3);
            retrieved.Prices.Should().ContainInOrder(19.99m, 99.99m, 39.99m);
        }
    }

    [Fact]
    public async Task ListDecimal_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-decimal");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DecimalAddRemoveTest",
                Prices = [10.50m, 20.50m, 30.50m]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Prices.Remove(20.50m);
            entity.Prices.Add(40.50m);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Prices.Should().HaveCount(3);
            retrieved.Prices.Should().ContainInOrder(10.50m, 30.50m, 40.50m);
        }
    }

    // ========================================================================
    // LIST<BOOL> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListBool_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-bool");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "BoolChangeTrackingTest",
                Flags = [true, false, true]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Flags[1] = true;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Flags.Should().HaveCount(3);
            retrieved.Flags.Should().ContainInOrder(true, true, true);
        }
    }

    [Fact]
    public async Task ListBool_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-bool");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "BoolAddRemoveTest",
                Flags = [true, false]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Flags.RemoveAt(0);
            entity.Flags.Add(true);
            entity.Flags.Add(false);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Flags.Should().HaveCount(3);
            retrieved.Flags.Should().ContainInOrder(false, true, false);
        }
    }

    // ========================================================================
    // LIST<DATETIME> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListDateTime_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-datetime");
        var date1 = new DateTime(2024, 1, 1, 0, 0, 0);
        var date2 = new DateTime(2024, 6, 15, 0, 0, 0);
        var date3 = new DateTime(2024, 12, 31, 0, 0, 0);
        var newDate = new DateTime(2025, 7, 4, 0, 0, 0);

        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DateTimeChangeTrackingTest",
                EventDates = [date1, date2, date3]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.EventDates[1] = newDate;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.EventDates.Should().HaveCount(3);
            retrieved.EventDates[0].Should().Be(date1);
            retrieved.EventDates[1].Should().Be(newDate);
            retrieved.EventDates[2].Should().Be(date3);
        }
    }

    [Fact]
    public async Task ListDateTime_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-datetime");
        var date1 = new DateTime(2024, 1, 1, 0, 0, 0);
        var date2 = new DateTime(2024, 6, 15, 0, 0, 0);
        var date3 = new DateTime(2024, 12, 31, 0, 0, 0);

        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "DateTimeAddRemoveTest",
                EventDates = [date1, date2]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.EventDates.Remove(date1);
            entity.EventDates.Add(date3);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.EventDates.Should().HaveCount(2);
            retrieved.EventDates[0].Should().Be(date2);
            retrieved.EventDates[1].Should().Be(date3);
        }
    }

    // ========================================================================
    // LIST<ENUM> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListEnum_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-enum");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "EnumChangeTrackingTest",
                Priorities = [Priority.Low, Priority.Medium, Priority.High]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Priorities[1] = Priority.Critical;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Priorities.Should().HaveCount(3);
            retrieved.Priorities.Should().ContainInOrder(Priority.Low, Priority.Critical, Priority.High);
        }
    }

    [Fact]
    public async Task ListEnum_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-enum");
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "EnumAddRemoveTest",
                Priorities = [Priority.Low, Priority.High]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.Priorities.Remove(Priority.Low);
            entity.Priorities.Add(Priority.Critical);
            entity.Priorities.Add(Priority.Medium);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.Priorities.Should().HaveCount(3);
            retrieved.Priorities.Should().ContainInOrder(Priority.High, Priority.Critical, Priority.Medium);
        }
    }

    // ========================================================================
    // LIST<GUID> CHANGE TRACKING
    // ========================================================================

    [Fact]
    public async Task ListGuid_ModifyElement_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-guid");
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();
        var newGuid = Guid.NewGuid();

        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "GuidChangeTrackingTest",
                ExternalIds = [guid1, guid2, guid3]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act - Modify an element
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.ExternalIds[1] = newGuid;
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.ExternalIds.Should().HaveCount(3);
            retrieved.ExternalIds.Should().ContainInOrder(guid1, newGuid, guid3);
        }
    }

    [Fact]
    public async Task ListGuid_AddAndRemove_ShouldPersistChange()
    {
        // Arrange
        var id = FirestoreTestFixture.GenerateId("ct-guid");
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = new PrimitiveArrayEntity
            {
                Id = id,
                Name = "GuidAddRemoveTest",
                ExternalIds = [guid1, guid2]
            };
            context.PrimitiveArrays.Add(entity);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var entity = await context.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            entity!.ExternalIds.Remove(guid1);
            entity.ExternalIds.Add(guid3);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var readContext = _fixture.CreateContext<PrimitiveArrayTestDbContext>())
        {
            var retrieved = await readContext.PrimitiveArrays.FirstOrDefaultAsync(e => e.Id == id);
            retrieved.Should().NotBeNull();
            retrieved!.ExternalIds.Should().HaveCount(2);
            retrieved.ExternalIds.Should().ContainInOrder(guid2, guid3);
        }
    }
}
