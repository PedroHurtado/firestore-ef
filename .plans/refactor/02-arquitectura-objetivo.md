# 02 - Arquitectura Objetivo

## Principio

Cada clase tiene UNA responsabilidad. Las dependencias se inyectan, nunca se crean con `new` ni se resuelven con Service Locator.

---

## Flujo de Ejecución Objetivo

```
LINQ Query
    ↓
FirestoreQueryableMethodTranslatingExpressionVisitor
    ↓ traduce a
FirestoreQueryExpression (AST puro, sin SDK)
    ↓
FirestoreShapedQueryCompilingExpressionVisitor
    ↓ compila a
Lambda que usa IFirestoreQueryExecutor (inyectado)
    ↓
FirestoreQueryingEnumerable (solo itera)
    ↓ llama a
IFirestoreQueryExecutor.ExecuteAsync(FirestoreQueryExpression)
    ↓ retorna
IEnumerable<TEntity> o TScalar
```

---

## Responsabilidades por Clase

### FirestoreShapedQueryCompilingExpressionVisitor
- **Hace:** Compila expresiones a lambdas ejecutables
- **NO hace:** Ejecutar queries, deserializar, conocer SDK
- **Recibe:** `IFirestoreQueryExecutor` (inyectado vía Factory)
- **Elimina:** `using Google.Cloud.Firestore`

### FirestoreQueryingEnumerable
- **Hace:** Itera resultados, implementa `IEnumerable<T>`
- **NO hace:** Crear executor, resolver servicios, deserializar
- **Recibe:** `IFirestoreQueryExecutor` (inyectado)
- **Elimina:** Service Locator, `new FirestoreQueryExecutor()`

### FirestoreAggregationQueryingEnumerable
- **Hace:** Ejecuta agregaciones, retorna valores escalares
- **NO hace:** Crear executor, resolver servicios
- **Recibe:** `IFirestoreQueryExecutor` (inyectado)
- **Elimina:** Service Locator, `new FirestoreQueryExecutor()`

### IFirestoreQueryExecutor (NUEVA firma)
```csharp
public interface IFirestoreQueryExecutor
{
    // Queries de entidades
    Task<IReadOnlyList<TEntity>> ExecuteQueryAsync<TEntity>(
        FirestoreQueryExpression query,
        CancellationToken ct = default);

    Task<TEntity?> ExecuteIdQueryAsync<TEntity>(
        FirestoreQueryExpression query,
        CancellationToken ct = default);

    // Agregaciones
    Task<long> ExecuteCountAsync(FirestoreQueryExpression query, CancellationToken ct = default);
    Task<bool> ExecuteAnyAsync(FirestoreQueryExpression query, CancellationToken ct = default);
    Task<TResult> ExecuteSumAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct = default);
    Task<TResult> ExecuteAverageAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct = default);
    Task<TResult> ExecuteMinAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct = default);
    Task<TResult> ExecuteMaxAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct = default);
}
```

### FirestoreQueryExecutor (implementación)
- **Hace:** Orquesta ejecución completa
- **Usa:**
  - `IFirestoreClientWrapper` - I/O
  - `IFirestoreDocumentDeserializer` - Materialización
- **Retorna:** Entidades o valores, NO snapshots

---

## Registro en DI

```csharp
// FirestoreServiceCollectionExtensions.cs
builder.TryAddProviderSpecificServices(b => b
    .TryAddScoped<IFirestoreQueryExecutor, FirestoreQueryExecutor>()
    // ... existentes
);
```

---

## Inyección en la Cadena

```
DI Container
    ↓ inyecta IFirestoreQueryExecutor a
FirestoreShapedQueryCompilingExpressionVisitorFactory
    ↓ pasa al constructor de
FirestoreShapedQueryCompilingExpressionVisitor
    ↓ pasa al constructor de
FirestoreQueryingEnumerable / FirestoreAggregationQueryingEnumerable
```

---

## Resultado Final

| Archivo | `using Google.Cloud.Firestore` | Service Locator | `new Executor()` |
|---------|-------------------------------|-----------------|------------------|
| Visitor | NO | NO | NO |
| QueryingEnumerable | NO | NO | NO |
| AggregationEnumerable | NO | NO | NO |
| QueryExecutor | SÍ (usa wrapper) | NO | N/A |
| ClientWrapper | SÍ (único I/O) | NO | N/A |