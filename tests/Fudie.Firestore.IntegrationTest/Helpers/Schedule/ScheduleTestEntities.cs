namespace Fudie.Firestore.IntegrationTest.Helpers.Schedule;

// ============================================
// BASE CLASS: Entity<T>
// ============================================

public abstract class Entity<T> : IEquatable<Entity<T>> where T : notnull
{
    public T Id { get; protected set; }

    protected Entity(T id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<T> entity && Equals(entity);
    }

    public bool Equals(Entity<T>? other)
    {
        return other is not null && EqualityComparer<T>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<T>.Default.GetHashCode(Id);
    }

    public static bool operator ==(Entity<T>? left, Entity<T>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<T>? left, Entity<T>? right)
    {
        return !Equals(left, right);
    }
}

// ============================================
// SCHEDULE AGGREGATE
// ============================================

public partial class Schedule : Entity<Guid>
{
    public Guid TenantId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public bool IsActive { get; protected set; }

    protected Dictionary<DayOfWeek, DaySchedule> _weeklyHours = [];
    public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours => _weeklyHours.AsReadOnly();

    protected HashSet<SpecialDate> _specialDates = [];
    public IReadOnlyCollection<SpecialDate> SpecialDates => _specialDates.ToList().AsReadOnly();

    // Propiedades calculadas - deben ignorarse en serialización
    public bool HasWeeklyHours => _weeklyHours.Any();
    public bool HasSpecialDates => _specialDates.Any();
    public bool IsFullyConfigured => _weeklyHours.Count == 7;

    protected Schedule() : base(Guid.Empty) { }
    public Schedule(Guid id) : base(id) { }

    // Métodos para actualizar propiedades protected
    public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    public void SetName(string name) => Name = name;
    public void SetDescription(string? description) => Description = description;
    public void SetIsActive(bool isActive) => IsActive = isActive;

    public void SetDaySchedule(DayOfWeek day, DaySchedule schedule)
    {
        _weeklyHours[day] = schedule;
    }

    public void RemoveDaySchedule(DayOfWeek day)
    {
        _weeklyHours.Remove(day);
    }

    public void AddSpecialDate(SpecialDate specialDate)
    {
        _specialDates.Add(specialDate);
    }

    public void RemoveSpecialDate(SpecialDate specialDate)
    {
        _specialDates.Remove(specialDate);
    }

    public void ClearSpecialDates()
    {
        _specialDates.Clear();
    }
}

// ============================================
// VALUE OBJECT: TimeSlot
// ============================================

public partial record TimeSlot
{
    public TimeOnly OpenTime { get; }
    public TimeOnly CloseTime { get; }

    // Propiedad calculada - debe ignorarse en serialización
    public TimeSpan Duration => CloseTime - OpenTime;

    protected TimeSlot() { }

    public TimeSlot(TimeOnly openTime, TimeOnly closeTime)
    {
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    public bool Contains(TimeOnly time) => time >= OpenTime && time <= CloseTime;
    public bool OverlapsWith(TimeSlot other) => OpenTime < other.CloseTime && CloseTime > other.OpenTime;
}

// ============================================
// VALUE OBJECT: DaySchedule
// ============================================

public partial record DaySchedule
{
    public DayOfWeek DayOfWeek { get; }
    public bool IsClosed { get; }
    public IReadOnlyCollection<TimeSlot> TimeSlots { get; }

    // Propiedad calculada - debe ignorarse en serialización
    public TimeSpan TotalOpenHours => TimeSlots.Aggregate(TimeSpan.Zero, (sum, ts) => sum + ts.Duration);

    protected DaySchedule()
    {
        TimeSlots = Array.Empty<TimeSlot>();
    }

    public DaySchedule(DayOfWeek dayOfWeek, bool isClosed, IReadOnlyCollection<TimeSlot> timeSlots)
    {
        DayOfWeek = dayOfWeek;
        IsClosed = isClosed;
        TimeSlots = timeSlots;
    }

    public bool IsOpenAt(TimeOnly time) => !IsClosed && TimeSlots.Any(ts => ts.Contains(time));
}

// ============================================
// VALUE OBJECT: SpecialDate
// ============================================

public partial record SpecialDate
{
    public DateOnly Date { get; }
    public bool IsClosed { get; }
    public string Reason { get; }
    public IReadOnlyCollection<TimeSlot> TimeSlots { get; }

    // Propiedad calculada - debe ignorarse en serialización
    public TimeSpan TotalOpenHours => IsClosed ? TimeSpan.Zero : TimeSlots.Aggregate(TimeSpan.Zero, (sum, ts) => sum + ts.Duration);

    protected SpecialDate()
    {
        Reason = string.Empty;
        TimeSlots = Array.Empty<TimeSlot>();
    }

    public SpecialDate(DateOnly date, bool isClosed, string reason, IReadOnlyCollection<TimeSlot> timeSlots)
    {
        Date = date;
        IsClosed = isClosed;
        Reason = reason;
        TimeSlots = timeSlots;
    }

    public bool IsOpenAt(TimeOnly time) => !IsClosed && TimeSlots.Any(ts => ts.Contains(time));
}
