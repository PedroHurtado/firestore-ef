# 08 - Eliminar tipos SDK de IFirestoreQueryExecutor

**Fecha:** 2025-12-23
**Estado:** EN PROGRESO

---

## Objetivo

Eliminar todos los tipos del SDK de Google (`QuerySnapshot`, `DocumentSnapshot`) de `IFirestoreQueryExecutor` para que:
1. El contrato sea completamente agnóstico del SDK
2. El Visitor solo haga tracking (SRP)
3. El Executor ejecute, deserialice y cargue navegaciones

---

## Progreso

| Ciclo | Comportamiento | Estado | Commit | Tests |
|-------|----------------|--------|--------|-------|
| 1 | Crear nuevo método `ExecuteQueryAsync<T>` que retorne `IAsyncEnumerable<T>` | ✅ | 474087a | ✅ |
| 2 | Mover deserialización al Executor (sin Includes) | ✅ | 474087a | ✅ (incluido en Ciclo 1) |
| 3 | Mover carga de SubCollections al Executor | ✅ | 23582ac | ✅ |
| 4 | Mover carga de DocumentReferences al Executor | ✅ | 23582ac | ✅ |
| 5 | Mover carga de ComplexType Includes al Executor | ✅ | 23582ac | ✅ |
| 6 | Actualizar Visitor para solo iterar (delegando al Executor) | ✅ | 82b9c49 | ✅ |
| 7 | Crear nuevo método `ExecuteIdQueryAsync<T>` que retorne `Task<T?>` | ⏳ | | |
| 8 | Eliminar métodos antiguos del contrato | ⏳ | | |
| 9 | Eliminar `using Google.Cloud.Firestore` de IFirestoreQueryExecutor | ⏳ | | |
| 10 | Limpiar código muerto del Visitor | ⏳ | | |

---

## Diseño Final

### IFirestoreQueryExecutor (sin SDK)

```csharp
public interface IFirestoreQueryExecutor
{
    // Query normal - retorna entidades con navegaciones cargadas
    IAsyncEnumerable<T> ExecuteQueryAsync<T>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default) where T : class;

    // Query por ID - retorna entidad con navegaciones cargadas
    Task<T?> ExecuteIdQueryAsync<T>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default) where T : class;

    // Agregaciones - ya está bien
    Task<T> ExecuteAggregationAsync<T>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default);

    // Evaluación de expresiones - ya está bien
    int EvaluateIntExpression(
        Expression expression,
        QueryContext queryContext);
}
```

### Visitor (solo tracking)

```csharp
// El Visitor recibe entidades ya deserializadas con navegaciones
// Solo hace:
// 1. Iterar entidades del Executor
// 2. Si isTracking, registrar en StateManager
// 3. Retornar entidad
```

---

## Fase 1: Nuevo método ExecuteQueryAsync<T>

### Ciclo 1: Crear firma del método

**Cambios:**
- Agregar método a `IFirestoreQueryExecutor`
- Implementar en `FirestoreQueryExecutor` (sin Includes por ahora)

**Test:** Verificar que retorna entidades deserializadas

### Ciclo 2: Mover deserialización

**Cambios:**
- Mover `DeserializeEntity` del Visitor al Executor
- El Executor usa `_deserializer.DeserializeEntity<T>()`

**Test:** Query simple retorna entidad deserializada

---

## Fase 2: Mover carga de navegaciones al Executor

### Ciclo 3: SubCollections

**Cambios:**
- Mover `LoadSubCollectionAsync` del Visitor al Executor
- El Executor carga subcollections después de deserializar

**Test:** Query con `.Include(x => x.SubCollection)` retorna entidad con subcollection cargada

### Ciclo 4: DocumentReferences

**Cambios:**
- Mover `LoadReferenceAsync` del Visitor al Executor
- El Executor carga referencias después de deserializar

**Test:** Query con `.Include(x => x.Reference)` retorna entidad con referencia cargada

### Ciclo 5: ComplexType Includes

**Cambios:**
- Mover `LoadComplexTypeIncludes` del Visitor al Executor
- El Executor carga navegaciones en ComplexTypes

**Test:** Query con Include en ComplexType retorna entidad con navegación cargada

---

## Fase 3: Simplificar Visitor

### Ciclo 6: Visitor solo tracking

**Cambios:**
- Eliminar métodos de deserialización del Visitor
- Eliminar métodos de carga de navegaciones del Visitor
- Visitor solo itera y hace tracking

**Test:** Todos los tests de integración siguen pasando

---

## Fase 4: Query por ID

### Ciclo 7: ExecuteIdQueryAsync<T>

**Cambios:**
- Agregar método a `IFirestoreQueryExecutor`
- Implementar en `FirestoreQueryExecutor`
- El Executor deserializa y carga navegaciones

**Test:** Query por ID retorna entidad deserializada con navegaciones

---

## Fase 5: Limpieza

### Ciclo 8: Eliminar métodos antiguos

**Cambios:**
- Eliminar `ExecuteQueryAsync` que retorna `QuerySnapshot`
- Eliminar `ExecuteIdQueryAsync` que retorna `DocumentSnapshot`
- Eliminar `GetSubCollectionAsync` que retorna `QuerySnapshot`
- Eliminar `GetDocumentByReferenceAsync` que retorna `DocumentSnapshot`

**Test:** Build compila, todos los tests pasan

### Ciclo 9: Eliminar SDK del contrato

**Cambios:**
- Eliminar `using Google.Cloud.Firestore` de `IFirestoreQueryExecutor`
- Verificar que no hay tipos SDK en el contrato

**Test:** Build compila

### Ciclo 10: Limpiar Visitor

**Cambios:**
- Eliminar código muerto del Visitor
- Eliminar imports no usados

**Test:** Todos los tests pasan, coverage no baja

---

## Archivos Afectados

| Archivo | Cambio |
|---------|--------|
| `Query/Contracts/IFirestoreQueryExecutor.cs` | Nuevos métodos genéricos, eliminar métodos SDK |
| `Query/FirestoreQueryExecutor.cs` | Implementar deserialización y carga de navegaciones |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` | Simplificar a solo tracking |
| Tests unitarios | Actualizar mocks |
| Tests integración | Deben seguir pasando |

---

## Principios

1. **SRP**: Cada clase hace una sola cosa
   - Executor: ejecuta, deserializa, carga navegaciones
   - Visitor: tracking

2. **No SDK en contratos**: Los contratos públicos no exponen tipos del SDK

3. **DDD**: Las entidades siempre tienen Id accesible

4. **TDD**: Un ciclo = un comportamiento, test primero

---

## Notas

- El Deserializer ya sabe poblar el Id desde `documentSnapshot.Id`
- Las navegaciones (SubCollections, References) son siempre entidades con Id
- El tracking usa `entity.Id` que ya está deserializado
