# Extensibilidad de MapOf y ArrayOf en EF Core Builders

## Situacion Actual

### Lo que funciona

| Builder | ArrayOf | MapOf |
|---------|---------|-------|
| `EntityTypeBuilder<T>` | Si | Si |
| `ArrayOfElementBuilder<T>` | Si | No |
| `ComplexPropertyBuilder<T>` | No | No |

### Casos de anidacion soportados

```
Entity
├── ArrayOf<T>           Si
│   └── ArrayOf<T>       Si  (arrays dentro de arrays)
│   └── MapOf<K,V>       No  (maps dentro de arrays)
├── MapOf<K,V>           Si
│   └── ArrayOf<T>       Si  (arrays dentro de maps)
│   └── MapOf<K,V>       ?   (maps dentro de maps - no probado)
└── ComplexProperty<T>   Si
    └── ArrayOf<T>       No  (PENDIENTE)
    └── MapOf<K,V>       No  (PENDIENTE - TODO linea 63-64)
```

### Casos NO soportados en el provider

```
ArrayOf<T>
└── MapOf<K,V>           No  (maps dentro de arrays)

ComplexProperty<T>
├── ArrayOf<T>           No  (arrays dentro de complex properties)
└── MapOf<K,V>           No  (maps dentro de complex properties)
```

---

## Analisis de ComplexPropertyBuilder (EF Core)

### Fuente analizada
`https://github.com/dotnet/dotnet/.../ComplexPropertyBuilder.cs`

### Caracteristicas de extensibilidad

| Caracteristica | Valor | Implicacion |
|----------------|-------|-------------|
| `sealed` | No | Se puede heredar |
| Metodos `virtual` | Casi todos | Se pueden sobrescribir |
| Propiedades `protected` | `PropertyBuilder`, `TypeBuilder` | Acceso a builders internos |
| `IInfrastructure<T>` | Si | Patron estandar de extension EF Core |

### Interfaces implementadas

```csharp
public class ComplexPropertyBuilder :
    IInfrastructure<IConventionComplexPropertyBuilder>,
    IInfrastructure<IConventionComplexTypeBuilder>
```

### Acceso a internals

```csharp
// Desde el constructor
PropertyBuilder = ((ComplexProperty)complexProperty).Builder;
TypeBuilder = ((ComplexProperty)complexProperty).ComplexType.Builder;
```

---

## Estrategia de Implementacion

### Clase de extensiones existente

Ya existe `FirestorePropertyBuilderExtensions` con extensiones para `ComplexPropertyBuilder`:

```csharp
public static class FirestorePropertyBuilderExtensions
{
    public const string PersistNullValuesAnnotation = "Firestore:PersistNullValues";

    public static ComplexPropertyBuilder HasGeoPoint(this ComplexPropertyBuilder propertyBuilder);
    public static bool IsPersistNullValuesEnabled(this IProperty property);
    public static PropertyBuilder PersistNullValues(this PropertyBuilder propertyBuilder);
    public static PropertyBuilder<TProperty> PersistNullValues<TProperty>(this PropertyBuilder<TProperty> propertyBuilder);
    public static ComplexPropertyBuilder<TComplex> Reference<TComplex, TRelated>(this ComplexPropertyBuilder<TComplex> builder, Expression<Func<TComplex, TRelated?>> navigationExpression) where TRelated : class;
}
```

### Metodos a agregar

```csharp
public static class FirestorePropertyBuilderExtensions
{
    // ... existentes ...

    // Nuevo: ArrayOf dentro de ComplexProperty
    public static ComplexPropertyBuilder<TComplex> ArrayOf<TComplex, TElement>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, ICollection<TElement>>> propertyExpression,
        Action<ArrayOfElementBuilder<TElement>>? buildAction = null)
        where TComplex : class;

    // Nuevo: MapOf dentro de ComplexProperty
    public static ComplexPropertyBuilder<TComplex> MapOf<TComplex, TValue>(
        this ComplexPropertyBuilder<TComplex> builder,
        Expression<Func<TComplex, IDictionary<string, TValue>>> propertyExpression,
        Action<MapOfElementBuilder<TValue>>? buildAction = null)
        where TComplex : class;
}
```

### Uso final esperado

```csharp
entity.ComplexProperty(s => s.Policy, policy =>
{
    policy.Ignore(p => p.SlotIntervalMinutes);
    policy.Ignore(p => p.MaxAdvanceDays);

    policy.MapOf(p => p.StandardDurations);   // Nuevo
    policy.ArrayOf(p => p.SomeCollection);    // Nuevo
});
```

**Puntos clave:**
- Reutilizar la logica existente de serializacion JSON a shadow property
- El mecanismo de change tracking ya esta cubierto
- Solo falta "conectar" el builder con la infraestructura existente
- Seguir el mismo patron usado en `Reference<TComplex, TRelated>`

---

## TODOs en SchedulersDbContext

### TODO 1 - Linea 63-64 (PRIORIDAD)
```csharp
// TODO: MapOf not supported in ComplexPropertyBuilder
// policy.MapOf(p => p.StandardDurations);
```
**Entidad afectada:** `ServiceSchedule.Policy.StandardDurations`

### TODO 2 - Linea 74-78 (BAJO PRIORIDAD)
```csharp
// TODO: MapOf not supported in ArrayOfElementBuilder
// service.MapOf(srv => srv.WeeklySchedule, dayConfig => { ... });
```
**Entidad afectada:** `ServiceSchedule.Services[].WeeklySchedule`

---

## Conclusion

La extensibilidad de `ComplexPropertyBuilder` es viable. EF Core expone suficientes puntos de extension (`IInfrastructure`, metodos virtuales, propiedades protected) para implementar `ArrayOf` y `MapOf` como metodos de extension en `FirestorePropertyBuilderExtensions` sin hackear el framework.