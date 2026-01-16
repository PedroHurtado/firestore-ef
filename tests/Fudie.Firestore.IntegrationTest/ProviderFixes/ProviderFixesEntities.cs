namespace Fudie.Firestore.IntegrationTest.ProviderFixes;

// ============================================================================
// BASE CLASSES
// ============================================================================

public interface IEntity;

public abstract class Entity<TId>(TId id) : IEntity where TId : notnull
{
    public TId Id { get; init; } = id;
}

public interface IDomainEvent;

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ============================================================================
// ENUMS
// ============================================================================

public enum DepositType
{
    PerPerson = 1,
    PercentageOfBill = 2,
    FixedAmount = 3
}

public enum PortionType
{
    Small = 1,
    Half = 2,
    Full = 3,
    MarketPrice = 4
}

// ============================================================================
// VALUE OBJECTS (Records)
// ============================================================================

public record DepositPolicy
{
    public DepositType DepositType { get; }
    public decimal Amount { get; }
    public decimal? Percentage { get; }
    public decimal? MinimumBillForDeposit { get; }
    public int? MinimumGuestsForDeposit { get; }

    protected DepositPolicy(
        DepositType depositType,
        decimal amount,
        decimal? percentage,
        decimal? minimumBillForDeposit,
        int? minimumGuestsForDeposit)
    {
        DepositType = depositType;
        Amount = amount;
        Percentage = percentage;
        MinimumBillForDeposit = minimumBillForDeposit;
        MinimumGuestsForDeposit = minimumGuestsForDeposit;
    }

    public bool IsApplicable(int guestCount, decimal estimatedBill)
    {
        if (MinimumGuestsForDeposit.HasValue && guestCount < MinimumGuestsForDeposit.Value)
            return false;

        if (MinimumBillForDeposit.HasValue && estimatedBill < MinimumBillForDeposit.Value)
            return false;

        return true;
    }

    public decimal CalculateDeposit(int guestCount, decimal estimatedBill)
    {
        return DepositType switch
        {
            DepositType.PerPerson => Amount * guestCount,
            DepositType.PercentageOfBill => estimatedBill * (Percentage!.Value / 100m),
            DepositType.FixedAmount => Amount,
            _ => 0m
        };
    }
}

public record NutritionalInfo
{
    public int Calories { get; }
    public decimal Protein { get; }
    public decimal Carbohydrates { get; }
    public decimal Fat { get; }
    public int ServingSize { get; }
    public decimal? Fiber { get; }
    public decimal? Sugar { get; }
    public decimal? Salt { get; }

    protected NutritionalInfo(
        int calories,
        decimal protein,
        decimal carbohydrates,
        decimal fat,
        int servingSize,
        decimal? fiber,
        decimal? sugar,
        decimal? salt)
    {
        Calories = calories;
        Protein = protein;
        Carbohydrates = carbohydrates;
        Fat = fat;
        ServingSize = servingSize;
        Fiber = fiber;
        Sugar = sugar;
        Salt = salt;
    }

    public NutritionalInfo GetNutritionForPortion(decimal portionPercentage)
    {
        return new NutritionalInfo(
            calories: (int)(Calories * portionPercentage),
            protein: Protein * portionPercentage,
            carbohydrates: Carbohydrates * portionPercentage,
            fat: Fat * portionPercentage,
            servingSize: (int)(ServingSize * portionPercentage),
            fiber: Fiber.HasValue ? Fiber.Value * portionPercentage : null,
            sugar: Sugar.HasValue ? Sugar.Value * portionPercentage : null,
            salt: Salt.HasValue ? Salt.Value * portionPercentage : null);
    }
}

public record ItemDepositOverride
{
    public decimal DepositAmount { get; }
    public int? MinimumQuantityForDeposit { get; }

    protected ItemDepositOverride(
        decimal depositAmount,
        int? minimumQuantityForDeposit)
    {
        DepositAmount = depositAmount;
        MinimumQuantityForDeposit = minimumQuantityForDeposit;
    }

    public bool IsApplicable(int quantity)
    {
        if (MinimumQuantityForDeposit.HasValue)
            return quantity >= MinimumQuantityForDeposit.Value;

        return true;
    }
}

