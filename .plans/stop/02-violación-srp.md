# Análisis de Violación del Principio de Responsabilidad Única (SRP)

**Fecha:** 2025-12-20
**Estado:** Crítico - Requiere refactorización

---

## Resumen Ejecutivo

Dos clases del proyecto violan gravemente el Principio de Responsabilidad Única (SRP). Cada una mezcla su responsabilidad declarada con operaciones de I/O directas a Firestore.

---

## 1. `FirestoreQueryExecutor`

### Responsabilidad Declarada
Ejecutar queries contra Firestore.

### Responsabilidades Reales (Violación SRP)

| # | Responsabilidad | Debería estar aquí |
|---|-----------------|-------------------|
| 1 | Construir queries (Where, OrderBy, Limit) | ❌ No |
| 2 | Ejecutar queries normales (vía wrapper) | ✅ Sí |
| 3 | Ejecutar queries por ID (vía wrapper) | ✅ Sí |
| 4 | **Ejecutar agregaciones (BYPASS directo)** | ❌ No |
| 5 | Evaluar expresiones de ID | ❌ No |
| 6 | Logging de operaciones | ⚠️ Discutible |

### Bypasses Identificados (6 total)

| Línea | Método | Llamada directa |
|-------|--------|-----------------|
| 765 | `ExecuteCountAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 778 | `ExecuteAnyAsync` | `limitedQuery.GetSnapshotAsync()` |
| 797 | `ExecuteSumAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 820 | `ExecuteAverageAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 848 | `ExecuteMinAsync` | `minQuery.GetSnapshotAsync()` |
| 876 | `ExecuteMaxAsync` | `maxQuery.GetSnapshotAsync()` |

### Problema
La clase que debería **solo ejecutar** queries también **construye** queries y hace **llamadas directas** a Firestore para agregaciones, sin pasar por `IFirestoreClientWrapper`.

---

## 2. `FirestoreShapedQueryCompilingExpressionVisitor`

### Responsabilidad Declarada
Compilar expresiones de query con forma (shaped) para generar código ejecutable.

### Responsabilidades Reales (Violación SRP)

| # | Responsabilidad | Debería estar aquí |
|---|-----------------|-------------------|
| 1 | Compilar expresiones de query | ✅ Sí |
| 2 | Copiar metadata de Includes | ✅ Sí |
| 3 | Crear delegates de materialización | ✅ Sí |
| 4 | **Cargar subcollections (BYPASS directo)** | ❌ No |
| 5 | **Cargar references (BYPASS directo)** | ❌ No |
| 6 | **Cargar ComplexType includes (BYPASS directo)** | ❌ No |
| 7 | Compilar filtros para includes | ⚠️ Discutible |
| 8 | Resolver identidades (identity resolution) | ❌ No |
| 9 | Fixup de navegaciones | ❌ No |
| 10 | Gestionar ChangeTracker | ❌ No |

### Bypasses Identificados (5 total)

| Línea | Método | Llamada directa |
|-------|--------|-----------------|
| 1439 | `LoadSubCollectionAsync` | `subCollectionRef.GetSnapshotAsync()` |
| 1568 | `LoadReferenceAsync` | `docRef.GetSnapshotAsync()` |
| 1591 | `LoadReferenceAsync` | `docRefFromId.GetSnapshotAsync()` |
| 1743 | `LoadComplexTypeInclude` | `docRef.GetSnapshotAsync()` |
| 1754 | `LoadComplexTypeInclude` | `docRefFromId.GetSnapshotAsync()` |

### Problema
La clase que debería **solo compilar** expresiones también:
- Hace **I/O directo** a Firestore
- **Deserializa** documentos
- **Gestiona** el ChangeTracker de EF Core
- Hace **fixup** de navegaciones

---

## Comparativa de Violaciones

| Clase | Responsabilidad Declarada | Responsabilidades Reales | Bypasses |
|-------|---------------------------|--------------------------|----------|
| `FirestoreQueryExecutor` | Ejecutar queries | 6 responsabilidades | 6 |
| `FirestoreShapedQueryCompilingExpressionVisitor` | Compilar expressions | 10 responsabilidades | 5 |

---

## Consecuencias de las Violaciones

1. **Imposible depurar con un solo breakpoint** - Las operaciones de Firestore están dispersas
2. **Imposible mockear** - No se pueden hacer unit tests de estas clases
3. **Acoplamiento alto** - Cambiar Firestore requiere modificar múltiples clases
4. **Mantenimiento difícil** - Cada clase hace demasiadas cosas
5. **Tests frágiles** - Los tests de integración son la única opción

---

## Arquitectura Correcta (Propuesta)

### Separación de Responsabilidades

```
┌─────────────────────────────────────────────────────────────┐
│                    IFirestoreClientWrapper                   │
│         (ÚNICO punto de entrada a Firestore)                │
├─────────────────────────────────────────────────────────────┤
│ ExecuteQueryAsync(Query)                                    │
│ ExecuteAggregateQueryAsync(AggregateQuery)     ← NUEVO     │
│ GetSubCollectionAsync(DocumentSnapshot, name)  ← NUEVO     │
│ GetDocumentByReferenceAsync(DocumentReference) ← NUEVO     │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
┌─────────┴─────────┐ ┌───────┴───────┐ ┌────────┴────────┐
│ FirestoreQuery    │ │ Aggregation   │ │ SubCollection   │
│ Executor          │ │ Executor      │ │ Loader          │
│ (solo ejecutar)   │ │ (solo agregar)│ │ (solo cargar)   │
└───────────────────┘ └───────────────┘ └─────────────────┘
```

### Clases Refactorizadas

1. **`FirestoreQueryExecutor`** → Solo ejecuta queries normales
2. **`FirestoreAggregationExecutor`** (nuevo) → Solo ejecuta agregaciones
3. **`FirestoreSubCollectionLoader`** (nuevo) → Solo carga subcollections
4. **`FirestoreReferenceLoader`** (nuevo) → Solo carga references
5. **`FirestoreShapedQueryCompilingExpressionVisitor`** → Solo compila, delega I/O

---

## Prioridad de Corrección

1. **Alta**: Extender `IFirestoreClientWrapper` con métodos faltantes
2. **Alta**: Refactorizar bypasses en `FirestoreQueryExecutor`
3. **Alta**: Refactorizar bypasses en `FirestoreShapedQueryCompilingExpressionVisitor`
4. **Media**: Extraer responsabilidades a clases especializadas
5. **Baja**: Mejorar tests para cubrir nuevas clases
