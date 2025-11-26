# Patrones del Provider de Cosmos DB - Lecciones Clave

## Análisis del Código Fuente de EF Core Cosmos DB

Basado en el análisis del código fuente del provider de Cosmos DB de EF Core, estos son los patrones críticos que debes seguir.

## 1. Estructura de la Clase QueryingEnumerable

### Patrón Cosmos DB

```csharp
private sealed class QueryingEnumerable<T> : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
{
    // Estado compartido
    private readonly CosmosQueryContext _cosmosQueryContext;
    private readonly ISqlExpressionFactory _sqlExpressionFactory;
    private readonly SelectExpression _selectExpression;
    private readonly Func<CosmosQueryContext, JToken, T> _shaper;
    private readonly IQuerySqlGeneratorFactory _querySqlGeneratorFactory;
    // ... más campos ...

    public QueryingEnumerable(
        CosmosQueryContext cosmosQueryContext,
        ISqlExpressionFactory sqlExpressionFactory,
        IQuerySqlGeneratorFactory querySqlGeneratorFactory,
        SelectExpression selectExpression,
        Func<CosmosQueryContext, JToken, T> shaper,
        Type contextType,
        IEntityType rootEntityType,
        List<Expression> partitionKeyPropertyValues,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
    {
        // Inicialización
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator(this, cancellationToken);

    public IEnumerator<T> GetEnumerator()
        => throw new InvalidOperationException(CosmosStrings.SyncNotSupported);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    // Clase anidada para el enumerador
    private sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        // Implementación del enumerador asíncrono
    }
}
```

### Aplicación a Firestore

```csharp
internal sealed class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
    where T : class, new()
{
    private readonly QueryContext _queryContext;
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
    private readonly Type _contextType;

    public FirestoreQueryingEnumerable(
        QueryContext queryContext,
        FirestoreQueryExpression queryExpression,
        Func<QueryContext, DocumentSnapshot, T> shaper,
        Type contextType)
    {
        _queryContext = queryContext;
        _queryExpression = queryExpression;
        _shaper = shaper;
        _contextType = contextType;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator(this, cancellationToken);

    public IEnumerator<T> GetEnumerator()
        => throw new NotSupportedException(
            "Firestore queries must be executed asynchronously. Use ToListAsync() instead of ToList().");

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        // ... implementación ...
    }
}
```

**Puntos clave:**
- `sealed class` para evitar herencia
- Implementa `IAsyncEnumerable<T>` y `IEnumerable<T>`
- `GetEnumerator()` sincrónico lanza excepción (solo async soportado)
- Clase `AsyncEnumerator` anidada

## 2. Implementación del AsyncEnumerator

### Patrón Cosmos DB

```csharp
private sealed class AsyncEnumerator : IAsyncEnumerator<T>
{
    private readonly QueryingEnumerable<T> _queryingEnumerable;
    private readonly CosmosQueryContext _cosmosQueryContext;
    private readonly Func<CosmosQueryContext, JToken, T> _shaper;
    // ... más campos ...

    private IAsyncEnumerator<JToken> _enumerator;  // ← Enumerador del stream de datos

    public T Current { get; private set; }

    public async ValueTask<bool> MoveNextAsync()
    {
        try
        {
            using var _ = _concurrencyDetector?.EnterCriticalSection();

            // Primera llamada: ejecutar query
            if (_enumerator == null)
            {
                var sqlQuery = _queryingEnumerable.GenerateQuery();

                EntityFrameworkMetricsData.ReportQueryExecuting();

                _enumerator = _cosmosQueryContext.CosmosClient
                    .ExecuteSqlQueryAsync(_cosmosContainer, _cosmosPartitionKey, sqlQuery, ...)
                    .GetAsyncEnumerator(_cancellationToken);

                _cosmosQueryContext.InitializeStateManager(_standAloneStateManager);
            }

            // Iterar sobre resultados
            var hasNext = await _enumerator.MoveNextAsync().ConfigureAwait(false);

            // Materializar con el shaper
            Current = hasNext
                ? _shaper(_cosmosQueryContext, _enumerator.Current)
                : default;

            return hasNext;
        }
        catch (Exception exception)
        {
            // Manejo de excepciones
            if (_exceptionDetector.IsCancellation(exception, _cancellationToken))
            {
                _queryLogger.QueryCanceled(_contextType);
            }
            else
            {
                _queryLogger.QueryIterationFailed(_contextType, exception);
            }
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        var enumerator = _enumerator;
        if (enumerator != null)
        {
            _enumerator = null;
            return enumerator.DisposeAsync();
        }
        return default;
    }
}
```

