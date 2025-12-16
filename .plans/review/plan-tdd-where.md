# Plan TDD: Where - EF Core Firestore Provider

**Fecha:** 2025-12-15

---

## Progreso

| Ciclo | Comportamiento | Estado | Commit | Tests |
|-------|----------------|--------|--------|-------|
| 1 | Igualdad (`==`) | ✅ | `f3be48a` | `WhereEqualTests` (6 tests) |
| 2 | Desigualdad (`!=`) | ✅ | `d922f76` | `WhereNotEqualTests` (5 tests) |
| 3 | Mayor que (`>`) | ✅ | `912ce33` | `WhereComparisonTests` (4 tests) |
| 4 | Mayor o igual (`>=`) | ✅ | `912ce33` | `WhereComparisonTests` (4 tests) |
| 5 | Menor que (`<`) | ✅ | `912ce33` | `WhereComparisonTests` (4 tests) |
| 6 | Menor o igual (`<=`) | ✅ | `912ce33` | `WhereComparisonTests` (4 tests) |
| 7 | AND (`&&`) | ✅ | `febd561` | `WhereLogicalTests` (3 tests) |
| 8 | OR (`\|\|`) | ✅ | `febd561` | `WhereLogicalTests` (2 tests) |
| 9 | Id + Filters (Multi-tenancy) | ✅ | `29a26f8` | `WhereLogicalTests` (2 tests) |
| 9.1 | AND + OR anidado (`A && (B \|\| C)`) | ✅ | `c82144d` | `WhereLogicalTests` (3 tests) |
| 9.2 | ComplexType.Property (propiedades anidadas) | ✅ | `f0c1d4c` | `ComplexTypeConventionTests` (2 tests) |

---

## Cotejo: Generator → Firestore

### Operadores de Comparación

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| (implícito) | `==` | WhereEqualTo | ✅ |
| NotEqual | `!=` | WhereNotEqualTo | ✅ |
| GreaterThan | `>` | WhereGreaterThan | ✅ |
| GreaterThanOrEqual | `>=` | WhereGreaterThanOrEqualTo | ✅ |
| LessThan | `<` | WhereLessThan | ✅ |
| LessThanOrEqual | `<=` | WhereLessThanOrEqualTo | ✅ |
| Between | `>= && <=` | Combinación | ✅ |

### Operadores IN

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| In | `list.Contains(x.Field)` | WhereIn | ✅ (máx 30) |
| NotIn | `!list.Contains(x.Field)` | WhereNotIn | ✅ (máx 10) |

### Operadores String

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| StartsWith | `.StartsWith()` | Workaround >= < | ⚠️ |
| EndsWith | `.EndsWith()` | No | ❌ |
| Contains | `.Contains()` | No | ❌ |
| Like | `EF.Functions.Like()` | No | ❌ |
| IgnoreCase | `.ToLower()` | No | ❌ |

### Operadores Null/Bool

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| IsNull | `== null` | WhereEqualTo(null) | ✅ |
| IsNotNull | `!= null` | WhereNotEqualTo(null) | ✅ |
| True | `== true` | WhereEqualTo(true) | ✅ |
| False | `== false` | WhereEqualTo(false) | ✅ |

### Conectores

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| And | `&&` | Implícito / Filter.And | ✅ |
| Or | `\|\|` | Filter.Or | ✅ |

### Ordenamiento

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| OrderBy | `.OrderBy()` | OrderBy | ✅ |
| OrderByDesc | `.OrderByDescending()` | OrderByDescending | ✅ |

### Terminadores (Prefijos)

| Generator | LINQ | Firestore | Soportado |
|-----------|------|-----------|-----------|
| FindBy | `.ToListAsync()` | GetSnapshotAsync | ✅ |
| FindFirstBy | `.FirstOrDefaultAsync()` | Limit(1) | ✅ |
| FindTopNBy | `.Take(N)` | Limit(N) | ✅ |
| CountBy | `.CountAsync()` | Count aggregation | ✅ |
| ExistsBy | `.AnyAsync()` | Limit(1) + check | ✅ |
| DeleteBy | `.ExecuteDeleteAsync()` | Batch delete | ⚠️ |

### Límites adicionales

| LINQ | Firestore | Soportado |
|------|-----------|-----------|
| `.TakeLast(N)` | LimitToLast(N) | ✅ |

### Agregaciones adicionales

| LINQ | Firestore | Soportado |
|------|-----------|-----------|
| `.Min()` | No nativo | ⚠️ Client-side |
| `.Max()` | No nativo | ⚠️ Client-side |

### Arrays (no en Generator, pero útil)

| LINQ | Firestore | Soportado |
|------|-----------|-----------|
| `array.Contains(value)` | WhereArrayContains | ✅ |
| `array.Any(x => list.Contains(x))` | WhereArrayContainsAny | ✅ |

---

## Paso 0: Diagnóstico

