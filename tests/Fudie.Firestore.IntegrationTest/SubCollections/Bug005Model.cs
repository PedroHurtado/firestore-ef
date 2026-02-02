namespace Fudie.Firestore.IntegrationTest.SubCollections;

// ============================================================================
// BASE CLASSES
// ============================================================================

/// <summary>
/// Base entity class with Id. Hasheable by Id.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; }

    protected Entity(TId id) => Id = id;

    public bool Equals(Entity<TId>? other) => other is not null && Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// Aggregate root base class.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }
}

// ============================================================================
// ENUMS
// ============================================================================

public enum PortionType
{
    Small = 1,
    Half = 2,
    Full = 3,
    MarketPrice = 4
}

public enum DepositType
{
    PerPerson = 1,
    PercentageOfBill = 2,
    FixedAmount = 3
}

// ============================================================================
// VALUE OBJECTS (Records)
// ============================================================================

/// <summary>
/// Price option for menu items.
/// </summary>
public partial record PriceOption
{
    public PortionType PortionType { get; }
    public decimal? Price { get; }
    public bool IsActive { get; }

    protected PriceOption(PortionType portionType, decimal? price, bool isActive)
    {
        PortionType = portionType;
        Price = price;
        IsActive = isActive;
    }

    // Computed properties (Ignored in DbContext)
    public bool RequiresMarketPrice => PortionType == PortionType.MarketPrice && !Price.HasValue;
    public string DisplayPrice => RequiresMarketPrice ? "S/M" : Price?.ToString("C") ?? "N/A";

    // Factory method for tests
    public static PriceOption Create(PortionType portionType, decimal? price, bool isActive = true)
        => new(portionType, price, isActive);
}

/// <summary>
/// Deposit policy for menus (ComplexProperty).
/// </summary>
public partial record DepositPolicy
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

    // Factory method for tests
    public static DepositPolicy Create(
        DepositType depositType,
        decimal amount,
        decimal? percentage = null,
        decimal? minimumBillForDeposit = null,
        int? minimumGuestsForDeposit = null)
        => new(depositType, amount, percentage, minimumBillForDeposit, minimumGuestsForDeposit);
}

/// <summary>
/// Item deposit override for menu items (ComplexProperty).
/// </summary>
public partial record ItemDepositOverride
{
    public decimal DepositAmount { get; }
    public int? MinimumQuantityForDeposit { get; }

    protected ItemDepositOverride(decimal depositAmount, int? minimumQuantityForDeposit)
    {
        DepositAmount = depositAmount;
        MinimumQuantityForDeposit = minimumQuantityForDeposit;
    }

    public bool AppliesToAllQuantities => !MinimumQuantityForDeposit.HasValue;

    // Factory method for tests
    public static ItemDepositOverride Create(decimal depositAmount, int? minimumQuantityForDeposit = null)
        => new(depositAmount, minimumQuantityForDeposit);
}

/// <summary>
/// Nutritional info for menu items (ComplexProperty).
/// </summary>
public partial record NutritionalInfo
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
        int calories, decimal protein, decimal carbohydrates, decimal fat,
        int servingSize, decimal? fiber, decimal? sugar, decimal? salt)
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

    // Factory method for tests
    public static NutritionalInfo Create(
        int calories, decimal protein, decimal carbohydrates, decimal fat,
        int servingSize, decimal? fiber = null, decimal? sugar = null, decimal? salt = null)
        => new(calories, protein, carbohydrates, fat, servingSize, fiber, sugar, salt);
}

/// <summary>
/// Category item - VALUE OBJECT with Reference to MenuItem.
/// THIS IS THE KEY PART OF THE BUG SCENARIO.
/// </summary>
public partial record CategoryItem
{
    public MenuItem MenuItem { get; }
    public int DisplayOrder { get; }

    private readonly HashSet<PriceOption> _priceOverrides;
    public IReadOnlyCollection<PriceOption> PriceOverrides => _priceOverrides.ToList().AsReadOnly();

    protected CategoryItem(MenuItem menuItem, int displayOrder, HashSet<PriceOption>? priceOverrides)
    {
        MenuItem = menuItem;
        DisplayOrder = displayOrder;
        _priceOverrides = priceOverrides ?? [];
    }

    // Equality by MenuItem.Id
    public virtual bool Equals(CategoryItem? other)
    {
        if (other is null) return false;
        return MenuItem.Id == other.MenuItem.Id;
    }

    public override int GetHashCode() => MenuItem.Id.GetHashCode();

    // Factory method for tests
    public static CategoryItem Create(MenuItem menuItem, int displayOrder, HashSet<PriceOption>? priceOverrides = null)
        => new(menuItem, displayOrder, priceOverrides);
}

// ============================================================================
// ENTITIES
// ============================================================================

/// <summary>
/// Allergen aggregate root.
/// </summary>
public partial class Allergen : AggregateRoot<string>
{
    public string Name { get; protected set; } = string.Empty;
    public string? IconUrl { get; protected set; }
    public bool IsActive { get; protected set; }
    public int DisplayOrder { get; protected set; }

