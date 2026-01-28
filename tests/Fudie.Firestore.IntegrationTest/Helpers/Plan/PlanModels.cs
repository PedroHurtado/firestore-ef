namespace Fudie.Firestore.IntegrationTest.Helpers.Plan;

// ============================================================================
// ENUMS
// ============================================================================

public enum BillingPeriod
{
    Monthly = 1,
    Quarterly = 2,
    Semester = 3,
    Yearly = 4
}

public enum FeatureType
{
    Boolean = 1,
    Limit = 2,
    Unlimited = 3
}

// ============================================================================
// VALUE OBJECTS
// ============================================================================

public record Currency
{
    public string Code { get; }
    public string Symbol { get; }
    public int DecimalPlaces { get; }

    public Currency(
        string code,
        string symbol,
        int decimalPlaces)
    {
        Code = code;
        Symbol = symbol;
        DecimalPlaces = decimalPlaces;
    }

    public static Currency EUR => new("EUR", "€", 2);
    public static Currency USD => new("USD", "$", 2);
    public static Currency GBP => new("GBP", "£", 2);

    public static Currency FromCode(string code) => code.ToUpper() switch
    {
        "EUR" => EUR,
        "USD" => USD,
        "GBP" => GBP,
        _ => throw new ArgumentException($"Currency {code} not supported", nameof(code))
    };
}

public record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(
        decimal amount,
        Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        return new Money(Amount * factor, Currency);
    }

    public bool IsZero => Amount == 0;
    public bool IsPositive => Amount > 0;
    public bool IsNegative => Amount < 0;
}

public record Feature
{
    public string Code { get; }
    public string Name { get; }
    public string? Description { get; }
    public FeatureType Type { get; }
    public int? Limit { get; }
    public string? Unit { get; }

    public Feature(
        string code,
        string name,
        string? description,
        FeatureType type,
        int? limit = null,
        string? unit = null)
    {
        Code = code;
        Name = name;
        Description = description;
        Type = type;
        Limit = limit;
        Unit = unit;
    }

    public bool IsValid => Type switch
    {
        FeatureType.Limit => Limit.HasValue && Limit > 0,
        FeatureType.Boolean => !Limit.HasValue,
        FeatureType.Unlimited => !Limit.HasValue,
        _ => false
    };

    public string DisplayValue => Type switch
    {
        FeatureType.Limit => $"{Limit} {Unit}",
        FeatureType.Unlimited => "Ilimitado",
        FeatureType.Boolean => "Incluido",
        _ => ""
    };
}

public record PaymentProviderConfig
{
    public string Provider { get; }
    public string ExternalProductId { get; }
    public string ExternalPriceId { get; }
    public bool IsActive { get; }

    public PaymentProviderConfig(
        string provider,
        string externalProductId,
        string externalPriceId,
        bool isActive = true)
    {
        Provider = provider;
        ExternalProductId = externalProductId;
        ExternalPriceId = externalPriceId;
        IsActive = isActive;
    }
}

// ============================================================================
// BASE CLASSES
// ============================================================================

public interface IPlanEntity;

public abstract class PlanEntity<TId>(TId id) : IPlanEntity, IEquatable<PlanEntity<TId>> where TId : notnull
{
    public TId Id { get; init; } = id;

    public override bool Equals(object? obj) => Equals(obj as PlanEntity<TId>);

    public bool Equals(PlanEntity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}

public interface IPlanDomainEvent;

public abstract class PlanAggregateRoot<TId>(TId id) : PlanEntity<TId>(id) where TId : notnull
{
    private readonly List<IPlanDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IPlanDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IPlanDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ============================================================================
// AGGREGATE ROOT
// ============================================================================

public class Plan : PlanAggregateRoot<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Money Price { get; set; } = Money.Zero(Currency.EUR);
    public BillingPeriod BillingPeriod { get; set; }
    public bool IsActive { get; set; }

    public List<Feature> _features = [];
    public IReadOnlyCollection<Feature> Features => _features.ToList().AsReadOnly();

    public HashSet<PaymentProviderConfig> _providerConfigurations = [];
    public IReadOnlyCollection<PaymentProviderConfig> ProviderConfigurations => _providerConfigurations.ToList().AsReadOnly();

    public bool HasActiveProvider => _providerConfigurations.Any(p => p.IsActive);

    public Plan() : base(Guid.Empty) { }
    public Plan(Guid id) : base(id) { }
}