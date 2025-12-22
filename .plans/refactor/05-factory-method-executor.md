# 05 - Factory Method para FirestoreQueryExecutor

## Objetivo

Centralizar la creación de `FirestoreQueryExecutor` en un factory method estático para preparar la inyección de dependencias futura.

---

## Estado Actual

### Violaciones (4 lugares con `new FirestoreQueryExecutor`)

| Archivo | Línea | Contexto |
|---------|-------|----------|
| `FirestoreQueryingEnumerable.cs` | 105 | `SyncEnumerator.InitializeEnumerator()` |
| `FirestoreQueryingEnumerable.cs` | 201 | `AsyncEnumerator.InitializeEnumeratorAsync()` |
| `FirestoreAggregationQueryingEnumerable.cs` | 79 | `SyncEnumerator.ExecuteAggregation()` |
| `FirestoreAggregationQueryingEnumerable.cs` | 127 | `AsyncEnumerator.ExecuteAggregationAsync()` |

### Contrato Incompleto

`IFirestoreQueryExecutor` no tiene:
- `ExecuteAggregationAsync<T>`
- `BuildQuery`
- `EvaluateIntExpression`

---

## Plan de Ejecución

### Paso 1: Agregar método Create en IFirestoreQueryExecutor

```csharp
// En IFirestoreQueryExecutor.cs agregar:
Task<T> ExecuteAggregationAsync<T>(
    FirestoreQueryExpression queryExpression,
    QueryContext queryContext,
    CancellationToken cancellationToken = default);
```

### Paso 2: Crear Factory Method en FirestoreQueryExecutor

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

    // ... resto igual
}
```

### Paso 3: Modificar FirestoreQueryingEnumerable

Agregar campo de clase y recibir executor en constructor:

```csharp
public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
{
    private readonly QueryContext _queryContext;
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Func<QueryContext, DocumentSnapshot, bool, T> _shaper;
    private readonly Type _contextType;
    private readonly bool _isTracking;
    private readonly IFirestoreQueryExecutor _executor;  // NUEVO

    public FirestoreQueryingEnumerable(
        QueryContext queryContext,
        FirestoreQueryExpression queryExpression,
        Func<QueryContext, DocumentSnapshot, bool, T> shaper,
        Type contextType,
        bool isTracking,
        IFirestoreQueryExecutor executor)  // NUEVO
    {
        // ... validaciones existentes
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }
```

Eliminar Service Locator y `new` en los Enumerators - usar `_enumerable._executor`:

```csharp
// ANTES (en InitializeEnumerator / InitializeEnumeratorAsync):
var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;
var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory))!;
var executorLogger = loggerFactory.CreateLogger<FirestoreQueryExecutor>();
var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

// DESPUÉS:
var executor = _enumerable._executor;
```

### Paso 4: Modificar FirestoreAggregationQueryingEnumerable

Mismo patrón:

```csharp
public class FirestoreAggregationQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
{
    private readonly QueryContext _queryContext;
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Type _contextType;
    private readonly IFirestoreQueryExecutor _executor;  // NUEVO