### Aplicación a Firestore

```csharp
private sealed class AsyncEnumerator : IAsyncEnumerator<T>
{
    private readonly QueryContext _queryContext;
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
    private readonly CancellationToken _cancellationToken;

    private IEnumerator<DocumentSnapshot> _documentsEnumerator;  // ← Similar al patrón
    private bool _isExecuted;

    public T Current { get; private set; }

    public async ValueTask<bool> MoveNextAsync()
    {
        try
        {
            // Primera llamada: ejecutar query (IGUAL que Cosmos)
            if (!_isExecuted)
            {
                await ExecuteQueryAsync();
                _isExecuted = true;
            }

            // Iterar sobre documentos (IGUAL que Cosmos)
            if (_documentsEnumerator == null || !_documentsEnumerator.MoveNext())
            {
                Current = default;
                return false;
            }

            // Materializar con el shaper (IGUAL que Cosmos)
            var document = _documentsEnumerator.Current;
            Current = _shaper(_queryContext, document);
            return true;
        }
        catch (Exception ex)
        {
            // Logging similar a Cosmos
            var logger = GetLogger();
            logger?.LogError(ex, "Error during Firestore query enumeration");
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _documentsEnumerator?.Dispose();
        _documentsEnumerator = null;
        return default;
    }
}
```

**Puntos clave:**
- **Lazy execution:** La query NO se ejecuta hasta la primera llamada a `MoveNextAsync()`
- **Stream pattern:** Cosmos usa `IAsyncEnumerator<JToken>`, Firestore puede usar `IEnumerator<DocumentSnapshot>` (ya que Firestore retorna todo el snapshot)
- **Shaper invocado por documento:** No en batch, sino uno por uno
- **Exception handling:** Similar al patrón de Cosmos

## 3. Implementación de VisitShapedQuery

### Patrón Cosmos DB

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // 1. Validación
    if (shapedQueryExpression.QueryExpression is not SelectExpression selectExpression)
    {
        throw new UnreachableException();
    }

    var rootEntityType = selectExpression.Container.EntityType;
    if (rootEntityType == null)
    {
        throw new UnreachableException();
    }

    // 2. Preparar parámetros
    var jTokenParameter = Expression.Parameter(typeof(JToken), "jToken");

    // 3. Detectar paginación
    var isPagingQuery = shapedQueryExpression.ShaperExpression is PagingExpression;

    // 4. Transformar el shaper
    var shaperBody = shapedQueryExpression.ShaperExpression;

    // Inyectar JObject
    shaperBody = new JObjectInjectingExpressionVisitor().Visit(shaperBody);

    // Aplicar materializers
    shaperBody = InjectEntityMaterializers(shaperBody);

    // Remover ProjectionBindings
    shaperBody = new CosmosProjectionBindingRemovingExpressionVisitor(...).Visit(shaperBody);

    // 5. Crear el shaper lambda
    var shaperLambda = Expression.Lambda(
        shaperBody,
        QueryCompilationContext.QueryContextParameter,
        jTokenParameter);

    // 6. CLAVE: Expression.New para crear el enumerable
    return Expression.New(
        typeof(QueryingEnumerable<>).MakeGenericType(elementType).GetConstructors()[0],
        cosmosQueryContextConstant,
        sqlExpressionFactory,
        querySqlGeneratorFactory,
        selectExpression,
        Expression.Constant(shaperLambda.Compile()),  // ← Compilar el shaper!
        contextType,
        rootEntityType,
        partitionKeyValues,
        standAloneStateManager,
        threadSafetyChecksEnabled);
}
```

### Aplicación a Firestore (Simplificada)

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // 1. Obtener FirestoreQueryExpression
    var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

    // 2. Obtener tipo de entidad
    var entityType = firestoreQueryExpression.EntityType.ClrType;

    // 3. Crear el shaper
    var shaperLambda = CreateShaper(shapedQueryExpression, entityType);

    // 4. Compilar el shaper (IGUAL que Cosmos)
    var compiledShaper = Expression.Constant(shaperLambda.Compile());

    // 5. Crear constantes
    var queryExpressionConstant = Expression.Constant(firestoreQueryExpression);
    var contextTypeConstant = Expression.Constant(QueryCompilationContext.ContextType);

    // 6. CLAVE: Expression.New (IGUAL que Cosmos)
    var queryingEnumerableType = typeof(FirestoreQueryingEnumerable<>)
        .MakeGenericType(entityType);

    var constructor = queryingEnumerableType.GetConstructors(
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance)[0];

    return Expression.New(
        constructor,
        QueryCompilationContext.QueryContextParameter,
        queryExpressionConstant,
        compiledShaper,  // ← Ya compilado!
        contextTypeConstant);
}
```

