# 06 - Eliminar Service Locator del Visitor

**Fecha inicio:** 2025-12-22
**Fecha completado:** 2025-12-23 11:00
**Estado:** ✅ COMPLETADO
**Commit:** `5bb1a1e`

---

## Objetivo

Eliminar el Service Locator para `IFirestoreClientWrapper` del Visitor (línea 1212) y convertir los métodos estáticos a métodos de instancia para usar `_queryExecutor`.

---

## Trabajo Realizado

### Paso 1: Eliminar Service Locator ✅

Se eliminó la línea que obtenía `IFirestoreClientWrapper` vía Service Locator:
```csharp
// ANTES (línea 1212)
var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;

// DESPUÉS
// Eliminado - se usa _queryExecutor
```

### Paso 2: Extender IFirestoreQueryExecutor ✅

Se agregaron métodos para navegaciones:
```csharp
Task<QuerySnapshot> GetSubCollectionAsync(
    DocumentReference parentDoc,
    string subCollectionName,
    CancellationToken cancellationToken = default);

Task<DocumentSnapshot> GetDocumentByReferenceAsync(
    DocumentReference docRef,
    CancellationToken cancellationToken = default);

FirestoreDb Database { get; }
```

### Paso 3: Implementar en FirestoreQueryExecutor ✅

Se implementaron los nuevos métodos delegando al wrapper.

### Paso 4: Convertir métodos estáticos a instancia ✅

Se convirtieron los siguientes métodos de `static` a instancia:
- `DeserializeEntity`
- `DeserializeWithIncludesAndProject`
- `DeserializeAndProject`
- `LoadIncludes`
- `LoadNavigationAsync`
- `LoadSubCollectionAsync`
- `LoadReferenceAsync`
- `LoadComplexTypeIncludes`
- `LoadComplexTypeInclude`

Cambios en Expression Trees:
- `BindingFlags.Static` → `BindingFlags.Instance`
- `Expression.Call(method, ...)` → `Expression.Call(Expression.Constant(this), method, ...)`

---

## Archivos Modificados

| Archivo | Cambio |
|---------|--------|
| `Query/Contracts/IFirestoreQueryExecutor.cs` | +`GetSubCollectionAsync`, +`GetDocumentByReferenceAsync`, +`Database` |
| `Query/FirestoreQueryExecutor.cs` | Implementación de nuevos métodos |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` | Métodos static → instance, eliminar Service Locator |

---

## Tests

✅ 572 unit tests passing
✅ 195 integration tests passing

---

## Problema Identificado (para siguiente fase)

El contrato `IFirestoreQueryExecutor` ahora expone tipos del SDK de Google:
- `DocumentReference` en firmas de métodos
- `FirestoreDb Database` (redundante con `IFirestoreClientWrapper`)

**Decisión:** Se abordará en la siguiente fase cuando el executor devuelva tipos CLR.

---

## Siguiente Fase

Mover los métodos de navegación del Visitor al `NavigationLoader`:
- `LoadIncludes`
- `LoadNavigationAsync`
- `LoadSubCollectionAsync`
- `LoadReferenceAsync`
- `LoadComplexTypeIncludes`
- `LoadComplexTypeInclude`

Ver: `.plans/refactor/07-mover-navegaciones-a-loader.md`
