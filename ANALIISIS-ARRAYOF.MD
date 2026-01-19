# Propiedades de `MenuItem` en EF Core

## Propiedades Escalares (13)

Obtenidas con `entityType.GetProperties()`

| Propiedad | Tipo | Mapeada |
|-----------|------|:-------:|
| Id | `Guid` | ✅ |
| TenantId | `Guid` | ✅ |
| Name | `string` | ✅ |
| Description | `string?` | ✅ |
| ImageUrl | `string?` | ✅ |
| DisplayOrder | `int` | ✅ |
| IsActive | `bool` | ✅ |
| IsHighRiskItem | `bool` | ✅ |
| RequiresAdvanceOrder | `bool` | ✅ |
| MinimumAdvanceOrderQuantity | `int?` | ✅ |
| IsAvailable | `bool` | ✅ |
| IsAlwaysAvailable | `bool` | ✅ |
| AllergenNotes | `string?` | ✅ |

---

## ComplexProperties (2)

Obtenidas con `entityType.GetComplexProperties()`

| Propiedad | Tipo | Razón |
|-----------|------|-------|
| DepositOverride | `ItemDepositOverride?` | Configurado con `ComplexProperty()` |
| NutritionalInfo | `NutritionalInfo?` | Configurado con `ComplexProperty()` |

---

## Navegaciones (2)

Obtenidas con `entityType.GetNavigations()`

| Propiedad | Tipo | Razón |
|-----------|------|-------|
| Allergens | `IReadOnlyCollection<Allergen>` | `Allergen` es entidad, EF Core detecta relación |
| PriceOptions | `IReadOnlyCollection<PriceOption>` | `PriceOption` es entidad, EF Core detecta relación |

---

## Ignoradas Silenciosamente (2)

No aparecen en ninguna colección del modelo

| Propiedad | Tipo | Razón |
|-----------|------|-------|
| AvailableDays | `IReadOnlyCollection<DayOfWeek>` | Sin setter + colección de enum |
| DomainEvents | `IReadOnlyCollection<IDomainEvent>` | Sin setter + colección de interfaz + backing field private |

---

## Reglas de Mapeo de EF Core

| Condición | ¿Mapeada? |
|-----------|:---------:|
| Propiedad con getter/setter | ✅ |
| Expression-bodied (`=>`) sin setter | ❌ |
| Colección de entidad | ✅ (navegación) |
| Colección de interfaz | ❌ |
| Colección de enum sin setter | ❌ |
| `List<enum>` con setter | ✅ (EF Core 8+) |
| Backing field `private` | ❌ (no detectado) |
| Backing field `protected` | ✅ (detectado) |

---

## Definición del Modelo

```csharp
public class MenuItem : AggregateRoot<Guid>
{
    // Escalares
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
    public string? AllergenNotes { get; protected set; }

    // ComplexProperties
    public ItemDepositOverride? DepositOverride { get; protected set; }
    public NutritionalInfo? NutritionalInfo { get; protected set; }

    // Navegaciones (con backing fields)
    protected HashSet<PriceOption> _priceOptions = [];
    public IReadOnlyCollection<PriceOption> PriceOptions => _priceOptions.ToList().AsReadOnly();

    protected HashSet<Allergen> _allergens = [];
    public IReadOnlyCollection<Allergen> Allergens => _allergens.ToList().AsReadOnly();

    // Ignoradas silenciosamente
    protected HashSet<DayOfWeek> _availableDays = [];
    public IReadOnlyCollection<DayOfWeek> AvailableDays => _availableDays.ToList().AsReadOnly();
}

// En AggregateRoot (ignorada)
private readonly List<IDomainEvent> _domainEvents = [];
public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
```

## Configuración en DbContext

```csharp
modelBuilder.Entity<MenuItem>(entity =>
{
    entity.HasQueryFilter(m => m.TenantId == TenantId);

    // ComplexTypes (embedded objects)
    entity.ComplexProperty(m => m.DepositOverride);
    entity.ComplexProperty(m => m.NutritionalInfo);

    // ArrayOf embedded: PriceOptions
    entity.ArrayOf(m => m.PriceOptions, option =>
    {
        option.Ignore(o => o.RequiresMarketPrice);
        option.Ignore(o => o.DisplayPrice);
    });            

    // ArrayOf Reference: Allergens
    entity.ArrayOf(m => m.Allergens).AsReferences();
});
```