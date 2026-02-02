# Bug 006: DateTime Returned as UTC Instead of Local Time

## Resumen

Al guardar un `DateTime` en Firestore y luego leerlo, el valor se devolvía en UTC en lugar de mantener la hora local original. Esto causaba que los valores leídos no coincidieran con los valores guardados.

## Error Observado

```csharp
// Guardar
var menu = new Menu { EffectiveFrom = new DateTime(2025, 6, 1) };  // Local: 2025-06-01 00:00:00
await context.SaveChangesAsync();

// Leer
var loaded = await context.Menus.FirstAsync(m => m.Id == menu.Id);
// loaded.EffectiveFrom = 2025-05-31 22:00:00 (UTC)  // INCORRECTO
// Esperado: 2025-06-01 00:00:00 (Local)
```

## Causa

El `FirestoreValueConverter` convertía correctamente `DateTime` a UTC al guardar:

```csharp
// ToFirestore - CORRECTO
if (value is DateTime dt)
    return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
```

Pero al leer, devolvía el valor en UTC sin convertir a hora local:

```csharp
// FromFirestore - INCORRECTO
if (value is Timestamp timestamp && actualTargetType == typeof(DateTime))
    return timestamp.ToDateTime();  // Devuelve UTC
```

## Solución

Convertir el `Timestamp` a hora local al leer:

```csharp
// FromFirestore - CORRECTO
if (value is Timestamp timestamp && actualTargetType == typeof(DateTime))
    return timestamp.ToDateTime().ToLocalTime();
```

## Archivos Modificados

1. **FirestoreValueConverter.cs** (línea 116-117)
   ```csharp
   // Timestamp → DateTime (Firestore SDK specific type)
   // ToDateTime() returns UTC, we convert to local time to match user expectations
   if (value is Timestamp timestamp && actualTargetType == typeof(DateTime))
       return timestamp.ToDateTime().ToLocalTime();
   ```

## Tests Actualizados

Los tests que usaban `DateTimeKind.Utc` explícitamente fueron actualizados para usar `DateTime` sin especificar Kind (comportamiento normal de usuario):

- `PrimitiveArraySerializationTests.cs`
- `PrimitiveArrayChangeTrackingTests.cs`
- `NestedArraySerializationTests.cs`
- `SelectReferenceTests.cs`

### Ejemplo de cambio en tests

```csharp
// ANTES (comportamiento atípico)
var dates = new[]
{
    new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
    new DateTime(2024, 6, 20, 14, 0, 0, DateTimeKind.Utc)
};

// DESPUÉS (comportamiento normal de usuario)
var dates = new[]
{
    new DateTime(2024, 1, 15, 10, 30, 0),
    new DateTime(2024, 6, 20, 14, 0, 0)
};
```

## Comportamiento Final

| Operación | Entrada | Almacenado en Firestore | Leído |
|-----------|---------|------------------------|-------|
| Guardar Local | `2025-06-01 10:00` (Local) | `2025-06-01 08:00` (UTC) | `2025-06-01 10:00` (Local) |
| Guardar Unspecified | `2025-06-01 10:00` (Unspecified) | `2025-06-01 08:00` (UTC) | `2025-06-01 10:00` (Local) |

**Nota**: Firestore `Timestamp` no preserva el `DateTimeKind` original. Todos los valores se almacenan como UTC y se devuelven como hora local.

---

**Fecha**: 2026-02-02
**Proyecto afectado**: Customer (webapi)
**Provider**: Fudie.Firestore.EntityFrameworkCore
**Estado**: RESUELTO
