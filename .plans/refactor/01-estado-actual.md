# 01 - Estado Actual: Violaciones de Arquitectura

## Resumen

| Tipo de Violación | Cantidad | Archivos |
|-------------------|----------|----------|
| `new FirestoreQueryExecutor()` | 4 | 2 |
| Service Locator (`GetService`) | 15 | 3 |
| `using Google.Cloud.Firestore` | 15 | 15 |

---

## 1. Violaciones `new FirestoreQueryExecutor()`

### FirestoreQueryingEnumerable.cs

| Línea | Método | Código |
|-------|--------|--------|
| 105 | `Enumerator.MoveNext()` | `new FirestoreQueryExecutor(clientWrapper, executorLogger)` |
| 201 | `AsyncEnumerator.MoveNextAsync()` | `new FirestoreQueryExecutor(clientWrapper, executorLogger)` |

### FirestoreAggregationQueryingEnumerable.cs

| Línea | Método | Código |
|-------|--------|--------|
| 79 | `Enumerator.MoveNext()` | `new FirestoreQueryExecutor(clientWrapper, executorLogger)` |
| 127 | `AsyncEnumerator.MoveNextAsync()` | `new FirestoreQueryExecutor(clientWrapper, executorLogger)` |

---

## 2. Violaciones Service Locator

### FirestoreQueryingEnumerable.cs (4 llamadas)

| Línea | Servicio |
|-------|----------|
| 100 | `IFirestoreClientWrapper` |
| 101 | `ILoggerFactory` |
| 196 | `IFirestoreClientWrapper` |
| 197 | `ILoggerFactory` |

### FirestoreAggregationQueryingEnumerable.cs (4 llamadas)

| Línea | Servicio |
|-------|----------|
| 75 | `IFirestoreClientWrapper` |
| 76 | `ILoggerFactory` |
| 123 | `IFirestoreClientWrapper` |
| 124 | `ILoggerFactory` |

### FirestoreShapedQueryCompilingExpressionVisitor.cs (4 llamadas)

| Línea | Servicio |
|-------|----------|
| 1198 | `ITypeMappingSource` |
| 1199 | `IFirestoreCollectionManager` |
| 1200 | `ILoggerFactory` |
| 1201 | `IFirestoreClientWrapper` |

### FirestoreDocumentDeserializer.cs (3 llamadas - proxies/lazy loading)

| Línea | Servicio |
|-------|----------|
| 895 | `IProxyFactory` (dinámico) |
| 912 | `ILazyLoader` (dinámico) |
| 922 | `IEntityMaterializerSource` (dinámico) |

**Nota:** Las 3 llamadas en Deserializer son para lazy loading/proxies de EF Core, casos especiales.

---

## 3. Dependencias Directas a Google.Cloud.Firestore

Archivos que **NO deberían** tener `using Google.Cloud.Firestore`:

| Archivo | Razón |
|---------|-------|
| `FirestoreShapedQueryCompilingExpressionVisitor.cs` | Visitor solo compila, no ejecuta |
| `FirestoreQueryingEnumerable.cs` | Debería recibir entidades, no snapshots |
| `FirestoreAggregationQueryingEnumerable.cs` | Debería recibir valores, no snapshots |
| `FirestoreQueryExpression.cs` | Es un AST, no debería conocer SDK |
| `NavigationLoader.cs` | Usa wrapper, no debería exponer tipos SDK |
| `INavigationLoader.cs` | Interfaz expone `DocumentSnapshot` |
| `FirestoreDocumentDeserializer.cs` | Recibe snapshots - aceptable |
| `IFirestoreDocumentDeserializer.cs` | Interfaz - aceptable |
| `FirestoreDatabase.cs` | Storage layer - aceptable |
| `FirestoreTransaction.cs` | Storage layer - aceptable |

Archivos que **SÍ deberían** tenerlo:

| Archivo | Razón |
|---------|-------|
| `FirestoreClientWrapper.cs` | Único punto de I/O |
| `IFirestoreClientWrapper.cs` | Interfaz del wrapper |
| `IFirestoreQueryExecutor.cs` | Actualmente expone `QuerySnapshot` |
| `FirestoreQueryExecutor.cs` | Implementación que usa wrapper |

---

## 4. Problema de Fondo

El `FirestoreShapedQueryCompilingExpressionVisitor` tiene 2000+ líneas haciendo:

1. **Compilación de expresiones** (correcto)
2. **Generación de código de ejecución** (correcto)
3. **Lógica de deserialización inline** (incorrecto - debería estar en Deserializer)
4. **Conocimiento del SDK de Firestore** (incorrecto - debería usar abstracciones)

El Visitor genera lambdas que:
- Resuelven servicios con Service Locator
- Crean `FirestoreQueryExecutor` con `new`
- Conocen tipos de Firestore directamente