public record PriceOption
{
    public PortionType PortionType { get; }
    public decimal? Price { get; }
    public bool IsActive { get; }

    protected PriceOption(
        PortionType portionType,
        decimal? price,
        bool isActive)
    {
        PortionType = portionType;
        Price = price;
        IsActive = isActive;
    }

    public bool RequiresMarketPrice => PortionType == PortionType.MarketPrice && !Price.HasValue;

    public string DisplayPrice => RequiresMarketPrice ? "S/M" : Price!.Value.ToString("C");
}

public record CategoryItem
{
    public MenuItem MenuItem { get; }
    public int DisplayOrder { get; }

    private readonly HashSet<PriceOption> _priceOverrides;

    public IReadOnlyCollection<PriceOption> PriceOverrides => _priceOverrides.ToList().AsReadOnly();

    protected CategoryItem(
        MenuItem menuItem,
        int displayOrder,
        HashSet<PriceOption>? priceOverrides)
    {
        MenuItem = menuItem;
        DisplayOrder = displayOrder;
        _priceOverrides = priceOverrides ?? [];
    }

    public virtual bool Equals(CategoryItem? other)
    {
        if (other is null) return false;
        return MenuItem.Id == other.MenuItem.Id;
    }

    public override int GetHashCode() => MenuItem.Id.GetHashCode();
}

// ============================================================================
// ENTITIES
// ============================================================================

public class MenuCategory : Entity<Guid>
{
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public bool IsActive { get; protected set; }

    protected HashSet<CategoryItem> _items = [];

    public IReadOnlyCollection<CategoryItem> Items => _items.ToList().AsReadOnly();

    protected MenuCategory() : base(Guid.Empty) { }

    public MenuCategory(Guid id) : base(id) { }
}

// ============================================================================
// AGGREGATE ROOTS
// ============================================================================

public class Menu : AggregateRoot<Guid>
{
    public Guid TenantId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public bool IsActive { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public DateTime? EffectiveFrom { get; protected set; }
    public DateTime? EffectiveUntil { get; protected set; }
    public DepositPolicy? DepositPolicy { get; protected set; }

    protected HashSet<MenuCategory> _categories = [];

    public IReadOnlyCollection<MenuCategory> Categories => _categories.ToList().AsReadOnly();

    protected Menu() : base(Guid.Empty) { }

    public Menu(Guid id) : base(id) { }
}

public class MenuItem : AggregateRoot<Guid>
{
    public Guid TenantId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public string? ImageUrl { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public bool IsActive { get; protected set; }
    public bool IsHighRiskItem { get; protected set; }
    public bool RequiresAdvanceOrder { get; protected set; }
    public int? MinimumAdvanceOrderQuantity { get; protected set; }
    public bool IsAvailable { get; protected set; } = true;
    public bool IsAlwaysAvailable { get; protected set; } = true;
    public ItemDepositOverride? DepositOverride { get; protected set; }
    public NutritionalInfo? NutritionalInfo { get; protected set; }

    protected HashSet<DayOfWeek> _availableDays = [];

    public IReadOnlyCollection<DayOfWeek> AvailableDays => _availableDays.ToList().AsReadOnly();

    protected HashSet<PriceOption> _priceOptions = [];

    public IReadOnlyCollection<PriceOption> PriceOptions => _priceOptions.ToList().AsReadOnly();

    protected HashSet<Allergen> _allergens = [];

    public IReadOnlyCollection<Allergen> Allergens => _allergens.ToList().AsReadOnly();

    public string? AllergenNotes { get; protected set; }

    protected MenuItem() : base(Guid.Empty) { }

    public MenuItem(Guid id) : base(id) { }
}

public class Allergen : AggregateRoot<string>
{
    public string Name { get; protected set; } = string.Empty;
    public string? IconUrl { get; protected set; }
    public bool IsActive { get; protected set; }
    public int DisplayOrder { get; protected set; }

    protected Allergen() : base(string.Empty) { }

    public Allergen(string code) : base(code) { }
}