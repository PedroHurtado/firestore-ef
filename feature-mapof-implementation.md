# Feature: MapOf Implementation Roadmap

## Resumen

Implementar soporte para `IReadOnlyDictionary<TKey, TElement>` como Maps nativos de Firestore. Esta feature permite persistir diccionarios donde cada clave se convierte en un campo del Map y cada valor en un objeto anidado.

## Alcance de esta fase

> **IMPORTANTE**: En esta fase NO se toca el Materializer. Solo implementamos escritura.
> La materialización (lectura de Maps a objetos CLR) se implementará en una fase posterior.

### Flujo de esta fase
```
DbContext (Write) → Firestore Document
                          ↓
              Firestore SDK (Read) ← Tests de integración
```

---

## Archivos a crear (NUEVOS)

### 1. Metadata/Conventions

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfAnnotations.cs` | Constantes y métodos de extensión para anotaciones de MapOf | ✅ Creado |

### 2. Metadata/Builders

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfBuilder.cs` | Builder principal que registra tipos de clave y elemento | ✅ Creado |
| `MapOfElementBuilder.cs` | Configura elementos con Property, Reference, ArrayOf, MapOf, Ignore | ✅ Creado |
| `MapOfEntityTypeBuilderExtensions.cs` | Métodos de extensión `MapOf()` en EntityTypeBuilder | ✅ Creado |

### 3. Conventions (NUEVO)

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfConvention.cs` | Detecta propiedades `IReadOnlyDictionary<,>` y crea shadow properties para tracking | ✅ Creado |

### 4. ChangeTracking (NUEVO)

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfChangeTracker.cs` | Detecta cambios en Maps comparando JSON serializado | ⏳ Pendiente |

### 5. Tests Unitarios (NUEVOS)

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfAnnotationsTests.cs` | Tests para constantes y métodos de extensión | ✅ Creado |
| `MapOfBuilderTests.cs` | Tests para registro de tipos | ✅ Creado |
| `MapOfElementBuilderTests.cs` | Tests para Property, Reference, ArrayOf, MapOf, Ignore | ✅ Creado |
| `MapOfEntityTypeBuilderExtensionsTests.cs` | Tests para métodos de extensión | ✅ Creado |
| `MapOfConventionTests.cs` | Tests para detección automática y shadow properties | ✅ Creado (11 tests) |
| `MapOfChangeTrackerTests.cs` | Tests para detección de cambios | ⏳ Pendiente |

### 6. Tests de Integración (NUEVOS)

| Archivo | Propósito | Estado |
|---------|-----------|--------|
| `MapOfSerializationTests.cs` | Escribir con DbContext, leer con SDK de Firestore | ⏳ Pendiente |
| `MapOfChangeTrackingTests.cs` | Verificar detección de cambios (add/update/delete keys) | ⏳ Pendiente |

---

## Archivos a modificar (EXISTENTES)

### 1. Storage

| Archivo | Modificación |
|---------|--------------|
| `FirestoreDatabase.cs` | Añadir `SerializeMapOfProperties()` similar a `SerializeArrayOfProperties()` |

### 2. Update

| Archivo | Modificación |
|---------|--------------|
| `PartialUpdateBuilder.cs` | Añadir `ProcessMapOfProperties()` con lógica de diff para Maps |
| `PartialUpdateResult.cs` | (opcional) Añadir campos para operaciones de Map si es necesario |

### 3. ChangeTracking

| Archivo | Modificación |
|---------|--------------|
| `ArrayOfSaveChangesInterceptor.cs` | Renombrar a `FirestoreSaveChangesInterceptor` y añadir `MapOfChangeTracker.SyncMapOfChanges()` |

### 4. Query/Pipeline/Handlers

| Archivo | Modificación |
|---------|--------------|
| `TrackingHandler.cs` | Añadir inicialización de shadow properties para MapOf |

### 5. Conventions

| Archivo | Modificación |
|---------|--------------|
| `FirestoreConventionSetBuilder.cs` | Registrar `MapOfConvention` |

---

## Fases de implementación

### Fase 1: Builders y Metadata ✅ COMPLETADA

**Objetivo**: API fluent para configurar MapOf en `OnModelCreating`

```csharp
entity.MapOf(e => e.WeeklyHours, day =>
{
    day.Property(d => d.IsClosed);
    day.ArrayOf(d => d.TimeSlots, ts =>
    {
        ts.Property(t => t.Open);
        ts.Property(t => t.Close);
    });
});
```

**Archivos creados**:
- `MapOfAnnotations.cs`
- `MapOfBuilder.cs`
- `MapOfElementBuilder.cs`
- `MapOfEntityTypeBuilderExtensions.cs`

**Tests unitarios**: 54 tests pasando

---

### Fase 2: Convention y Shadow Properties ✅ COMPLETADA

**Objetivo**: Detectar automáticamente propiedades `IReadOnlyDictionary<,>` y crear shadow properties para change tracking.

**Archivos creados**:
- `MapOfConvention.cs` - Implementa `IEntityTypeAddedConvention` y `IModelFinalizingConvention`
- `MapOfConventionTests.cs` - 11 tests unitarios

**Archivos modificados**:
- `FirestoreConventionSetBuilder.cs` - Registrado `MapOfConvention`
- `ConventionHelpers.cs` - Añadidos helpers para diccionarios: `IsGenericDictionary()`, `GetDictionaryKeyType()`, `GetDictionaryValueType()`, `FindDictionaryBackingFields()`

**Tipos de clave soportados**:
- Primitivos (int, long, etc.)
- String
- Enum
- Guid
- DateTime, DateTimeOffset

**Comportamiento implementado**:
```csharp
// Detecta automáticamente:
public IReadOnlyDictionary<DayOfWeek, DaySchedule> WeeklyHours { get; set; }

