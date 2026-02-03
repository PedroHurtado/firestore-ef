# MapOf Builder — Feature Specification

## Context

The Fudie Firestore EF Core provider currently supports three metadata builders:

- **`ArrayOf`** — maps `IEnumerable<TElement>` to a Firestore array of maps.
- **`Reference`** — maps a navigation property to a Firestore document reference.
- **`SubCollection`** — maps a 1:N relationship to a Firestore subcollection.

A new requirement has appeared: persisting a `Dictionary<TKey, TElement>` as a **Firestore Map** where each key becomes a Map field and each value is a nested Map (which may itself contain arrays, references, or other nested maps).

## What is MapOf

`MapOf` maps a `IReadOnlyDictionary<TKey, TElement>` property to a native Firestore Map field.

### Firestore representation example

Given this domain model:

```csharp
public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours => ...
```

The Firestore document field would look like:

```
weeklyHours: {
  "Monday": {
    dayOfWeek: 1,
    isClosed: false,
    timeSlots: [
      { open: "09:00", close: "14:00" },
      { open: "17:00", close: "22:00" }
    ]
  },
  "Tuesday": {
    dayOfWeek: 2,
    isClosed: true,
    timeSlots: []
  }
}
```

## Public API

### Extension methods on `EntityTypeBuilder<TEntity>`

Follow the same pattern as `ArrayOfEntityTypeBuilderExtensions`. Create a new static class `MapOfEntityTypeBuilderExtensions`:

```csharp
public static class MapOfEntityTypeBuilderExtensions
{
    public static MapOfBuilder<TEntity, TKey, TElement> MapOf<TEntity, TKey, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IReadOnlyDictionary<TKey, TElement>>> propertyExpression)
        where TEntity : class
        where TElement : class;

    public static MapOfBuilder<TEntity, TKey, TElement> MapOf<TEntity, TKey, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IReadOnlyDictionary<TKey, TElement>>> propertyExpression,
        Action<MapOfElementBuilder<TElement>> configure)
        where TEntity : class
        where TElement : class;
}
```

### MapOfBuilder<TEntity, TKey, TElement>

Returned by the extension methods. Stores metadata about the Map property (property name, key type, element type). Follow the same structure as `ArrayOfBuilder`.

### MapOfElementBuilder<TElement>

Configures the inner structure of the Map value (`TElement`). Must expose the **same primitives** that `ArrayOfElementBuilder<TElement>` exposes:

- `Property(...)` — maps a scalar property of the element.
- `ArrayOf(...)` — maps a nested array within the element.
- `Reference(...)` — maps a reference property within the element.
- `MapOf(...)` — maps a nested dictionary within the element (for Map of Maps).

The element builder is fully composable: the value of a dictionary can contain any combination of properties, arrays, references, and nested maps.

## Reference implementation

Use `ArrayOf` as the reference pattern for everything:

- `ArrayOfEntityTypeBuilderExtensions` → `MapOfEntityTypeBuilderExtensions`
- `ArrayOfBuilder<TEntity, TElement>` → `MapOfBuilder<TEntity, TKey, TElement>`
- `ArrayOfElementBuilder<TElement>` → `MapOfElementBuilder<TElement>`

The `MapOfElementBuilder` should have the same surface area as `ArrayOfElementBuilder`. The key difference is that `MapOf` deals with a dictionary (key-value pairs) rather than a list, and the `TKey` must be resolvable from a property of `TElement`.

## Test cases

All tests should follow TDD and use the same patterns and conventions as the existing `ArrayOf` tests.

### 1. Basic MapOf registration

Configure a `MapOf<TEntity, TKey, TElement>` and verify that the metadata is correctly registered on the entity type (property name, key type, element type).

### 2. MapOf with element property configuration

Configure a `MapOf` using the `Action<MapOfElementBuilder<TElement>>` overload. Verify that scalar properties of `TElement` are correctly registered.

### 3. MapOf with nested ArrayOf

Configure a `MapOf` where `TElement` contains an `ArrayOf`. This is the original use case:

```csharp
builder.MapOf<Restaurant, DayOfWeek, DaySchedule>(
    r => r.WeeklyHours,
    day =>
    {
        day.Property(d => d.IsClosed);
        day.ArrayOf<DaySchedule, TimeSlot>(
            d => d.TimeSlots,
            ts =>
            {
                ts.Property(t => t.Open);
                ts.Property(t => t.Close);
            });
    });
```

Verify the nested array metadata is correctly associated with the element.

### 4. MapOf with nested Reference

Configure a `MapOf` where `TElement` contains a `Reference` property. Verify the reference metadata is correctly registered within the element configuration.

### 5. MapOf with nested MapOf

Configure a `MapOf` where `TElement` contains another `MapOf` (Map of Maps). Verify the nested map metadata is correctly registered.

### 6. Key resolution

Verify that the `TKey` type is correctly associated with the map metadata, so the provider knows how to serialize/deserialize dictionary keys (e.g., `DayOfWeek` enum to string).

### 7. Full composition

Configure a `MapOf` where `TElement` uses `Property`, `ArrayOf`, `Reference`, and `MapOf` simultaneously. Verify all metadata is correctly registered.

## Implementation notes

- The namespace should be `Fudie.Firestore.EntityFrameworkCore.Metadata.Builders`, same as `ArrayOf`.
- Follow the exact same code structure, naming conventions, and patterns as the `ArrayOf` implementation.
- Do not implement serialization/deserialization at this stage — only the builder and metadata registration.
- Start with tests, then implement to make them pass.
