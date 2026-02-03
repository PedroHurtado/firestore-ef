using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.Schedule;

namespace Fudie.Firestore.IntegrationTest.MapOf;

/// <summary>
/// Tests de integración para verificar que los cambios en MapOf se detectan y actualizan correctamente.
/// Patrón: INSERT → UPDATE → SELECT con DbContext (sin SDK raw)
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class MapOfChangeTrackingTests
{
    private readonly FirestoreTestFixture _fixture;

    public MapOfChangeTrackingTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Update_AddNewKeyToMap_ShouldAddKeyCorrectly()
    {
        // Arrange: Insert schedule with Monday only
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Add Tuesday to the map
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            schedule!.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
                DayOfWeek.Tuesday,
                false,
                [new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0))]
            ));

            await context.SaveChangesAsync();
        }

        // Assert: Verify Tuesday was added
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);
            schedule.WeeklyHours.Should().ContainKey(DayOfWeek.Tuesday);

            var tuesday = schedule.WeeklyHours[DayOfWeek.Tuesday];
            tuesday.IsClosed.Should().BeFalse();
            tuesday.TimeSlots.Should().HaveCount(1);
            tuesday.TimeSlots.First().OpenTime.Should().Be(new TimeOnly(10, 0));
            tuesday.TimeSlots.First().CloseTime.Should().Be(new TimeOnly(18, 0));
        }
    }

    [Fact]
    public async Task Update_ModifyExistingKeyInMap_ShouldUpdateValueCorrectly()
    {
        // Arrange: Insert schedule with Monday
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Modify Monday schedule
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            schedule!.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(8, 0), new TimeOnly(20, 0))] // Changed hours
            ));

            await context.SaveChangesAsync();
        }

        // Assert: Verify Monday was updated
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);

            var monday = schedule.WeeklyHours[DayOfWeek.Monday];
            monday.TimeSlots.Should().HaveCount(1);
            monday.TimeSlots.First().OpenTime.Should().Be(new TimeOnly(8, 0));
            monday.TimeSlots.First().CloseTime.Should().Be(new TimeOnly(20, 0));
        }
    }

    [Fact]
    public async Task Update_RemoveKeyFromMap_ShouldDeleteKeyCorrectly()
    {
        // Arrange: Insert schedule with Monday and Tuesday
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
                DayOfWeek.Tuesday,
                false,
                [new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Remove Tuesday from the map
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            schedule!.RemoveDaySchedule(DayOfWeek.Tuesday);

            await context.SaveChangesAsync();
        }

        // Assert: Verify Tuesday was removed
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);
            schedule.WeeklyHours.Should().NotContainKey(DayOfWeek.Tuesday);
        }
    }

    [Fact]
    public async Task Update_CombinedOperations_ShouldApplyAllChangesCorrectly()
    {
        // Arrange: Insert schedule with Monday and Tuesday
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
                DayOfWeek.Tuesday,
                false,
                [new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Modify Monday, Add Wednesday, Remove Tuesday
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            // Modify existing
            schedule!.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(8, 0), new TimeOnly(16, 0))]
            ));

            // Add new
            schedule.SetDaySchedule(DayOfWeek.Wednesday, new DaySchedule(
                DayOfWeek.Wednesday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            // Remove existing
            schedule.RemoveDaySchedule(DayOfWeek.Tuesday);

            await context.SaveChangesAsync();
        }

        // Assert: Verify all changes were applied
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            // Monday should be modified
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);
            var monday = schedule.WeeklyHours[DayOfWeek.Monday];
            monday.TimeSlots.First().OpenTime.Should().Be(new TimeOnly(8, 0));

            // Wednesday should be added
            schedule.WeeklyHours.Should().ContainKey(DayOfWeek.Wednesday);

            // Tuesday should be removed
            schedule.WeeklyHours.Should().NotContainKey(DayOfWeek.Tuesday);
        }
    }

    [Fact]
    public async Task Update_ClearAllKeys_ShouldRemoveEntireMap()
    {
        // Arrange: Insert schedule with Monday and Tuesday
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
                DayOfWeek.Tuesday,
                false,
                [new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Remove all keys from the map
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            schedule!.RemoveDaySchedule(DayOfWeek.Monday);
            schedule.RemoveDaySchedule(DayOfWeek.Tuesday);

            await context.SaveChangesAsync();
        }

        // Assert: Verify map is empty
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();
            schedule!.WeeklyHours.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Update_NestedArrayOfInMapValue_ShouldUpdateCorrectly()
    {
        // Arrange: Insert schedule with Monday having one time slot
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(12, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Update Monday to have multiple time slots
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            schedule!.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [
                    new TimeSlot(new TimeOnly(9, 0), new TimeOnly(12, 0)),
                    new TimeSlot(new TimeOnly(14, 0), new TimeOnly(18, 0))
                ]
            ));

            await context.SaveChangesAsync();
        }

        // Assert: Verify Monday has two time slots
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);

            var monday = schedule.WeeklyHours[DayOfWeek.Monday];
            monday.TimeSlots.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task Update_MapAndNestedArray_ShouldApplyAllChangesCorrectly()
    {
        // Arrange: Insert schedule with Monday and Tuesday (each with one time slot)
        var scheduleId = Guid.NewGuid();
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = new Helpers.Schedule.Schedule(scheduleId);
            schedule.SetTenantId(Guid.NewGuid());
            schedule.SetName("Restaurant Hours");
            schedule.SetIsActive(true);

            schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
                DayOfWeek.Tuesday,
                false,
                [new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0))]
            ));

            context.Schedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        // Act: Modify Monday's TimeSlots (ArrayOf), Add Wednesday (Map key), Remove Tuesday (Map key)
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            // Modify existing key: change Monday's TimeSlots from 1 to 2 (ArrayOf change)
            schedule!.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
                DayOfWeek.Monday,
                false,
                [
                    new TimeSlot(new TimeOnly(8, 0), new TimeOnly(12, 0)),
                    new TimeSlot(new TimeOnly(14, 0), new TimeOnly(20, 0))
                ]
            ));

            // Add new key: Wednesday (Map change)
            schedule.SetDaySchedule(DayOfWeek.Wednesday, new DaySchedule(
                DayOfWeek.Wednesday,
                false,
                [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0))]
            ));

            // Remove existing key: Tuesday (Map change)
            schedule.RemoveDaySchedule(DayOfWeek.Tuesday);

            await context.SaveChangesAsync();
        }

        // Assert: Verify all changes were applied
        using (var context = _fixture.CreateContext<ScheduleTestDbContext>())
        {
            var schedule = await context.Schedules.FindAsync(scheduleId);
            schedule.Should().NotBeNull();

            // Monday should be modified with 2 time slots (ArrayOf change)
            schedule!.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);
            var monday = schedule.WeeklyHours[DayOfWeek.Monday];
            monday.TimeSlots.Should().HaveCount(2);
            monday.TimeSlots.First().OpenTime.Should().Be(new TimeOnly(8, 0));
            monday.TimeSlots.First().CloseTime.Should().Be(new TimeOnly(12, 0));
            monday.TimeSlots.Last().OpenTime.Should().Be(new TimeOnly(14, 0));
            monday.TimeSlots.Last().CloseTime.Should().Be(new TimeOnly(20, 0));

            // Wednesday should be added (Map change)
            schedule.WeeklyHours.Should().ContainKey(DayOfWeek.Wednesday);
            var wednesday = schedule.WeeklyHours[DayOfWeek.Wednesday];
            wednesday.TimeSlots.Should().HaveCount(1);

            // Tuesday should be removed (Map change)
            schedule.WeeklyHours.Should().NotContainKey(DayOfWeek.Tuesday);
        }
    }
}