**Puntos clave:**
- **Siempre usar Expression.New:** NUNCA retornar un Lambda
- **Compilar el shaper:** `.Compile()` antes de pasarlo como argumento
- **Usar MakeGenericType:** Para construir el tipo genérico correcto
- **Pasar QueryContextParameter:** No crear un nuevo parámetro

## 4. Signature del Shaper

### Cosmos DB

```csharp
Func<CosmosQueryContext, JToken, T> _shaper;
```

**Parámetros:**
1. `CosmosQueryContext`: Contexto específico del provider (derivado de `QueryContext`)
2. `JToken`: Documento JSON de Cosmos DB
3. `T`: Entidad materializada

### Firestore

```csharp
Func<QueryContext, DocumentSnapshot, T> _shaper;
```

**Parámetros:**
1. `QueryContext`: Contexto genérico de EF Core
2. `DocumentSnapshot`: Documento de Firestore
3. `T`: Entidad materializada

**Nota:** Firestore usa `QueryContext` en lugar de un contexto custom como `FirestoreQueryContext`. Esto es válido y más simple.

## 5. Generación de Query SQL

### Patrón Cosmos DB

```csharp
private CosmosSqlQuery GenerateQuery()
    => _querySqlGeneratorFactory.Create().GetSqlQuery(
        (SelectExpression)new ParameterInliner(
            _sqlExpressionFactory,
            _cosmosQueryContext.Parameters)
        .Visit(_selectExpression),
        _cosmosQueryContext.Parameters);
```

**Puntos clave:**
- La generación de SQL ocurre DENTRO del QueryingEnumerable
- Se usa un `ParameterInliner` para sustituir parámetros
- Se pasan los parámetros del `QueryContext`

### Aplicación a Firestore

```csharp
// En FirestoreQueryExecutor (más simple)
private Google.Cloud.Firestore.Query BuildFirestoreQuery(
    FirestoreQueryExpression queryExpression)
{
    Google.Cloud.Firestore.Query query =
        _client.GetCollection(queryExpression.CollectionName);

    // Aplicar filtros
    foreach (var filter in queryExpression.Filters)
    {
        query = ApplyWhereClause(query, filter);
    }

    // Aplicar ordenamiento
    foreach (var orderBy in queryExpression.OrderByClauses)
    {
        query = orderBy.Descending
            ? query.OrderByDescending(orderBy.PropertyName)
            : query.OrderBy(orderBy.PropertyName);
    }

    // Aplicar límite
    if (queryExpression.Limit.HasValue)
    {
        query = query.Limit(queryExpression.Limit.Value);
    }

    return query;
}
```

**Nota:** Firestore no tiene un "SQL generator" porque usa la API fluent del SDK. Más simple que Cosmos.

## 6. Manejo de Parámetros

### Cosmos DB

```csharp
// Los parámetros se pasan como SqlParameter[]
var sqlQuery = new CosmosSqlQuery(
    "SELECT * FROM c WHERE c.Precio > @p0",
    new[] { new SqlParameter("@p0", 100) });
```

Cosmos necesita parametrizar las queries para prevenir injection y mejorar caching.

### Firestore

```csharp
// Firestore usa valores directos en la API
var query = db.Collection("productos")
    .WhereGreaterThan("Precio", 100);
```

Firestore no requiere parametrización explícita, el SDK lo maneja internamente.

## 7. Manejo de Estado (StateManager)

### Cosmos DB

```csharp
_cosmosQueryContext.InitializeStateManager(_standAloneStateManager);
```

Cosmos inicializa el StateManager para tracking de entidades. Esto ocurre en la primera llamada a `MoveNextAsync()`.

### Firestore

Para Firestore, el tracking lo maneja EF Core automáticamente, pero puedes seguir el mismo patrón si necesitas un StateManager custom.

## 8. Concurrency y Thread Safety

### Cosmos DB

```csharp
using var _ = _concurrencyDetector?.EnterCriticalSection();
```

