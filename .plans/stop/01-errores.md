# Análisis de Errores de Arquitectura - Bypass del Wrapper

**Fecha:** 2025-12-20
**Estado:** Crítico - Requiere refactorización

---

## Resumen Ejecutivo

El proyecto tiene **11 llamadas directas a Firestore** que no pasan por `IFirestoreClientWrapper`, lo que viola el principio de responsabilidad única y hace imposible depurar, interceptar o mockear las operaciones de base de datos.

**Impacto:** ~44 de 184 tests (24%) usan flujos que bypasean el wrapper.

---

## Detalle de Bypasses

### 1. `FirestoreQueryExecutor.cs` - Agregaciones (6 bypasses)

| Línea | Método | Llamada directa |
|-------|--------|-----------------|
| 765 | `ExecuteCountAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 778 | `ExecuteAnyAsync` | `limitedQuery.GetSnapshotAsync()` |
| 797 | `ExecuteSumAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 820 | `ExecuteAverageAsync` | `aggregateQuery.GetSnapshotAsync()` |
| 848 | `ExecuteMinAsync` | `minQuery.GetSnapshotAsync()` |
| 876 | `ExecuteMaxAsync` | `maxQuery.GetSnapshotAsync()` |

**Causa raíz:**
Las agregaciones de Firestore (`Count`, `Sum`, `Average`) devuelven `AggregateQuerySnapshot`, no `QuerySnapshot`. El método `IFirestoreClientWrapper.ExecuteQueryAsync` solo acepta `Google.Cloud.Firestore.Query` y devuelve `QuerySnapshot`.

```csharp
// El wrapper actual:
Task<QuerySnapshot> ExecuteQueryAsync(Query query, CancellationToken cancellationToken)

// Pero las agregaciones necesitan:
var aggregateQuery = query.Count();  // Devuelve AggregateQuery
var snapshot = await aggregateQuery.GetSnapshotAsync();  // Devuelve AggregateQuerySnapshot
```

**Solución propuesta:**
Añadir método al wrapper:
```csharp
Task<AggregateQuerySnapshot> ExecuteAggregateQueryAsync(AggregateQuery query, CancellationToken cancellationToken)
```

**Tests afectados:** 18 tests en `AggregationTests.cs`

---

### 2. `FirestoreShapedQueryCompilingExpressionVisitor.cs` - SubCollections/References (5 bypasses)

| Línea | Método | Llamada directa |
|-------|--------|-----------------|
| 1439 | `LoadSubCollectionAsync` | `subCollectionRef.GetSnapshotAsync()` |
| 1568 | `LoadReferenceAsync` | `docRef.GetSnapshotAsync()` |
| 1591 | `LoadReferenceAsync` | `docRefFromId.GetSnapshotAsync()` |
| 1743 | `LoadComplexTypeInclude` | `docRef.GetSnapshotAsync()` |
| 1754 | `LoadComplexTypeInclude` | `docRefFromId.GetSnapshotAsync()` |

**Causa raíz:**
Para cargar subcollections se necesita acceder a `DocumentReference.Collection()`, que no está expuesto en el wrapper. Se tomó un atajo accediendo directamente.

```csharp
// Lo que se hizo (incorrecto):
var subCollectionRef = parentDoc.Reference.Collection(subCollectionName);
var snapshot = await subCollectionRef.GetSnapshotAsync();
```

**Solución propuesta:**
Extender el wrapper:
```csharp
Task<QuerySnapshot> GetSubCollectionAsync(DocumentReference parentDoc, string subCollectionName, CancellationToken cancellationToken)
Task<DocumentSnapshot> GetDocumentByReferenceAsync(DocumentReference docRef, CancellationToken cancellationToken)
```

**Tests afectados:**
- 8 tests en `SubCollectionTests.cs`
- 11 tests en `ReferenceSerializationTests.cs`
- 7 tests en `SelectSubcollectionTests.cs`

---

## Resumen del Impacto

| Componente | Bypasses | Tests afectados | Violación SRP |
|------------|----------|-----------------|---------------|
| `FirestoreQueryExecutor` | 6 | 18 | Ejecuta queries Y hace llamadas directas a Firestore |
| `FirestoreShapedQueryCompilingExpressionVisitor` | 5 | ~26 | Compila expressions Y hace llamadas directas a Firestore |
| **TOTAL** | **11** | **~44 de 184 (24%)** | |

---

## Consecuencias

1. **No hay un punto único de entrada a Firestore** - Imposible depurar con un solo breakpoint
2. **24% de los tests** usan flujos que no pasan por el wrapper
3. **El wrapper existe pero es incompleto** - No cubre subcollections, references ni agregaciones
4. **Violación grave de SRP**: Clases que deberían solo compilar/ejecutar también hacen I/O directo
5. **Imposible mockear** estas operaciones para unit tests

---

## Plan de Corrección

### Fase 1: Extender `IFirestoreClientWrapper`

```csharp
public interface IFirestoreClientWrapper
{
    // Métodos existentes...

    // Nuevos métodos para agregaciones
    Task<AggregateQuerySnapshot> ExecuteAggregateQueryAsync(AggregateQuery query, CancellationToken cancellationToken = default);

    // Nuevos métodos para subcollections
    Task<QuerySnapshot> GetSubCollectionAsync(DocumentSnapshot parentDoc, string subCollectionName, CancellationToken cancellationToken = default);

    // Nuevos métodos para references
    Task<DocumentSnapshot> GetDocumentByReferenceAsync(DocumentReference docRef, CancellationToken cancellationToken = default);
}
```

### Fase 2: Implementar en `FirestoreClientWrapper`

### Fase 3: Refactorizar `FirestoreQueryExecutor`
- Reemplazar las 6 llamadas directas por llamadas al wrapper

### Fase 4: Refactorizar `FirestoreShapedQueryCompilingExpressionVisitor`
- Reemplazar las 5 llamadas directas por llamadas al wrapper

### Fase 5: Verificar
- Todos los tests deben seguir pasando
- Un breakpoint en el wrapper debe capturar TODAS las operaciones

---

## Llamadas que SÍ pasan por el wrapper (correctas)

| Archivo | Línea | Método del wrapper |
|---------|-------|-------------------|
| `FirestoreClientWrapper.cs` | 71 | `GetDocumentAsync` |
| `FirestoreClientWrapper.cs` | 84 | `GetCollectionAsync` |
| `FirestoreClientWrapper.cs` | 113 | `ExecuteQueryAsync` |

Estas son las únicas 3 operaciones centralizadas actualmente.
