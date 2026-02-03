using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.Schedule;

public class ScheduleTestDbContext(DbContextOptions<ScheduleTestDbContext> options) : DbContext(options)
{
    public DbSet<Schedule> Schedules => Set<Schedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.TenantId).IsRequired();
            entity.Property(s => s.Name).IsRequired();

            // Ignorar propiedades calculadas de la entidad
            entity.Ignore(s => s.HasWeeklyHours);
            entity.Ignore(s => s.HasSpecialDates);
            entity.Ignore(s => s.IsFullyConfigured);

            // MapOf para WeeklyHours: IReadOnlyDictionary<DayOfWeek, DaySchedule>
            entity.MapOf(s => s.WeeklyHours, daySchedule =>
            {
                // Ignorar propiedad calculada del elemento
                daySchedule.Ignore(ds => ds.TotalOpenHours);

                // ArrayOf anidado dentro del MapOf
                daySchedule.ArrayOf(ds => ds.TimeSlots, timeSlot =>
                {
                    // Ignorar propiedad calculada del TimeSlot
                    timeSlot.Ignore(ts => ts.Duration);
                });
            });

            // ArrayOf para SpecialDates: IReadOnlyCollection<SpecialDate>
            entity.ArrayOf(s => s.SpecialDates, specialDate =>
            {
                // Ignorar propiedad calculada
                specialDate.Ignore(sd => sd.TotalOpenHours);

                // ArrayOf anidado dentro del ArrayOf
                specialDate.ArrayOf(sd => sd.TimeSlots, timeSlot =>
                {
                    timeSlot.Ignore(ts => ts.Duration);
                });
            });
        });
    }
}
