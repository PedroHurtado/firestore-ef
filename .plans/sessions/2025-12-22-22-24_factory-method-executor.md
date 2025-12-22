# 05 - Factory Method para FirestoreQueryExecutor

**Fecha:** 2025-12-22 (actualizado 09:45)
**Estado:** COMPLETADO
**Commit:** `f6ea4f7`

## Objetivo

Centralizar la creación de `FirestoreQueryExecutor` en un factory method estático para preparar la inyección de dependencias futura.

---

## Estado Antes del Refactoring

### Violaciones (4 lugares con `new FirestoreQueryExecutor`)

| Archivo | Línea | Contexto |
|---------|-------|----------|
| `FirestoreQueryingEnumerable.cs` | 105 | `SyncEnumerator.InitializeEnumerator()` |
| `FirestoreQueryingEnumerable.cs` | 201 | `AsyncEnumerator.InitializeEnumeratorAsync()` |
| `FirestoreAggregationQueryingEnumerable.cs` | 79 | `SyncEnumerator.ExecuteAggregation()` |
| `FirestoreAggregationQueryingEnumerable.cs` | 127 | `AsyncEnumerator.ExecuteAggregationAsync()` |

### Contrato Incompleto

`IFirestoreQueryExecutor` no tenía:
- `ExecuteAggregationAsync<T>`
- `EvaluateIntExpression`

---

## Plan de Ejecución (COMPLETADO)

### Paso 1: Agregar métodos al contrato IFirestoreQueryExecutor ✅

```csharp
// En IFirestoreQueryExecutor.cs agregado:
Task<T> ExecuteAggregationAsync<T>(
    FirestoreQueryExpression queryExpression,
    QueryContext queryContext,
    CancellationToken cancellationToken = default);

int EvaluateIntExpression(
    System.Linq.Expressions.Expression expression,
    QueryContext queryContext);
```

### Paso 2: Crear Factory Method en FirestoreQueryExecutor ✅

```csharp
public class FirestoreQueryExecutor : IFirestoreQueryExecutor
{
    // TODO: Reemplazar factory method por inyección de dependencias
    public static IFirestoreQueryExecutor Create(
        IFirestoreClientWrapper client,
        ILogger<FirestoreQueryExecutor> logger)
    {
        return new FirestoreQueryExecutor(client, logger);
    }

    // Cambiar de public a protected
    protected FirestoreQueryExecutor(
        IFirestoreClientWrapper client,
        ILogger<FirestoreQueryExecutor> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

### Paso 3: Modificar FirestoreQueryingEnumerable ✅

Agregado campo `_executor` y recibido en constructor. Eliminado Service Locator.

### Paso 4: Modificar FirestoreAggregationQueryingEnumerable ✅

Mismo patrón que Paso 3.

### Paso 5: Modificar FirestoreShapedQueryCompilingExpressionVisitor ✅

- Agregado campo `_queryExecutor`
- Actualizado constructor
- Actualizados 4 métodos que crean Enumerables vía Expression Trees

### Paso 6: Modificar FirestoreShapedQueryCompilingExpressionVisitorFactory ✅

```csharp
public class FirestoreShapedQueryCompilingExpressionVisitorFactory
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
    private readonly IFirestoreClientWrapper _clientWrapper;
    private readonly ILoggerFactory _loggerFactory;

    public FirestoreShapedQueryCompilingExpressionVisitorFactory(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        IFirestoreClientWrapper clientWrapper,
        ILoggerFactory loggerFactory)
    {
        _dependencies = dependencies;
        _clientWrapper = clientWrapper;
        _loggerFactory = loggerFactory;
    }

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext ctx)
    {
        var executor = FirestoreQueryExecutor.Create(
            _clientWrapper,
            _loggerFactory.CreateLogger<FirestoreQueryExecutor>());

        return new FirestoreShapedQueryCompilingExpressionVisitor(
            _dependencies,
            ctx,
            executor);
    }
}
```

### Paso 7: Eliminar Service Locator de Enumerators ✅

Ya no resuelven dependencias - usan `_enumerable._executor` directamente.

### Paso 8: Actualizar tests unitarios ✅

- `FirestoreQueryExecutorTest.cs` - Cambiados tests de constructor a factory method
- `FirestoreQueryingEnumerableTest.cs` - Actualizado para 6 parámetros

---

## Archivos Modificados

| Archivo | Cambio |
|---------|--------|
| `Query/Contracts/IFirestoreQueryExecutor.cs` | +`ExecuteAggregationAsync<T>`, +`EvaluateIntExpression` |
| `Query/FirestoreQueryExecutor.cs` | +`Create()`, constructor → `protected` |
| `Query/FirestoreQueryingEnumerable.cs` | +`_executor`, -Service Locator |
| `Query/FirestoreAggregationQueryingEnumerable.cs` | +`_executor`, -Service Locator |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` | +`_queryExecutor`, 4 métodos actualizados |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitorFactory.cs` | +DI, +crear executor |
| `tests/.../FirestoreQueryExecutorTest.cs` | Tests de factory method |
| `tests/.../FirestoreQueryingEnumerableTest.cs` | 6 parámetros |

---

## Verificación

```bash
dotnet test
# Unit tests: 572 passed
# Integration tests: 195 passed, 3 skipped
```

---

## Resultado Final

| Métrica | Antes | Después |
|---------|-------|---------|
| `new FirestoreQueryExecutor()` en producción | 4 | 0 |
| `new FirestoreQueryExecutor()` en tests | 3 | 0 (usan `Create()`) |
| Service Locator en Enumerables | 8 | 0 |
| Factory method centralizado | 0 | 1 |

---

## Siguiente Paso (Futuro)

Cuando se decida registrar en DI:

1. Registrar `IFirestoreQueryExecutor` en `FirestoreServiceCollectionExtensions`
2. Inyectar directamente en el Visitor vía Factory
3. Eliminar el factory method estático
4. El TODO en `FirestoreQueryExecutor.Create()` se resuelve
