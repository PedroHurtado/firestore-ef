# Plan: Implementar Entity Tracking en Firestore Provider

**Fecha:** 2025-12-13

## Problema Identificado

Cuando se ejecuta una query (ej: `FindAsync`, `Where().ToListAsync()`), las entidades se devuelven en estado `Detached` en lugar de `Unchanged`. Esto causa que:

1. El ChangeTracker no detecte modificaciones
2. `SaveChangesAsync` no persista los cambios
3. El test `Update_ExistingEntity_ShouldPersistChanges` falle

### Flujo Esperado vs Actual

**Flujo Esperado (SQL Server, Cosmos):**
```
Query → Materializar entidad → Verificar ChangeTracker → Adjuntar como Unchanged → Retornar
```

**Flujo Actual (Firestore):**
```
Query → Materializar entidad → Retornar (sin adjuntar) → Estado = Detached
```

## Ubicación del Problema

El método `DeserializeEntity<T>` en `FirestoreShapedQueryCompilingExpressionVisitor.cs` (líneas 88-119) materializa la entidad pero nunca la adjunta al ChangeTracker.

```csharp
private static T DeserializeEntity<T>(...) where T : class, new()
{
    // ... deserialización ...
    var entity = deserializer.DeserializeEntity<T>(documentSnapshot);
    // ... carga de includes ...
    return entity;  // ← Retorna sin adjuntar al ChangeTracker
}
```

## Solución Propuesta

### Estrategia: Tracking Manual con Identity Resolution

Implementar tracking después de la materialización, respetando:
1. `QueryTrackingBehavior` (NoTracking, TrackAll, NoTrackingWithIdentityResolution)
2. Identity Resolution (si la entidad ya está trackeada, retornar esa instancia)
3. Snapshot de valores originales (para detectar cambios)

### Archivos a Modificar

1. **`FirestoreShapedQueryCompilingExpressionVisitor.cs`**
   - Modificar `DeserializeEntity<T>` para adjuntar entidades al ChangeTracker

2. **Nuevo: `FirestoreEntityTracker.cs`** (opcional, para encapsular lógica)
   - Clase helper para manejar el tracking

## Plan de Implementación

### Paso 1: Entender QueryTrackingBehavior

Verificar cómo acceder al `QueryTrackingBehavior` desde el shaper:
- `QueryCompilationContext.QueryTrackingBehavior`
- Debe pasarse al `FirestoreQueryingEnumerable`

### Paso 2: Modificar FirestoreQueryingEnumerable

Pasar información de tracking al enumerable:
```csharp
public class FirestoreQueryingEnumerable<T>
{
    private readonly bool _isTracking;
    // ...
}
```

### Paso 3: Implementar Tracking en DeserializeEntity

```csharp
private static T DeserializeEntity<T>(
    QueryContext queryContext,
    DocumentSnapshot documentSnapshot,
    FirestoreQueryExpression queryExpression,
    bool isTracking) where T : class, new()
{
    var dbContext = queryContext.Context;

    // 1. Obtener metadata de la entidad y su PK
    var entityType = dbContext.Model.FindEntityType(typeof(T));
    var key = entityType.FindPrimaryKey();
    var keyProperty = key?.Properties.FirstOrDefault();

    // 2. Convertir ID al tipo correcto (Firestore siempre es string, pero PK puede ser int, Guid, etc.)
    var convertedKey = ConvertKeyValue(documentSnapshot.Id, keyProperty);
    var keyValues = new object[] { convertedKey };

    // 3. Identity Resolution con IStateManager - O(1) lookup
    if (isTracking)
    {
        var stateManager = dbContext.GetService<IStateManager>();
        var existingEntry = stateManager.TryGetEntry(key, keyValues);

        if (existingEntry != null)
        {
            return (T)existingEntry.Entity; // Retornar instancia existente
        }
    }

    // 4. Deserializar
    var entity = deserializer.DeserializeEntity<T>(documentSnapshot);

    // 5. Cargar includes
    if (queryExpression.PendingIncludes.Count > 0)
    {
        LoadIncludes(...);
    }

    // 6. Adjuntar al ChangeTracker como Unchanged usando Attach()
    if (isTracking)
    {
        dbContext.Attach(entity);
    }

    return entity;
}

// Helper para conversión de tipo de ID
private static object ConvertKeyValue(string firestoreId, IProperty keyProperty)
{
    var targetType = keyProperty.ClrType;

    // Usar ValueConverter si está configurado
    var converter = keyProperty.GetValueConverter();
    if (converter != null)
    {
        return converter.ConvertFromProvider(firestoreId);
    }

    // Conversión estándar
    if (targetType == typeof(string)) return firestoreId;
    if (targetType == typeof(int)) return int.Parse(firestoreId);
    if (targetType == typeof(long)) return long.Parse(firestoreId);
    if (targetType == typeof(Guid)) return Guid.Parse(firestoreId);

    return Convert.ChangeType(firestoreId, targetType);
}
```