Cosmos usa un `ConcurrencyDetector` para detectar acceso concurrente al DbContext. Esto previene bugs sutiles.

### Firestore

Puedes implementar el mismo patrón si tu implementación lo requiere:

```csharp
// Opcional: agregar al constructor del AsyncEnumerator
_concurrencyDetector = queryingEnumerable._threadSafetyChecksEnabled
    ? _queryContext.ConcurrencyDetector
    : null;

// En MoveNextAsync:
using var _ = _concurrencyDetector?.EnterCriticalSection();
```

## 9. Métricas y Diagnósticos

### Cosmos DB

```csharp
EntityFrameworkMetricsData.ReportQueryExecuting();
```

Cosmos reporta métricas antes de ejecutar la query. Esto alimenta el sistema de diagnósticos de EF Core.

### Firestore

Puedes agregar el mismo patrón:

```csharp
// Antes de ejecutar la query
EntityFrameworkMetricsData.ReportQueryExecuting();
```

## 10. Manejo de Excepciones de Cancelación

### Cosmos DB

```csharp
catch (Exception exception)
{
    if (_exceptionDetector.IsCancellation(exception, _cancellationToken))
    {
        _queryLogger.QueryCanceled(_contextType);
    }
    else
    {
        _queryLogger.QueryIterationFailed(_contextType, exception);
    }
    throw;
}
```

Cosmos distingue entre cancelación y otros errores. Esto mejora el logging.

### Firestore

```csharp
catch (Exception ex)
{
    // Opcional: Detectar si es cancelación
    if (ex is OperationCanceledException)
    {
        logger?.LogInformation("Firestore query canceled for {EntityType}", _contextType.Name);
    }
    else
    {
        logger?.LogError(ex, "Firestore query failed for {EntityType}", _contextType.Name);
    }
    throw;
}
```

## Resumen de Diferencias Clave: Cosmos vs Firestore

| Aspecto | Cosmos DB | Firestore |
|---------|-----------|-----------|
| **Query Language** | SQL (generado desde SelectExpression) | API Fluent del SDK |
| **Parámetros** | Requiere SqlParameter[] | Valores directos en la API |
| **Streaming** | IAsyncEnumerator<JToken> (streaming) | IEnumerator<DocumentSnapshot> (snapshot completo) |
| **Context Type** | CosmosQueryContext (custom) | QueryContext (genérico) |
| **Partition Key** | Esencial, múltiples formas de especificar | No aplica |
| **Paging** | ToPageAsync() con continuation tokens | StartAfter() con cursores |
| **ReadItem** | Optimización para Get by ID + Partition Key | Optimización con GetDocumentAsync() |

## Patrón Final Recomendado para Firestore

```csharp
// 1. Clase QueryingEnumerable (sealed, implementa IAsyncEnumerable)
internal sealed class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
{
    // Constructor simple con: QueryContext, FirestoreQueryExpression, Shaper, ContextType

    public IAsyncEnumerator<T> GetAsyncEnumerator(...)
        => new AsyncEnumerator(this, cancellationToken);

    public IEnumerator<T> GetEnumerator()
        => throw new NotSupportedException("Use async");

    // Clase AsyncEnumerator anidada
    private sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        public async ValueTask<bool> MoveNextAsync()
        {
            // Lazy execution en primera llamada
            // Iterar sobre snapshot.Documents
            // Aplicar shaper por documento
        }
    }
}

// 2. VisitShapedQuery
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // Crear shaper lambda
    var shaperLambda = CreateShaper(...);

    // CLAVE: Expression.New + .Compile()
    return Expression.New(
        typeof(FirestoreQueryingEnumerable<T>).GetConstructor(...),
        QueryCompilationContext.QueryContextParameter,
        Expression.Constant(firestoreQueryExpression),
        Expression.Constant(shaperLambda.Compile()),  // ← Compilar!
        Expression.Constant(QueryCompilationContext.ContextType));
}
```

## Conclusión

El patrón del provider de Cosmos DB es la referencia gold standard para implementar providers de EF Core con queries asíncronas. Los puntos más críticos son:

1. **Usar Expression.New, NO Expression.Lambda**
2. **Compilar el shaper antes de pasarlo**
3. **Lazy execution en la primera llamada a MoveNextAsync()**
4. **Aplicar el shaper documento por documento**
5. **GetEnumerator() sincrónico lanza excepción**

Siguiendo estos patrones, tu implementación de Firestore será robusta y compatible con el pipeline de EF Core.