    protected Allergen() : base(string.Empty) { }
    public Allergen(string code) : base(code) { }

    // Factory method for tests
    public static Allergen Create(string code, string name, bool isActive = true, int displayOrder = 0, string? iconUrl = null)
    {
        var allergen = new Allergen(code);
        allergen.Name = name;
        allergen.IsActive = isActive;
        allergen.DisplayOrder = displayOrder;
        allergen.IconUrl = iconUrl;
        return allergen;
    }
}

/// <summary>
/// MenuItem aggregate root.
/// </summary>
public partial class MenuItem : AggregateRoot<Guid>
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
    public string? AllergenNotes { get; protected set; }

    protected HashSet<DayOfWeek> _availableDays = [];
    public IReadOnlyCollection<DayOfWeek> AvailableDays => _availableDays.ToList().AsReadOnly();

    protected HashSet<PriceOption> _priceOptions = [];
    public IReadOnlyCollection<PriceOption> PriceOptions => _priceOptions.ToList().AsReadOnly();

    protected HashSet<Allergen> _allergens = [];
    public IReadOnlyCollection<Allergen> Allergens => _allergens.ToList().AsReadOnly();

    // Computed properties
    public bool IsAvailableToday => IsAlwaysAvailable || _availableDays.Contains(DateTime.Today.DayOfWeek);
    public bool CanBeOrdered => IsActive && IsAvailableToday && IsAvailable;
    public bool HasDepositOverride => DepositOverride != null;
    public bool HasActivePriceOption => _priceOptions.Any(p => p.IsActive);

    protected MenuItem() : base(Guid.Empty) { }
    public MenuItem(Guid id) : base(id) { }

    // Factory method for tests
    public static MenuItem Create(
        Guid id,
        Guid tenantId,
        string name,
        decimal defaultPrice = 10.00m,
        bool isActive = true)
    {
        var item = new MenuItem(id);
        item.TenantId = tenantId;
        item.Name = name;
        item.IsActive = isActive;
        item._priceOptions.Add(PriceOption.Create(PortionType.Full, defaultPrice, true));
        return item;
    }

    // Helper methods for tests
    public void AddPriceOption(PriceOption option) => _priceOptions.Add(option);
    public void AddAllergen(Allergen allergen) => _allergens.Add(allergen);
    public void SetDepositOverride(ItemDepositOverride? deposit) => DepositOverride = deposit;
    public void SetNutritionalInfo(NutritionalInfo? info) => NutritionalInfo = info;
}

/// <summary>
/// MenuCategory entity (SubCollection of Menu).
/// Contains ArrayOf CategoryItem with Reference to MenuItem.
/// </summary>
public partial class MenuCategory : Entity<Guid>
{
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public bool IsActive { get; protected set; }

    // CRITICAL: ArrayOf CategoryItem (ValueObject with Reference)
    protected HashSet<CategoryItem> _items = [];
    public IReadOnlyCollection<CategoryItem> Items => _items.ToList().AsReadOnly();

    protected MenuCategory() : base(Guid.Empty) { }
    public MenuCategory(Guid id) : base(id) { }

    // Factory method for tests
    public static MenuCategory Create(Guid id, string name, int displayOrder = 0, bool isActive = true, string? description = null)
    {
        var category = new MenuCategory(id);
        category.Name = name;
        category.DisplayOrder = displayOrder;
        category.IsActive = isActive;
        category.Description = description;
        return category;
    }

    // Helper methods for tests
    public void AddItem(CategoryItem item) => _items.Add(item);
    public void RemoveItem(CategoryItem item) => _items.Remove(item);
    public void ClearItems() => _items.Clear();
}

/// <summary>
/// Menu aggregate root.
/// Contains SubCollection of MenuCategory.
/// Has ComplexProperty DepositPolicy.
/// </summary>
public partial class Menu : AggregateRoot<Guid>
{
    public Guid TenantId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public bool IsActive { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public DateTime? EffectiveFrom { get; protected set; }
    public DateTime? EffectiveUntil { get; protected set; }

    // ComplexProperty
    public DepositPolicy? DepositPolicy { get; protected set; }

    // SubCollection of MenuCategory
    protected HashSet<MenuCategory> _categories = [];
    public IReadOnlyCollection<MenuCategory> Categories
    {
        get
        {
            Console.WriteLine($"[DEBUG] Categories getter called - Stack: {Environment.StackTrace}");
            return _categories.ToList().AsReadOnly();
        }
    }

    protected Menu() : base(Guid.Empty) { }
    public Menu(Guid id) : base(id) { }

    // Factory method for tests
    public static Menu Create(Guid id, Guid tenantId, string name, bool isActive = false)
    {
        var menu = new Menu(id);
        menu.TenantId = tenantId;
        menu.Name = name;
        menu.IsActive = isActive;
        return menu;
    }

    // Helper methods for tests
    public void AddCategory(MenuCategory category) => _categories.Add(category);
    public void RemoveCategory(MenuCategory category) => _categories.Remove(category);
    public void SetDepositPolicy(DepositPolicy? policy) => DepositPolicy = policy;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}