### Paso 4: Propagar isTracking desde QueryCompilationContext

En `VisitShapedQuery`:
```csharp
var isTracking = QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;

var newExpression = Expression.New(
    constructor,
    QueryCompilationContext.QueryContextParameter,
    Expression.Constant(firestoreQueryExpression),
    Expression.Constant(shaperLambda.Compile()),
    Expression.Constant(entityType),
    Expression.Constant(isTracking));  // ← Nuevo parámetro
```

### Paso 5: Actualizar FirestoreQueryingEnumerable

```csharp
public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly bool _isTracking;

    public FirestoreQueryingEnumerable(
        QueryContext queryContext,
        FirestoreQueryExpression queryExpression,
        Func<QueryContext, DocumentSnapshot, bool, T> shaper, // ← Modificar firma
        Type contextType,
        bool isTracking) // ← Nuevo parámetro
    {
        _isTracking = isTracking;
        // ...
    }
}
```

## Consideraciones Adicionales

### Tracking de Entidades Incluidas (Includes)

Las entidades cargadas via `.Include()` también deben ser trackeadas:
- Subcollections
- Referencias

Modificar `LoadSubCollectionAsync` y `LoadReferenceAsync` para también adjuntar entidades.

### Valores Originales (OriginalValues)

Cuando se adjunta una entidad como `Unchanged`, EF Core automáticamente crea un snapshot de los valores originales. Esto permite detectar cambios cuando se modifica una propiedad.

### NoTrackingWithIdentityResolution

Este modo especial requiere identity resolution pero sin tracking. Considerar implementarlo si es necesario.

## Tests a Crear/Modificar

1. **Test existente**: `Update_ExistingEntity_ShouldPersistChanges` - Debe pasar
2. **Nuevo test**: `Query_SameEntity_ShouldReturnSameInstance` - Identity resolution
3. **Nuevo test**: `AsNoTracking_ShouldNotTrackEntities` - Verificar NoTracking

## Riesgos y Mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| Performance por ChangeTracker lookup | Usar `IStateManager.TryGetEntry()` - O(1) |
| Conflictos de tracking con includes | Trackear en orden: padre primero, hijos después |
| Memory leaks si no se dispone el contexto | No es responsabilidad del provider |
| Tipo de ID incompatible | Usar `ConvertKeyValue` con soporte para ValueConverters |

## Notas de Referencia

- **Cosmos DB Provider**: Revisar `CosmosShapedQueryCompilingExpressionVisitor` para patrones de materialización
- **IEntityMaterializerSource**: Considerar usar para casos más complejos en el futuro
- **IStateManager**: API interna de EF Core, pero estable y usada por otros providers

## Orden de Implementación (Incremental)

### Fase 1: Tracking Básico (MVP) ✅ COMPLETADA
1. [x] Verificar que `QueryContext` está disponible en `DeserializeEntity` ✓ (ya lo está)
2. [x] Añadir tracking básico con `Attach()` directamente (sin pasar isTracking por ahora)
3. [x] **Ejecutar test `Update_ExistingEntity` y verificar que pasa** ✅
**Commit:** `102d038`

### Fase 2: Respetar QueryTrackingBehavior ✅ COMPLETADA
4. [x] Modificar `FirestoreQueryingEnumerable` para aceptar `isTracking`
5. [x] Modificar `VisitShapedQuery` para pasar `isTracking` desde `QueryCompilationContext`
6. [x] Condicionar el `Attach()` a `isTracking == true`
7. [x] **Ejecutar tests y verificar** ✅
**Commit:** `262463b`

### Fase 3: Identity Resolution ✅ COMPLETADA
8. [x] Añadir conversión de tipo de ID (`ConvertKeyValue`)
9. [x] Implementar identity resolution con `IStateManager.TryGetEntry()`
10. [x] **Ejecutar tests y verificar** ✅
**Commit:** `2c4c6c6`

### Fase 4: Tracking de Includes ✅ COMPLETADA
11. [x] Actualizar `LoadSubCollectionAsync` para trackear entidades
12. [x] Actualizar `LoadReferenceAsync` para trackear entidades
13. [x] **Ejecutar tests y verificar** ✅
**Commit:** `dbd7997`

### Fase 5: Tests Adicionales ✅ COMPLETADA
14. [x] Agregar test `Query_SameEntity_ShouldReturnSameInstance`
15. [x] Agregar test `AsNoTracking_ShouldNotTrackEntities`
**Commit:** `006f944`

## Comandos de Verificación

```bash
# Ejecutar solo el test de Update
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "Update_ExistingEntity"

# Ejecutar todos los tests CRUD
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "DbContextCrudTests"

# Ejecutar todos los tests
dotnet test
```
