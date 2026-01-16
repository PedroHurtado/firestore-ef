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

    // ========== FACTORY METHODS ==========

    public static DepositPolicy CreatePerPerson(decimal amountPerPerson, int? minimumGuests = null)
        => new(DepositType.PerPerson, amountPerPerson, null, null, minimumGuests);

    public static DepositPolicy CreatePercentage(decimal percentage, decimal? minimumBill = null)
        => new(DepositType.PercentageOfBill, 0, percentage, minimumBill, null);

    public static DepositPolicy CreateFixedAmount(decimal amount, decimal? minimumBill = null, int? minimumGuests = null)
        => new(DepositType.FixedAmount, amount, null, minimumBill, minimumGuests);

    // ========== DOMAIN METHODS ==========

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

    // ========== FACTORY METHODS ==========

    public static NutritionalInfo Create(
        int calories,
        decimal protein,
        decimal carbohydrates,
        decimal fat,
        int servingSize = 100,
        decimal? fiber = null,
        decimal? sugar = null,
        decimal? salt = null)
        => new(calories, protein, carbohydrates, fat, servingSize, fiber, sugar, salt);

    public static NutritionalInfo CreateBasic(int calories, decimal protein, decimal carbs, decimal fat)
        => new(calories, protein, carbs, fat, 100, null, null, null);

    // ========== DOMAIN METHODS ==========

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

    // ========== FACTORY METHODS ==========

    public static ItemDepositOverride Create(decimal depositAmount, int? minimumQuantity = null)
        => new(depositAmount, minimumQuantity);

    // ========== DOMAIN METHODS ==========

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

    // ========== FACTORY METHODS ==========

    public static PriceOption Create(PortionType portionType, decimal price, bool isActive = true)
        => new(portionType, price, isActive);

    public static PriceOption CreateSmall(decimal price) => new(PortionType.Small, price, true);
    public static PriceOption CreateHalf(decimal price) => new(PortionType.Half, price, true);
    public static PriceOption CreateFull(decimal price) => new(PortionType.Full, price, true);
    public static PriceOption CreateMarketPrice() => new(PortionType.MarketPrice, null, true);

    // ========== DOMAIN METHODS ==========

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

    // ========== FACTORY METHODS ==========

    public static CategoryItem Create(MenuItem menuItem, int displayOrder, params PriceOption[] priceOverrides)
        => new(menuItem, displayOrder, priceOverrides.Length > 0 ? [.. priceOverrides] : null);

    public static CategoryItem Create(MenuItem menuItem, int displayOrder)
        => new(menuItem, displayOrder, null);

    // ========== EQUALITY ==========

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

    // ========== FACTORY METHODS ==========

    public static MenuCategory Create(
        string name,
        int displayOrder,
        string? description = null,
        bool isActive = true,
        Guid? id = null)
    {
        return new MenuCategory(id ?? Guid.NewGuid())
        {
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            IsActive = isActive
        };
    }

    // ========== MUTATORS (for testing) ==========

    public MenuCategory WithItem(CategoryItem item)
    {
        _items.Add(item);
        return this;
    }

    public MenuCategory WithItems(params CategoryItem[] items)
    {
        foreach (var item in items)
            _items.Add(item);
        return this;
    }
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

    // ========== FACTORY METHODS ==========

    public static Menu Create(
        Guid tenantId,
        string name,
        int displayOrder = 0,
        string? description = null,
        bool isActive = true,
        DateTime? effectiveFrom = null,
        DateTime? effectiveUntil = null,
        DepositPolicy? depositPolicy = null,
        Guid? id = null)
    {
        return new Menu(id ?? Guid.NewGuid())
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            IsActive = isActive,
            DisplayOrder = displayOrder,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            DepositPolicy = depositPolicy
        };
    }

    // ========== MUTATORS (for testing) ==========

    public Menu WithDepositPolicy(DepositPolicy policy)
    {
        DepositPolicy = policy;
        return this;
    }

    public Menu WithCategory(MenuCategory category)
    {
        _categories.Add(category);
        return this;
    }

    public Menu WithCategories(params MenuCategory[] categories)
    {
        foreach (var category in categories)
            _categories.Add(category);
        return this;
    }
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

    // ========== FACTORY METHODS ==========

    public static MenuItem Create(
        Guid tenantId,
        string name,
        int displayOrder = 0,
        string? description = null,
        string? imageUrl = null,
        bool isActive = true,
        bool isHighRiskItem = false,
        bool requiresAdvanceOrder = false,
        int? minimumAdvanceOrderQuantity = null,
        bool isAvailable = true,
        bool isAlwaysAvailable = true,
        ItemDepositOverride? depositOverride = null,
        NutritionalInfo? nutritionalInfo = null,
        string? allergenNotes = null,
        Guid? id = null)
    {
        return new MenuItem(id ?? Guid.NewGuid())
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsActive = isActive,
            IsHighRiskItem = isHighRiskItem,
            RequiresAdvanceOrder = requiresAdvanceOrder,
            MinimumAdvanceOrderQuantity = minimumAdvanceOrderQuantity,
            IsAvailable = isAvailable,
            IsAlwaysAvailable = isAlwaysAvailable,
            DepositOverride = depositOverride,
            NutritionalInfo = nutritionalInfo,
            AllergenNotes = allergenNotes
        };
    }

    public static MenuItem CreateSimple(Guid tenantId, string name, Guid? id = null)
        => Create(tenantId, name, id: id);

    // ========== MUTATORS (for testing) ==========

    public MenuItem WithNutritionalInfo(NutritionalInfo info)
    {
        NutritionalInfo = info;
        return this;
    }

    public MenuItem WithDepositOverride(ItemDepositOverride deposit)
    {
        DepositOverride = deposit;
        return this;
    }

    public MenuItem WithPriceOption(PriceOption option)
    {
        _priceOptions.Add(option);
        return this;
    }

    public MenuItem WithPriceOptions(params PriceOption[] options)
    {
        foreach (var option in options)
            _priceOptions.Add(option);
        return this;
    }

    public MenuItem WithAllergen(Allergen allergen)
    {
        _allergens.Add(allergen);
        return this;
    }

    public MenuItem WithAllergens(params Allergen[] allergens)
    {
        foreach (var allergen in allergens)
            _allergens.Add(allergen);
        return this;
    }

    public MenuItem WithAvailableDays(params DayOfWeek[] days)
    {
        foreach (var day in days)
            _availableDays.Add(day);
        return this;
    }
}