// CRÍTICO: Ignora la propiedad para que EF Core no la procese como navegación
entityTypeBuilder.Ignore("WeeklyHours");

// Crea shadow property:
// __{PropertyName}_Json → "__WeeklyHours_Json"

// Registra anotaciones:
// Firestore:MapOf:KeyClrType:WeeklyHours → typeof(DayOfWeek)
// Firestore:MapOf:ElementClrType:WeeklyHours → typeof(DaySchedule)
```

---

### Fase 3: Serialización ✅ COMPLETADA

**Objetivo**: Serializar Maps a Firestore en operaciones de INSERT.

**Archivos modificados**:
- `FirestoreDatabase.cs` → Añadido `SerializeMapOfProperties()` y métodos helper

**Métodos implementados**:
- `SerializeMapOfProperties()` - Detecta propiedades MapOf y las serializa
- `SerializeMapOfValue()` - Serializa IReadOnlyDictionary usando reflection
- `SerializeMapOfDictionary()` - Serializa IDictionary directamente
- `ConvertMapKeyToString()` - Convierte claves a string (enum→ToString(), int→ToString())
- `SerializeMapOfElement()` - Serializa valores como ComplexType, Reference o primitivo

**Lógica de serialización**:
```csharp
// Entrada: IReadOnlyDictionary<DayOfWeek, DaySchedule>
// Salida: Dictionary<string, object> para Firestore

// DayOfWeek.Monday → "Monday" (enum.ToString())
// int 1 → "1" (primitivo.ToString())
// string "key" → "key" (directo)
```

**Estructura Firestore resultante**:
```
weeklyHours: {
  "Monday": { isClosed: false, timeSlots: [...] },
  "Tuesday": { isClosed: true, timeSlots: [] }
}
```

---

### Fase 4: Change Tracking ⏳ PENDIENTE

**Objetivo**: Detectar cambios en Maps (añadir/modificar/eliminar claves).

**Archivos a crear**:
- `MapOfChangeTracker.cs`

**Archivos a modificar**:
- `ArrayOfSaveChangesInterceptor.cs` → Renombrar y añadir `SyncMapOfChanges()`
- `TrackingHandler.cs` → Inicializar shadow properties de MapOf

**Tests unitarios a crear**:
- `MapOfChangeTrackerTests.cs`

**Lógica de detección**:
```csharp
// Comparar originalJson vs currentJson
// Para cada clave:
//   - En original pero no en current → eliminar
//   - En current pero no en original → añadir
//   - Valor cambió → actualizar
```

---

### Fase 5: Partial Updates ⏳ PENDIENTE

**Objetivo**: Actualizar solo las claves modificadas usando dot notation.

**Archivos a modificar**:
- `PartialUpdateBuilder.cs` → Añadir `ProcessMapOfProperties()`

**Operaciones Firestore**:
```csharp
// Eliminar clave
updates["weeklyHours.Monday"] = FieldValue.Delete;