Antes de empezar, verificar qué funciona hoy:

| Operador | ¿Funciona? |
|----------|------------|
| `==` | |
| `!=` | |
| `>` | |
| `>=` | |
| `<` | |
| `<=` | |
| `&&` | |
| `\|\|` | |
| `list.Contains()` | |
| `== null` | |
| `== true` | |
| `.OrderBy()` | |
| `.OrderByDescending()` | |
| `.Take()` | |
| `.TakeLast()` | |
| `.Skip()` | |
| `.Count()` | |
| `.Any()` | |
| `.Sum()` | |
| `.Average()` | |
| `.Min()` | |
| `.Max()` | |
| `array.Contains()` | |

---

## Ciclos TDD

### Fase 1: Comparaciones

| Ciclo | Comportamiento |
|-------|----------------|
| 1 | Igualdad (`==`) |
| 2 | Desigualdad (`!=`) |
| 3 | Mayor que (`>`) |
| 4 | Mayor o igual (`>=`) |
| 5 | Menor que (`<`) |
| 6 | Menor o igual (`<=`) |

### Fase 2: Lógicos

| Ciclo | Comportamiento |
|-------|----------------|
| 7 | AND (`&&`) |
| 8 | OR (`\|\|`) |
| 9 | Id + Filters (`FieldPath.DocumentId`) - Multi-tenancy |

**Nota Ciclo 9:** Permite queries como `.Where(e => e.Id == id && e.TenantId == tenantId)` usando `FieldPath.DocumentId` para filtrar por el ID del documento (metadata) combinado con otros campos.

### Fase 3: IN

| Ciclo | Comportamiento |
|-------|----------------|
| 10 | IN (`list.Contains(field)`) |
| 11 | NOT IN (`!list.Contains(field)`) |

### Fase 4: Null y Boolean

| Ciclo | Comportamiento |
|-------|----------------|
| 12 | Es null (`== null`) |
| 13 | No es null (`!= null`) |
| 14 | Boolean true (`== true`) |
| 15 | Boolean false (`== false`) |

### Fase 5: Arrays

| Ciclo | Comportamiento |
|-------|----------------|
| 16 | Array contains (`array.Contains(value)`) |
| 17 | Array contains any |

### Fase 6: Ordenamiento

| Ciclo | Comportamiento |
|-------|----------------|
| 18 | OrderBy |
| 19 | OrderByDescending |
| 20 | ThenBy |
| 21 | ThenByDescending |

### Fase 7: Límites

| Ciclo | Comportamiento |
|-------|----------------|
| 22 | Take (Limit) |
| 23 | First / FirstOrDefault |
| 24 | Single / SingleOrDefault |
| 25 | Skip (documentar ineficiencia) |

### Fase 8: Agregaciones

| Ciclo | Comportamiento |
|-------|----------------|
| 26 | Count |
| 27 | Any |
| 28 | Sum |
| 29 | Average |
| 30 | Min (client-side) |
| 31 | Max (client-side) |

**Nota:** Min y Max requieren evaluación client-side. Firestore no los soporta nativamente.

### Fase 9: Límites adicionales

| Ciclo | Comportamiento |
|-------|----------------|
| 32 | TakeLast (LimitToLast) |

### Fase 10: Strings (workarounds)

| Ciclo | Comportamiento |
|-------|----------------|
| 33 | StartsWith (workaround >= <) |

### Fase 11: No soportados (decidir)

| Operación | Decisión |
|-----------|----------|
| EndsWith | ¿Client-side o NotSupportedException? |
| Contains (string) | ¿Client-side o NotSupportedException? |
| Like | ¿Client-side o NotSupportedException? |
| IgnoreCase | ¿Client-side o NotSupportedException? |

---

## Reglas

1. Diagnóstico primero
2. Un ciclo = un comportamiento
3. Test se escribe cuando toca, no antes
4. Cada ciclo = commits (RED, GREEN, REFACTOR si aplica)
5. Si pasa en verde → siguiente ciclo

---

## Limitaciones Firestore

Documentar cuando aparezcan:

| Restricción | Límite |
|-------------|--------|
| Valores en IN | Máximo 30 |
| Valores en NOT IN | Máximo 10 |
| Campos con rango | Solo 1 por query |
| OR (disyunciones) | Máximo 30 |

---

## Extensiones Futuras (fuera de LINQ estándar)

| Extensión | Firestore | Uso |
|-----------|-----------|-----|
| StartAt | `.StartAt(snapshot)` | Paginación / Streaming |
| StartAfter | `.StartAfter(snapshot)` | Paginación / Streaming |
| EndAt | `.EndAt(snapshot)` | Paginación / Streaming |
| EndBefore | `.EndBefore(snapshot)` | Paginación / Streaming |

**Nota:** Los cursores trabajan con document snapshots, no con valores. Serían API propia del provider para flujos asíncronos de datos.