public class Allergen : AggregateRoot<string>
{
    public string Name { get; protected set; } = string.Empty;
    public string? IconUrl { get; protected set; }
    public bool IsActive { get; protected set; }
    public int DisplayOrder { get; protected set; }

    protected Allergen() : base(string.Empty) { }

    public Allergen(string code) : base(code) { }

    // ========== FACTORY METHODS ==========

    public static Allergen Create(
        string code,
        string name,
        int displayOrder = 0,
        string? iconUrl = null,
        bool isActive = true)
    {
        return new Allergen(code)
        {
            Name = name,
            IconUrl = iconUrl,
            IsActive = isActive,
            DisplayOrder = displayOrder
        };
    }

    // Common allergens factory methods
    public static Allergen Gluten() => Create("GLUTEN", "Gluten", 1);
    public static Allergen Dairy() => Create("DAIRY", "Lácteos", 2);
    public static Allergen Nuts() => Create("NUTS", "Frutos secos", 3);
    public static Allergen Eggs() => Create("EGGS", "Huevos", 4);
    public static Allergen Shellfish() => Create("SHELLFISH", "Mariscos", 5);
    public static Allergen Soy() => Create("SOY", "Soja", 6);
    public static Allergen Fish() => Create("FISH", "Pescado", 7);
    public static Allergen Peanuts() => Create("PEANUTS", "Cacahuetes", 8);
}