# 03 - Delta: Cambios Necesarios

## Resumen de Cambios

| Componente | Acción | Complejidad |
|------------|--------|-------------|
| `IFirestoreQueryExecutor` | Cambiar firma (retorna entidades) | Media |
| `FirestoreQueryExecutor` | Inyectar Deserializer, cambiar retornos | Media |
| `FirestoreServiceCollectionExtensions` | Registrar IFirestoreQueryExecutor | Baja |
| `FirestoreShapedQueryCompilingExpressionVisitorFactory` | Inyectar IFirestoreQueryExecutor | Baja |
| `FirestoreShapedQueryCompilingExpressionVisitor` | Recibir executor, eliminar Service Locator | Alta |
| `FirestoreQueryingEnumerable` | Recibir executor, eliminar new/ServiceLocator | Media |
| `FirestoreAggregationQueryingEnumerable` | Recibir executor, eliminar new/ServiceLocator | Media |

---

## 1. IFirestoreQueryExecutor

### Antes (actual)
```csharp
public interface IFirestoreQueryExecutor
{
    Task<QuerySnapshot> ExecuteQueryAsync(...);
    Task<DocumentSnapshot?> ExecuteIdQueryAsync(...);
}
```

### Después
```csharp
public interface IFirestoreQueryExecutor
{
    Task<IReadOnlyList<TEntity>> ExecuteQueryAsync<TEntity>(FirestoreQueryExpression query, CancellationToken ct);
    Task<TEntity?> ExecuteIdQueryAsync<TEntity>(FirestoreQueryExpression query, CancellationToken ct);
    Task<long> ExecuteCountAsync(FirestoreQueryExpression query, CancellationToken ct);
    Task<bool> ExecuteAnyAsync(FirestoreQueryExpression query, CancellationToken ct);
    Task<TResult> ExecuteSumAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct);
    Task<TResult> ExecuteAverageAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct);
    Task<TResult> ExecuteMinAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct);
    Task<TResult> ExecuteMaxAsync<TResult>(FirestoreQueryExpression query, string field, CancellationToken ct);
}
```

---

## 2. FirestoreQueryExecutor

### Cambios en Constructor
```csharp
// Antes
public FirestoreQueryExecutor(IFirestoreClientWrapper client, ILogger<FirestoreQueryExecutor> logger)

// Después
public FirestoreQueryExecutor(
    IFirestoreClientWrapper client,
    IFirestoreDocumentDeserializer deserializer,
    ILogger<FirestoreQueryExecutor> logger)
```

### Cambios en Métodos
- `ExecuteQueryAsync`: Usar deserializer, retornar `IReadOnlyList<TEntity>`
- `ExecuteIdQueryAsync`: Usar deserializer, retornar `TEntity?`
- Agregaciones: Ya implementadas, solo ajustar firmas

---

## 3. FirestoreServiceCollectionExtensions

### Agregar
```csharp
.TryAddScoped<IFirestoreQueryExecutor, FirestoreQueryExecutor>()
```

---

## 4. FirestoreShapedQueryCompilingExpressionVisitorFactory

### Antes
```csharp
public class FirestoreShapedQueryCompilingExpressionVisitorFactory
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;

    public FirestoreShapedQueryCompilingExpressionVisitorFactory(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext ctx)
    {
        return new FirestoreShapedQueryCompilingExpressionVisitor(_dependencies, ctx);
    }
}
```

### Después
```csharp
public class FirestoreShapedQueryCompilingExpressionVisitorFactory
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
    private readonly IFirestoreQueryExecutor _queryExecutor;

    public FirestoreShapedQueryCompilingExpressionVisitorFactory(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        IFirestoreQueryExecutor queryExecutor)
    {
        _dependencies = dependencies;
        _queryExecutor = queryExecutor;
    }

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext ctx)
    {
        return new FirestoreShapedQueryCompilingExpressionVisitor(_dependencies, ctx, _queryExecutor);
    }
}
```

---

## 5. FirestoreShapedQueryCompilingExpressionVisitor

### Eliminar
- Líneas 1198-1201: Service Locator para typeMappingSource, collectionManager, loggerFactory, clientWrapper
- Toda lógica de deserialización inline (ya movida al Deserializer en ciclos anteriores)
- `using Google.Cloud.Firestore`

### Agregar
- Campo `_queryExecutor` recibido por constructor
- Pasar `_queryExecutor` al crear Enumerables

---

## 6. FirestoreQueryingEnumerable

### Eliminar
- Líneas 100-101, 196-197: Service Locator
- Líneas 105, 201: `new FirestoreQueryExecutor()`

### Agregar
- Constructor recibe `IFirestoreQueryExecutor`
- `MoveNext/MoveNextAsync` usa el executor inyectado

---

## 7. FirestoreAggregationQueryingEnumerable

### Eliminar
- Líneas 75-76, 123-124: Service Locator
- Líneas 79, 127: `new FirestoreQueryExecutor()`

### Agregar
- Constructor recibe `IFirestoreQueryExecutor`
- Métodos usan el executor inyectado

---

## Impacto en Tests

| Test | Cambio |
|------|--------|
| `FirestoreQueryExecutorTest` | Actualizar constructor con Deserializer mock |
| Tests de integración | Sin cambios (usan DI real) |
| Nuevos tests | Mockear IFirestoreQueryExecutor fácilmente |