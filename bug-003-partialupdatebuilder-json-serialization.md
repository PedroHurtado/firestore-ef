# Bug 003: PartialUpdateBuilder usa JSON para persistir en lugar de objetos CLR

## Resumen

El `PartialUpdateBuilder` usa los valores parseados del JSON para construir los datos que se envían a Firestore, en lugar de usar los objetos CLR originales. Esto causa pérdida de información de tipos y conversiones incorrectas.

## Archivos afectados

- `src/Fudie.Firestore.EntityFrameworkCore/Update/PartialUpdateBuilder.cs`
- `src/Fudie.Firestore.EntityFrameworkCore/Storage/FirestoreValueConverter.cs` (fix temporal)

## Causa raíz

El flujo actual de `ComputeArrayDiff` es:

```
Array CLR → JSON (para diff) → JsonElement → JsonElementToDict → Firestore
                                    ↑
                            perdemos tipo CLR
```

El problema específico está en `JsonElementToValue`:

```csharp
JsonValueKind.Number when element.TryGetInt64(out var l) => l,  // ← prioriza Int64
JsonValueKind.Number when element.TryGetDouble(out var d) => d,
```

Cuando el JSON tiene un número como `0`, `TryGetInt64` tiene éxito y devuelve `long 0`, nunca llega a `TryGetDouble`. Este `long` va directo a Firestore sin pasar por `_valueConverter.ToFirestore()`.

## Síntomas

1. Un `decimal 0m` en el modelo CLR se guarda como `Int64` en Firestore en lugar de `Double`
2. Al leer, Firestore devuelve `Int64` y el `Materializer` falla al convertir a `decimal?`
3. Error: `Object of type 'System.Int64' cannot be converted to type 'System.Nullable'1[System.Decimal]'`
4. **El error no llega al cliente** - el cliente recibe `PriceOption` como `null` en lugar de la excepción

## Bug secundario: Try-catch mudo

El hecho de que el cliente reciba `null` en lugar de ver la excepción indica que hay un **try-catch mudo** en algún lugar del pipeline de materialización que está tragándose la excepción y devolviendo `null` silenciosamente.

Esto es problemático porque:
- Oculta errores de deserialización
- El cliente no sabe que hubo un problema
- Dificulta el debugging

**TODO**: Localizar y revisar el try-catch que está silenciando esta excepción.

## Ejemplo del problema

```csharp
// Modelo
public record PriceOption
{
    public decimal? Price { get; }  // ← decimal
}

// Test que falla
var request = CreateValidRequest(price: 0m);  // precio = 0
await Client.PutAsJsonAsync($"/menu-items/{id}/price-options/{type}", request);

// Después del update, al leer:
// Price: 0 (Int64, Scalar)  ← Debería ser Double
// Materializer falla al convertir Int64 → decimal?
```

## Fix temporal aplicado

En `FirestoreValueConverter.FromFirestore` se añadió conversión `long → decimal`:

```csharp
// long → decimal (Firestore returns 0 as Int64, not Double)
if (value is long ld && actualTargetType == typeof(decimal))
    return (decimal)ld;
```

Este fix permite que la lectura funcione, pero **no soluciona el problema de escritura**.

## Solución correcta requerida

El flujo correcto debería ser:

```
Array CLR → JSON (solo para calcular diff)
Array CLR → _valueConverter.ToFirestore() → Dict → Firestore
```

1. **El JSON solo debe usarse para detectar cambios** (comparar original vs actual)
2. **Los datos a persistir deben venir del objeto CLR original**, no del JSON parseado
3. **Todos los valores deben pasar por `_valueConverter.ToFirestore()`** antes de enviarse a Firestore

### Cambios necesarios en `ComputeArrayDiff`:

1. Usar el JSON solo para identificar **qué elementos** cambiaron (índices o identificadores)
2. Para elementos añadidos: buscarlos en `currentArray` (CLR) y serializarlos con el converter
3. Para elementos eliminados: usar el JSON original está bien (ya no existen en CLR)

### Métodos a modificar:

- `ComputeArrayDiff` - Cambiar para usar objetos CLR para elementos añadidos
- `ConvertJsonElementsToFirestore` - Posiblemente eliminar o refactorizar
- `JsonElementToDict` / `JsonElementToValue` - Posiblemente eliminar

## Impacto

- **Lectura**: Funciona con el fix temporal (long → decimal)
- **Escritura**: Los datos se guardan con tipos incorrectos (Int64 en lugar de Double)
- **Consistencia**: Los datos en Firestore pueden tener tipos inconsistentes dependiendo de si fueron creados con Add o Update

## Tests de regresión

Crear tests que verifiquen:
1. Update con `decimal 0m` guarda como Double en Firestore
2. Update con `decimal 0.5m` guarda como Double en Firestore
3. Lectura después de Update con precio 0 funciona correctamente

## Relación con otros bugs

- **Bug 001**: Afecta detección de cambios en ArrayOfChangeTracker (RESUELTO)
- **Bug 002**: Afecta serialización de propiedades ignoradas en Update (RESUELTO)
- **Bug 003**: Afecta tipos de datos en la persistencia de arrays (ESTE BUG - FIX TEMPORAL)