    public FirestoreAggregationQueryingEnumerable(
        QueryContext queryContext,
        FirestoreQueryExpression queryExpression,
        Type contextType,
        IFirestoreQueryExecutor executor)  // NUEVO
    {
        // ... validaciones existentes
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }
```

### Paso 5: Modificar FirestoreShapedQueryCompilingExpressionVisitor

Los Enumerables se crean vía Expression Trees en el Visitor:

| Método | Línea | Crea |
|--------|-------|------|
| `CreateQueryingEnumerable` | 129 | `FirestoreQueryingEnumerable<T>` |
| `CreateAggregationQueryExpression` | 158 | `FirestoreAggregationQueryingEnumerable<T>` |
| `CreateProjectionQueryExpression` | 207 | `FirestoreQueryingEnumerable<T>` |
| `CreateSubcollectionProjectionQueryExpression` | 260 | `FirestoreQueryingEnumerable<T>` |

**Cambios necesarios:**

1. El Visitor debe tener campo `_queryExecutor` (recibido por constructor)
2. Agregar `IFirestoreQueryExecutor` como parámetro en `Expression.New()` de cada método
3. Actualizar `GetConstructor()` para incluir el tipo adicional

```csharp
// ANTES (línea 130-137):
var constructor = enumerableType.GetConstructor(new[]
{
    typeof(QueryContext),
    typeof(FirestoreQueryExpression),
    typeof(Func<,,,>).MakeGenericType(...),
    typeof(Type),
    typeof(bool)
})!;

// DESPUÉS:
var constructor = enumerableType.GetConstructor(new[]
{
    typeof(QueryContext),
    typeof(FirestoreQueryExpression),
    typeof(Func<,,,>).MakeGenericType(...),
    typeof(Type),
    typeof(bool),
    typeof(IFirestoreQueryExecutor)  // NUEVO
})!;

// Y en Expression.New agregar:
Expression.Constant(_queryExecutor)  // NUEVO
```

### Paso 6: Modificar FirestoreShapedQueryCompilingExpressionVisitorFactory

El Factory debe crear el executor y pasarlo al Visitor:

```csharp
public class FirestoreShapedQueryCompilingExpressionVisitorFactory
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
    private readonly IFirestoreClientWrapper _clientWrapper;  // NUEVO
    private readonly ILoggerFactory _loggerFactory;           // NUEVO

    public FirestoreShapedQueryCompilingExpressionVisitorFactory(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        IFirestoreClientWrapper clientWrapper,              // NUEVO
        ILoggerFactory loggerFactory)                       // NUEVO
    {
        _dependencies = dependencies;
        _clientWrapper = clientWrapper;
        _loggerFactory = loggerFactory;
    }

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext ctx)
    {
        // Crear executor usando factory method
        var executor = FirestoreQueryExecutor.Create(
            _clientWrapper,
            _loggerFactory.CreateLogger<FirestoreQueryExecutor>());

        return new FirestoreShapedQueryCompilingExpressionVisitor(
            _dependencies,
            ctx,
            executor);  // NUEVO
    }
}
```

---

## Orden de Cambios

1. `IFirestoreQueryExecutor.cs` - Agregar `ExecuteAggregationAsync<T>`
2. `FirestoreQueryExecutor.cs` - Agregar `Create()`, cambiar constructor a `protected`
3. `FirestoreQueryingEnumerable.cs` - Agregar campo `_executor`, modificar constructor
4. `FirestoreAggregationQueryingEnumerable.cs` - Agregar campo `_executor`, modificar constructor
5. `FirestoreShapedQueryCompilingExpressionVisitor.cs` - Agregar campo `_queryExecutor`, modificar constructor, actualizar 4 métodos que crean Enumerables
6. `FirestoreShapedQueryCompilingExpressionVisitorFactory.cs` - Inyectar dependencias, crear executor con `Create()`, pasarlo al Visitor
7. Eliminar Service Locator de los Enumerators (ya no necesitan resolver nada)
8. Tests unitarios - Actualizar los que usen constructor directamente

---

## Verificación

```bash
dotnet test
# Esperado: 766 tests pasando
# Posibles fallos: Tests unitarios que usen constructor directamente
```

---

## Resultado Esperado

| Métrica | Antes | Después |
|---------|-------|---------|
| `new FirestoreQueryExecutor()` en producción | 4 | 0 |
| `new FirestoreQueryExecutor()` en tests | 3 | 3 (permitido) |
| Service Locator en Enumerables | 8 | 0 |
| Factory method centralizado | 0 | 1 |

---

## Siguiente Paso (Futuro)

Cuando el factory method esté funcionando:

1. Registrar `IFirestoreQueryExecutor` en DI
2. Inyectar en el Visitor vía Factory
3. Eliminar el factory method estático
4. El TODO se resuelve
