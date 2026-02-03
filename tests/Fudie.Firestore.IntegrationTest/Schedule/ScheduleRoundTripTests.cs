using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.Schedule;

namespace Fudie.Firestore.IntegrationTest.Schedule;

/// <summary>
/// Tests de integración para verificar el ciclo completo de escritura y lectura
/// con DbContext para el modelo Schedule que usa MapOf y ArrayOf.
///
/// Estos tests identificarán qué funcionalidades faltan en el Materializer.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ScheduleRoundTripTests
{
    private readonly FirestoreTestFixture _fixture;

    public ScheduleRoundTripTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ========================================================================
    // TEST 1: Entidad básica sin MapOf ni ArrayOf
    // ========================================================================

    [Fact]
    public async Task RoundTrip_BasicSchedule_ShouldWriteAndReadCorrectly()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Principal");
        schedule.SetDescription("Horario de atención al público");
        schedule.SetIsActive(true);

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(scheduleId);
        loaded.TenantId.Should().Be(tenantId);
        loaded.Name.Should().Be("Horario Principal");
        loaded.Description.Should().Be("Horario de atención al público");
        loaded.IsActive.Should().BeTrue();
    }

    // ========================================================================
    // TEST 2: MapOf básico - WeeklyHours con un día
    // ========================================================================

    [Fact]
    public async Task RoundTrip_ScheduleWithOneDay_ShouldWriteAndReadMapOf()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Lunes");
        schedule.SetIsActive(true);

        // Añadir horario del Lunes
        var mondaySchedule = new DaySchedule(
            DayOfWeek.Monday,
            isClosed: false,
            timeSlots: new[]
            {
                new TimeSlot(new TimeOnly(9, 0), new TimeOnly(14, 0)),
                new TimeSlot(new TimeOnly(16, 0), new TimeOnly(20, 0))
            }
        );
        schedule.SetDaySchedule(DayOfWeek.Monday, mondaySchedule);

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.WeeklyHours.Should().HaveCount(1);
        loaded.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);

        var monday = loaded.WeeklyHours[DayOfWeek.Monday];
        monday.DayOfWeek.Should().Be(DayOfWeek.Monday);
        monday.IsClosed.Should().BeFalse();
        monday.TimeSlots.Should().HaveCount(2);

        var firstSlot = monday.TimeSlots.First();
        firstSlot.OpenTime.Should().Be(new TimeOnly(9, 0));
        firstSlot.CloseTime.Should().Be(new TimeOnly(14, 0));
    }

    // ========================================================================
    // TEST 3: MapOf completo - WeeklyHours con varios días
    // ========================================================================

    [Fact]
    public async Task RoundTrip_ScheduleWithMultipleDays_ShouldWriteAndReadAllDays()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Semanal Completo");
        schedule.SetIsActive(true);

        // Lunes a Viernes: 9:00-14:00 y 16:00-20:00
        var weekdaySlots = new[]
        {
            new TimeSlot(new TimeOnly(9, 0), new TimeOnly(14, 0)),
            new TimeSlot(new TimeOnly(16, 0), new TimeOnly(20, 0))
        };

        schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(DayOfWeek.Monday, false, weekdaySlots));
        schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(DayOfWeek.Tuesday, false, weekdaySlots));
        schedule.SetDaySchedule(DayOfWeek.Wednesday, new DaySchedule(DayOfWeek.Wednesday, false, weekdaySlots));
        schedule.SetDaySchedule(DayOfWeek.Thursday, new DaySchedule(DayOfWeek.Thursday, false, weekdaySlots));
        schedule.SetDaySchedule(DayOfWeek.Friday, new DaySchedule(DayOfWeek.Friday, false, weekdaySlots));

        // Sábado: 10:00-14:00 (solo mañana)
        schedule.SetDaySchedule(DayOfWeek.Saturday, new DaySchedule(
            DayOfWeek.Saturday,
            false,
            new[] { new TimeSlot(new TimeOnly(10, 0), new TimeOnly(14, 0)) }
        ));

        // Domingo: Cerrado
        schedule.SetDaySchedule(DayOfWeek.Sunday, new DaySchedule(DayOfWeek.Sunday, true, Array.Empty<TimeSlot>()));

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.WeeklyHours.Should().HaveCount(7);

        // Verificar días laborables
        loaded.WeeklyHours[DayOfWeek.Monday].IsClosed.Should().BeFalse();
        loaded.WeeklyHours[DayOfWeek.Monday].TimeSlots.Should().HaveCount(2);

        // Verificar Sábado
        loaded.WeeklyHours[DayOfWeek.Saturday].IsClosed.Should().BeFalse();
        loaded.WeeklyHours[DayOfWeek.Saturday].TimeSlots.Should().HaveCount(1);

        // Verificar Domingo cerrado
        loaded.WeeklyHours[DayOfWeek.Sunday].IsClosed.Should().BeTrue();
        loaded.WeeklyHours[DayOfWeek.Sunday].TimeSlots.Should().BeEmpty();
    }

    // ========================================================================
    // TEST 4: ArrayOf básico - SpecialDates
    // ========================================================================

    [Fact]
    public async Task RoundTrip_ScheduleWithSpecialDates_ShouldWriteAndReadArrayOf()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario con Festivos");
        schedule.SetIsActive(true);

        // Añadir fechas especiales
        schedule.AddSpecialDate(new SpecialDate(
            new DateOnly(2024, 12, 25),
            isClosed: true,
            reason: "Navidad",
            timeSlots: Array.Empty<TimeSlot>()
        ));

        schedule.AddSpecialDate(new SpecialDate(
            new DateOnly(2024, 12, 31),
            isClosed: false,
            reason: "Nochevieja - Horario reducido",
            timeSlots: new[] { new TimeSlot(new TimeOnly(9, 0), new TimeOnly(14, 0)) }
        ));

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SpecialDates.Should().HaveCount(2);

        var navidad = loaded.SpecialDates.FirstOrDefault(sd => sd.Date == new DateOnly(2024, 12, 25));
        navidad.Should().NotBeNull();
        navidad!.IsClosed.Should().BeTrue();
        navidad.Reason.Should().Be("Navidad");
        navidad.TimeSlots.Should().BeEmpty();

        var nochevieja = loaded.SpecialDates.FirstOrDefault(sd => sd.Date == new DateOnly(2024, 12, 31));
        nochevieja.Should().NotBeNull();
        nochevieja!.IsClosed.Should().BeFalse();
        nochevieja.Reason.Should().Be("Nochevieja - Horario reducido");
        nochevieja.TimeSlots.Should().HaveCount(1);
    }

    // ========================================================================
    // TEST 5: Completo - MapOf + ArrayOf juntos
    // ========================================================================

    [Fact]
    public async Task RoundTrip_FullSchedule_ShouldWriteAndReadMapOfAndArrayOf()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Completo de Tienda");
        schedule.SetDescription("Incluye horarios semanales y fechas especiales");
        schedule.SetIsActive(true);

        // WeeklyHours - MapOf
        schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
            DayOfWeek.Monday,
            false,
            new[] { new TimeSlot(new TimeOnly(9, 0), new TimeOnly(21, 0)) }
        ));

        schedule.SetDaySchedule(DayOfWeek.Sunday, new DaySchedule(
            DayOfWeek.Sunday,
            true,
            Array.Empty<TimeSlot>()
        ));

        // SpecialDates - ArrayOf
        schedule.AddSpecialDate(new SpecialDate(
            new DateOnly(2024, 1, 1),
            true,
            "Año Nuevo",
            Array.Empty<TimeSlot>()
        ));

        schedule.AddSpecialDate(new SpecialDate(
            new DateOnly(2024, 5, 1),
            true,
            "Día del Trabajador",
            Array.Empty<TimeSlot>()
        ));

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();

        // Verificar propiedades básicas
        loaded!.Name.Should().Be("Horario Completo de Tienda");
        loaded.Description.Should().Be("Incluye horarios semanales y fechas especiales");

        // Verificar MapOf
        loaded.WeeklyHours.Should().HaveCount(2);
        loaded.WeeklyHours[DayOfWeek.Monday].IsClosed.Should().BeFalse();
        loaded.WeeklyHours[DayOfWeek.Sunday].IsClosed.Should().BeTrue();

        // Verificar ArrayOf
        loaded.SpecialDates.Should().HaveCount(2);
        loaded.SpecialDates.Should().Contain(sd => sd.Reason == "Año Nuevo");
        loaded.SpecialDates.Should().Contain(sd => sd.Reason == "Día del Trabajador");
    }

    // ========================================================================
    // TEST 6: Verificar que propiedades calculadas no se guardan
    // ========================================================================

    [Fact]
    public async Task RoundTrip_ComputedProperties_ShouldNotAffectRoundTrip()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Test Propiedades Calculadas");
        schedule.SetIsActive(true);

        // Añadir horario para que HasWeeklyHours sea true
        schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
            DayOfWeek.Monday,
            false,
            new[] { new TimeSlot(new TimeOnly(9, 0), new TimeOnly(17, 0)) }
        ));

        // Verificar propiedades calculadas antes de guardar
        schedule.HasWeeklyHours.Should().BeTrue();
        schedule.HasSpecialDates.Should().BeFalse();
        schedule.IsFullyConfigured.Should().BeFalse(); // Solo 1 día de 7

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert - Las propiedades calculadas deben funcionar igual después de leer
        loaded.Should().NotBeNull();
        loaded!.HasWeeklyHours.Should().BeTrue();
        loaded.HasSpecialDates.Should().BeFalse();
        loaded.IsFullyConfigured.Should().BeFalse();

        // Verificar que Duration en TimeSlot funciona
        var mondaySlots = loaded.WeeklyHours[DayOfWeek.Monday].TimeSlots;
        mondaySlots.First().Duration.Should().Be(TimeSpan.FromHours(8));

        // Verificar TotalOpenHours en DaySchedule
        loaded.WeeklyHours[DayOfWeek.Monday].TotalOpenHours.Should().Be(TimeSpan.FromHours(8));
    }

    // ========================================================================
    // TEST 7: MapOf vacío
    // ========================================================================

    [Fact]
    public async Task RoundTrip_EmptyMapOf_ShouldWriteAndReadCorrectly()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Sin Configurar");
        schedule.SetIsActive(false);
        // No añadimos WeeklyHours ni SpecialDates

        // Act - Write
        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Read
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var loaded = await readContext.Schedules.FindAsync(scheduleId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.WeeklyHours.Should().BeEmpty();
        loaded.SpecialDates.Should().BeEmpty();
        loaded.HasWeeklyHours.Should().BeFalse();
        loaded.HasSpecialDates.Should().BeFalse();
    }

    // ========================================================================
    // TEST 8: Proyección con MapOf - Select Name y WeeklyHours
    // ========================================================================

    [Fact]
    public async Task Projection_NameAndMapOf_ShouldMaterializeCorrectly()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using var writeContext = _fixture.CreateContext<ScheduleTestDbContext>();

        var schedule = new Helpers.Schedule.Schedule(scheduleId);
        schedule.SetTenantId(tenantId);
        schedule.SetName("Horario Para Proyección");
        schedule.SetIsActive(true);

        schedule.SetDaySchedule(DayOfWeek.Monday, new DaySchedule(
            DayOfWeek.Monday,
            false,
            new[] { new TimeSlot(new TimeOnly(9, 0), new TimeOnly(14, 0)) }
        ));

        schedule.SetDaySchedule(DayOfWeek.Tuesday, new DaySchedule(
            DayOfWeek.Tuesday,
            false,
            new[] { new TimeSlot(new TimeOnly(10, 0), new TimeOnly(18, 0)) }
        ));

        writeContext.Schedules.Add(schedule);
        await writeContext.SaveChangesAsync();

        // Act - Proyección
        using var readContext = _fixture.CreateContext<ScheduleTestDbContext>();
        var projection = await readContext.Schedules
            .Where(s => s.Id == scheduleId)
            .Select(s => new { s.Name, s.WeeklyHours })
            .FirstOrDefaultAsync();

        // Assert
        projection.Should().NotBeNull();
        projection!.Name.Should().Be("Horario Para Proyección");
        projection.WeeklyHours.Should().HaveCount(2);
        projection.WeeklyHours.Should().ContainKey(DayOfWeek.Monday);
        projection.WeeklyHours.Should().ContainKey(DayOfWeek.Tuesday);
        projection.WeeklyHours[DayOfWeek.Monday].TimeSlots.Should().HaveCount(1);
    }
}