// Añadir/Actualizar clave
updates["weeklyHours.Tuesday"] = new Dictionary<string, object> { ... };
```

---

### Fase 6: Tests de Integración ⏳ PENDIENTE

**Objetivo**: Verificar escritura end-to-end.

**Archivos a crear**:
- `tests/Fudie.Firestore.IntegrationTest/MapOf/MapOfSerializationTests.cs`
- `tests/Fudie.Firestore.IntegrationTest/MapOf/MapOfChangeTrackingTests.cs`

**Patrón de test**:
```csharp
[Fact]
public async Task MapOf_Insert_ShouldSerializeToFirestoreMap()
{
    // Arrange: Crear entidad con diccionario
    var restaurant = new Restaurant
    {
        Id = "rest-001",
        WeeklyHours = new Dictionary<DayOfWeek, DaySchedule>
        {
            [DayOfWeek.Monday] = new DaySchedule { IsClosed = false, ... }
        }
    };

    // Act: Guardar con DbContext
    await _context.Restaurants.AddAsync(restaurant);
    await _context.SaveChangesAsync();

    // Assert: Leer con SDK de Firestore y verificar estructura
    var doc = await _firestoreDb.Collection("restaurants").Document("rest-001").GetSnapshotAsync();
    var weeklyHours = doc.GetValue<Dictionary<string, object>>("weeklyHours");

    weeklyHours.Should().ContainKey("Monday");
    var monday = weeklyHours["Monday"] as Dictionary<string, object>;
    monday["isClosed"].Should().Be(false);
}
```

---

## Conversión de claves

| Tipo TKey | Serialización | Ejemplo |
|-----------|---------------|---------|
| `enum` | `ToString()` | `DayOfWeek.Monday` → `"Monday"` |
| `string` | directo | `"breakfast"` → `"breakfast"` |
| `int` | `ToString()` | `1` → `"1"` |
| `long` | `ToString()` | `100L` → `"100"` |
| `Guid` | `ToString()` | `Guid.NewGuid()` → `"a1b2c3..."` |

> **Nota**: En Firestore, las claves de un Map son siempre strings.

---

## Fase futura: Materialización (NO en este alcance)

> Esta fase se implementará DESPUÉS de completar las fases 1-6.

**Objetivo**: Leer Maps de Firestore y materializar a `IReadOnlyDictionary<TKey, TElement>`.

### Archivos de referencia para entender el flujo de lectura

El proceso de lectura involucra varios componentes que trabajan juntos:

#### 1. Query Compilation (`VisitShapedQuery`)
```
FirestoreShapedQueryCompilingExpressionVisitor.cs:32-40
```
- Llama a `AddComplexTypeIncludes()` y `AddArrayOfIncludes()` para procesar includes
- Los includes se almacenan en `FirestoreQueryCompilationContext`

#### 2. Include Translators
- `ComplexTypeIncludeTranslator.cs` - Procesa includes dentro de ComplexTypes
  - Patrón: `e => e.DireccionPrincipal.SucursalCercana`
  - Genera `IncludeInfo` con path completo

- `ArrayOfIncludeTranslator.cs` - Procesa includes de ArrayOf
  - Caso 1: ArrayOf Reference `e => e.Proveedores`
  - Caso 2: ArrayOf Embedded `e => e.Secciones` (datos vienen con padre)
  - Caso 3: Cadenas de ThenInclude en Embedded

#### 3. TrackingHandler
- Inicializa shadow properties cuando se trackean entidades desde queries
- Llama a `ArrayOfChangeTracker.InitializeShadowProperties()`

### Archivos a crear/modificar (futura fase)

| Archivo | Propósito |
|---------|-----------|
| `FirestoreValueConverter.cs` | Añadir conversión de `IDictionary<string, object>` → `IReadOnlyDictionary<TKey, TElement>` |
| `MapOfIncludeTranslator.cs` (NUEVO) | Procesar includes de referencias dentro de valores de MapOf |
| `FirestoreQueryCompilationContext.cs` | Añadir `_mapOfIncludes` y `AddMapOfInclude()` |
| `FirestoreQueryExpression_Include.cs` | Añadir `AddMapOfIncludes()` |
| `FirestoreShapedQueryCompilingExpressionVisitor.cs` | Llamar a `AddMapOfIncludes()` en `VisitShapedQuery` |

### Flujo futuro completo
```
LINQ Query
    ↓
MapOfIncludeTranslator (detecta includes en valores de Map)
    ↓
FirestoreQueryCompilationContext (almacena MapOfIncludes)
    ↓
VisitShapedQuery (añade includes al AST)
    ↓
Pipeline Execution
    ↓
FirestoreValueConverter.FromFirestore() (materializa Map a Dictionary)
    ↓
TrackingHandler (inicializa shadow properties)
    ↓
IReadOnlyDictionary<TKey, TElement>
```

---

## Checklist de progreso

- [x] Fase 1: Builders y Metadata
- [x] Fase 2: Convention y Shadow Properties
- [x] Fase 3: Serialización
- [ ] Fase 4: Change Tracking
- [ ] Fase 5: Partial Updates
- [ ] Fase 6: Tests de Integración
- [ ] Fase futura: Materialización (fuera de alcance actual)

---

## Referencias

### Documentos
- Especificación original: `MapOf-Feature-Spec.md`

### Implementación de referencia (ArrayOf)
- Builders: `Metadata/Builders/ArrayOf*.cs`
- Conventions: `Metadata/Conventions/ArrayOfConvention.cs`, `ArrayOfAnnotations.cs`
- Change tracking: `ChangeTracking/ArrayOfChangeTracker.cs`
- Interceptor: `ChangeTracking/ArrayOfSaveChangesInterceptor.cs`
- Partial updates: `Update/PartialUpdateBuilder.cs`

### Serialización
- `Storage/FirestoreDatabase.cs` → `SerializeArrayOfProperties()` (líneas 724-818)
- `Storage/FirestoreValueConverter.cs` → Conversión de tipos

### Query/Materialización (referencia para fase futura)
- `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` → `VisitShapedQuery()`
- `Query/Translators/ArrayOfIncludeTranslator.cs` → Includes de ArrayOf
- `Query/Translators/ComplexTypeIncludeTranslator.cs` → Includes de ComplexTypes
- `Query/FirestoreQueryCompilationContext.cs` → Storage de includes
- `Query/Pipeline/Handlers/TrackingHandler.cs` → Inicialización de shadow properties
