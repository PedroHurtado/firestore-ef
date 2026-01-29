# BUG-004: EF Core Firestore Provider no traduce `object.Equals()` en genéricos

## Descripción
El provider de Firestore no puede traducir expresiones LINQ que usan `.Equals()` cuando el tipo es genérico y se produce boxing a `object`.

## Error
```
System.InvalidOperationException: The LINQ expression 'DbSet<Allergen>()
    .Where(a => a.Id.Equals((object)__id_0))' could not be translated.
```

## Código que falla
```csharp
// En IEntityLookup.GetRequiredAsync<T, TId>()
await query.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
```

## Código que funciona
```csharp
// Con tipo concreto (no genérico)
await query.FirstOrDefaultAsync(mi => mi.Id == id);
```

## Causa raíz
Con genéricos, el compilador resuelve `.Equals(id)` a `object.Equals(object)` (boxing), no a `TId.Equals(TId)`. El provider traduce `string.Equals(string)` o `Guid.Equals(Guid)`, pero no `object.Equals(object)`.

---

## PASO 1: Crear tests en el provider (OBLIGATORIO)

Antes de implementar la solución, crear dos tests que reproduzcan el error.

### Ubicación de los tests
```
tests/Fudie.Firestore.IntegrationTest/Query/
```

### Archivo de test
```
ObjectEqualsTranslationTests.cs
```

### Tests a crear

1. **Test con `object.Equals()` y tipo `string`**
   - Simular el escenario donde `TId` es `string` (como `Allergen.Id`)
   - Debe fallar actualmente con el error de traducción

2. **Test con `object.Equals()` y tipo `Guid`**
   - Simular el escenario donde `TId` es `Guid` (como `MenuItem.Id`)
   - Debe fallar actualmente con el error de traducción

---

## PASO 2: Implementar la traducción

Añadir traducción en el visitor de expresiones para `object.Equals()` cuando el argumento es un valor que puede resolverse en tiempo de ejecución.

## PASO 3: Verificar

1. Los tests del PASO 1 deben pasar

