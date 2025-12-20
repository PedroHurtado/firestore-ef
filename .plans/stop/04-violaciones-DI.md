# Violaciones de Dependency Injection

**Fecha:** 2025-12-20

---

## 1. `FirestoreQueryExecutor` - Sin interfaz, instanciado con `new`

**Lugares:**
| Archivo | Línea |
|---------|-------|
| `FirestoreQueryingEnumerable.cs` | 105 |
| `FirestoreQueryingEnumerable.cs` | 201 |
| `FirestoreAggregationQueryingEnumerable.cs` | 79 |
| `FirestoreAggregationQueryingEnumerable.cs` | 127 |

**Problema:** No existe `IFirestoreQueryExecutor`. Se crea instancia nueva en cada query.

---

## 2. Service Locator Anti-Pattern

| Archivo | Líneas | Servicios resueltos manualmente |
|---------|--------|--------------------------------|
| `FirestoreQueryingEnumerable.cs` | 100-101, 196-197 | `IFirestoreClientWrapper`, `ILoggerFactory` |
| `FirestoreAggregationQueryingEnumerable.cs` | 75-76, 123-124 | `IFirestoreClientWrapper`, `ILoggerFactory` |
| `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1198-1201 | `ITypeMappingSource`, `IFirestoreCollectionManager`, `ILoggerFactory`, `IFirestoreClientWrapper` |

**Total:** 12 llamadas a `serviceProvider.GetService()`

---

## 3. `FirestoreDocumentDeserializer` - Creado inline

Se instancia con `new` dentro del Shaper compilado.

---

## Resumen

| Violación | Ocurrencias |
|-----------|-------------|
| `new FirestoreQueryExecutor()` | 4 |
| `serviceProvider.GetService()` | 12 |
| Falta interfaz `IFirestoreQueryExecutor` | 1 |
| `new FirestoreDocumentDeserializer()` | 1 |

---

## Clases con estado vs sin estado

| Clase | Estado | Debería ser |
|-------|--------|-------------|
| `FirestoreQueryExpression` | Con estado (acumula filtros, etc.) | Correcto |
| `FirestoreQueryExecutor` | Sin estado (solo `_client`, `_logger`) | Singleton/Scoped inyectado |
| `FirestoreDocumentDeserializer` | Sin estado | Singleton/Scoped inyectado |
