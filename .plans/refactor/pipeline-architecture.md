# Pipeline Architecture: Query Execution Refactor

## Resumen Ejecutivo

Este documento describe la nueva arquitectura basada en **Pipeline Pattern** para la ejecución de queries en el provider de Firestore para EF Core. Reemplaza el monolítico `FirestoreQueryExecutor` (2187 líneas) por una cadena de handlers con responsabilidad única.

---

## Problemas del Diseño Actual

### FirestoreQueryExecutor viola SRP

| Responsabilidad | Líneas aprox. |
|-----------------|---------------|
| Construir queries | ~400 |
| Ejecutar queries | ~100 |
| Evaluar expresiones | ~100 |
| Tracking | ~80 |
| Cargar Includes | ~400 |
| Agregaciones | ~200 |
| Código legacy | ~200 |

### FirestoreDocumentDeserializer viola DI

```csharp
// Anti-pattern: Service Locator + Reflexión
var proxiesAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.Proxies");

var proxyFactory = serviceProvider.GetService(proxyFactoryType);
```

### Dependencias incorrectas

| Lo que usábamos | Lo que realmente necesitamos |
|-----------------|------------------------------|
| `dbContext.Model` | `IModel` (inyectado) |
| `dbContext.GetService<IStateManager>()` | `IStateManager` (inyectado) |
| `dbContext.Attach(entity)` | `IStateManager.StartTrackingFromQuery()` |
| `IServiceProvider.GetService()` | `IProxyFactory?` (inyectado, nullable) |

---

## Nueva Arquitectura

### Flujo General

```
┌─────────────────────┐
│  Enumerable<T>      │
│                     │
│  - Crea el          │
│    PipelineContext  │
└──────────┬──────────┘
           │
           │ PipelineContext
           ▼
┌─────────────────────┐
│     Mediator        │
│                     │
│  - Ejecuta cadena   │
│    de handlers      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                      PIPELINE                                            │
│                                                                                          │
│  ┌──────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌──────┐│
│  │  Error   │→│  Log   │→│ Cache  │→│Resolver│→│Execute │→│Convert │→│Tracking│→│ Proxy││
│  │ Handling │ │ (AST)  │ │Handler │ │Handler │ │Handler │ │Handler │ │Handler │ │Handler│
│  └──────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └──────┘│
│                                                                               ┌────────┐ │
│                                                                               │Include │ │
│                                                                               │Handler │ │
│                                                                               └────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────────┘
           │
           ▼
┌─────────────────────┐
│ IAsyncEnumerable<T> │
└─────────────────────┘
```

---

## PipelineContext

```csharp
public enum QueryKind
{
    /// <summary>
    /// Query que retorna entidades completas: ToList(), First(), Single()
    /// </summary>
    Entity,

    /// <summary>
    /// Query de agregación: Count(), Sum(), Average(), Min(), Max()
    /// </summary>
    Aggregation,

    /// <summary>
    /// Query con proyección: Select(x => new { x.Name })
    /// </summary>
    Projection,

    /// <summary>
    /// Query que retorna bool: Any(), All(), Contains()
    /// </summary>
    Predicate
}

public record PipelineContext
{
    public required FirestoreQueryExpression Ast { get; init; }
    public required IFirestoreQueryContext QueryContext { get; init; }
    public required bool IsTracking { get; init; }
    public required Type ResultType { get; init; }
    public required QueryKind Kind { get; init; }

    /// <summary>
    /// Tipo de entidad raíz (null para proyecciones anónimas)
    /// </summary>
    public Type? EntityType { get; init; }

    /// <summary>
    /// Metadata compartida entre handlers
    /// </summary>
    public ImmutableDictionary<string, object> Metadata { get; init; }
        = ImmutableDictionary<string, object>.Empty;

    /// <summary>
    /// Query resuelta (populated por ResolverHandler)
    /// </summary>
    public ResolvedFirestoreQuery? ResolvedQuery { get; init; }
}
```

**Nota:** `QueryContext.Context` contiene el `DbContext`, pero los handlers no lo usan directamente. Inyectan `IStateManager`, `IModel`, etc.

---

## PipelineResult

```csharp
public abstract record PipelineResult
{
    public required PipelineContext Context { get; init; }

    /// <summary>
    /// Resultado streaming - permite procesar entidad por entidad
    /// </summary>
    public sealed record Streaming(
        IAsyncEnumerable<object> Items,
        PipelineContext Context
    ) : PipelineResult;

    /// <summary>
    /// Resultado materializado - colección completa en memoria
    /// </summary>
    public sealed record Materialized(
        IReadOnlyList<object> Items,
        PipelineContext Context
    ) : PipelineResult;

    /// <summary>
    /// Resultado escalar - agregaciones, Count, Any, etc.
    /// </summary>
    public sealed record Scalar(
        object Value,
        PipelineContext Context
    ) : PipelineResult;

    /// <summary>
    /// Resultado vacío - query sin resultados
    /// </summary>
    public sealed record Empty(
        PipelineContext Context
    ) : PipelineResult;
}
```

### Conversión entre tipos

```csharp
public static class PipelineResultExtensions
{
    public static async Task<PipelineResult.Materialized> MaterializeAsync(
        this PipelineResult.Streaming streaming,
        CancellationToken ct)
    {
        var items = await streaming.Items.ToListAsync(ct);
        return new PipelineResult.Materialized(items, streaming.Context);
    }

    public static PipelineResult.Streaming ToStreaming(
        this PipelineResult.Materialized materialized)
    {
        return new PipelineResult.Streaming(
            materialized.Items.ToAsyncEnumerable(),
            materialized.Context);
    }
}
```

---

## Handler Interface

```csharp
/// <summary>
/// Delegate que representa el siguiente handler en el pipeline
/// </summary>
public delegate Task<PipelineResult> PipelineDelegate(
    PipelineContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Handler del pipeline
/// </summary>
public interface IQueryPipelineHandler
{
    Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken);
}
```

### Handler base con skip automático

```csharp
public abstract class QueryPipelineHandlerBase : IQueryPipelineHandler
{
    /// <summary>
    /// Tipos de query que este handler procesa
    /// </summary>
    protected abstract QueryKind[] ApplicableKinds { get; }

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        if (!ApplicableKinds.Contains(context.Kind))
        {
            return await next(context, ct);
        }

        return await HandleCoreAsync(context, next, ct);
    }

    protected abstract Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct);
}
```

---

## Metadata Tipada

```csharp
public static class PipelineMetadataKeys
{
    public static readonly MetadataKey<bool> RequiresLazyLoader = new("RequiresLazyLoader");
    public static readonly MetadataKey<string> CacheKey = new("CacheKey");
    public static readonly MetadataKey<HashSet<object>> TrackedEntities = new("TrackedEntities");
    public static readonly MetadataKey<TimeSpan> ExecutionTime = new("ExecutionTime");
}

public readonly record struct MetadataKey<T>(string Name);

public static class PipelineContextExtensions
{
    public static PipelineContext WithMetadata<T>(
        this PipelineContext context,
        MetadataKey<T> key,
        T value)
    {
        return context with
        {
            Metadata = context.Metadata.SetItem(key.Name, value!)
        };
    }

    public static T? GetMetadata<T>(this PipelineContext context, MetadataKey<T> key)
    {
        return context.Metadata.TryGetValue(key.Name, out var value)
            ? (T)value
            : default;
    }
}
```

---

## Handlers

### Tabla de Handlers

| # | Handler | Responsabilidad | Dependencia (DI) | Aplica a |
|---|---------|-----------------|------------------|----------|
| 1 | `ErrorHandlingHandler` | Retries, logging de errores | `ILogger`, `Options` | Todos |
| 2 | `LogAstHandler` | Log del AST entrante | `ILogger` | Todos |
| 3 | `CacheHandler` | Buscar en cache | `ICache?` | Todos |
| 4 | `ResolverHandler` | AST → ResolvedQuery | `IFirestoreAstResolver` | Todos |
| 5 | `ExecutionHandler` | Ejecutar query | `IFirestoreClientWrapper`, `IQueryBuilder` | Todos |
| 6 | `ConvertHandler` | Convertir a tipo CLR | `IDocumentDeserializer`, `ITypeConverter` | Todos |
| 7 | `TrackingHandler` | Tracking | `IStateManager` | Entity |
| 8 | `ProxyHandler` | Crear proxy | `IProxyFactory?` | Entity |
| 9 | `IncludeHandler` | Cargar navegaciones | `IIncludeLoader` | Entity |

### Handlers y su modo de operación

| Handler | Input esperado | Output | Materializa? |
|---------|---------------|--------|--------------|
| ErrorHandlingHandler | Any | Pass-through | No |
| LogAstHandler | Any | Pass-through | No |
| CacheHandler | Any | Materialized (si hit) | Sí (para cachear) |
| ResolverHandler | Any | Pass-through | No |
| ExecutionHandler | - | Streaming | No |
| ConvertHandler | Streaming / Scalar | Streaming / Scalar | No |
| TrackingHandler | Streaming | Streaming | No |
| ProxyHandler | Streaming | Streaming | No |
| IncludeHandler | Streaming | Streaming | No (pero sub-queries sí) |

### Orden correcto: Tracking → Proxy

```
ExecutionHandler
    ↓
ConvertHandler (materializa entidad base)
    ↓
TrackingHandler (trackea la entidad base)
    ↓
ProxyHandler (envuelve en proxy, inyecta ILazyLoader)
    ↓
IncludeHandler (carga navegaciones)
```

**Justificación:**
1. **Tracking primero**: `IStateManager.StartTrackingFromQuery` necesita la entidad real, no el proxy
2. **Proxy después**: El proxy envuelve la entidad ya trackeada
3. **Include al final**: Las entidades relacionadas pasan por su propio sub-pipeline

---

## Detalle de Handlers

### 1. ErrorHandlingHandler

```csharp
public class ErrorHandlingHandler : IQueryPipelineHandler
{
    private readonly ILogger<ErrorHandlingHandler> _logger;
    private readonly FirestoreErrorHandlingOptions _options;

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await next(context, ct);
            }
            catch (FirestoreQueryExecutionException ex) when (ex.IsTransient && attempt < _options.MaxRetries)
            {
                attempt++;
                _logger.LogWarning(ex,
                    "Transient error on attempt {Attempt}/{MaxRetries}. Retrying...",
                    attempt, _options.MaxRetries);

                await Task.Delay(_options.GetDelay(attempt), ct);
            }
        }
    }
}
```

### 2. ResolverHandler

```csharp
public class ResolverHandler : IQueryPipelineHandler
{
    private readonly IFirestoreAstResolver _resolver;

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var resolved = _resolver.Resolve(context.Ast);

        var newContext = context with { ResolvedQuery = resolved };
        return await next(newContext, ct);
    }
}
```

### 3. ExecutionHandler

```csharp
public class ExecutionHandler : IQueryPipelineHandler
{
    private readonly IFirestoreClientWrapper _client;
    private readonly IQueryBuilder _queryBuilder;

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var resolved = context.ResolvedQuery!;

        // Document query
        if (resolved.DocumentId != null)
        {
            var doc = await _client.GetDocumentAsync(resolved.CollectionPath, resolved.DocumentId, ct);
            var items = doc.Exists ? AsyncEnumerable.Create(_ => doc) : AsyncEnumerable.Empty<DocumentSnapshot>();
            return await next(context, ct) with result...;
        }

        // Aggregation query
        if (resolved.IsAggregation)
        {
            var query = _queryBuilder.Build(resolved);
            var value = await ExecuteAggregation(query, resolved, ct);
            return new PipelineResult.Scalar(value, context);
        }

        // Collection query
        var sdkQuery = _queryBuilder.Build(resolved);
        var snapshots = _client.ExecuteQueryAsync(sdkQuery, ct);
        return new PipelineResult.Streaming(snapshots, context);
    }
}
```

### 4. ConvertHandler

```csharp
public class ConvertHandler : IQueryPipelineHandler
{
    private readonly IDocumentDeserializer _deserializer;
    private readonly ITypeConverter _typeConverter;

    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var result = await next(context, ct);

        // Aggregation scalar - convert types
        if (result is PipelineResult.Scalar scalar)
        {
            var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
            return new PipelineResult.Scalar(converted, context);
        }

        // Documents - deserialize
        if (result is PipelineResult.Streaming streaming)
        {
            var entities = streaming.Items.Select(doc =>
                _deserializer.Deserialize((DocumentSnapshot)doc, context.EntityType!));
            return new PipelineResult.Streaming(entities, context);
        }

        return result;
    }
}
```

### 5. TrackingHandler

```csharp
public class TrackingHandler : QueryPipelineHandlerBase
{
    private readonly IStateManager _stateManager;

    protected override QueryKind[] ApplicableKinds => [QueryKind.Entity];

    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var result = await next(context, ct);

        if (!context.IsTracking || result is not PipelineResult.Streaming streaming)
        {
            return result;
        }

        var tracked = TrackEntities(streaming.Items, context);
        return new PipelineResult.Streaming(tracked, result.Context);
    }

    private async IAsyncEnumerable<object> TrackEntities(
        IAsyncEnumerable<object> entities,
        PipelineContext context)
    {
        var entityType = context.QueryContext.Model.FindEntityType(context.EntityType!);

        await foreach (var entity in entities)
        {
            var entry = _stateManager.StartTrackingFromQuery(
                entityType!,
                entity,
                new ValueBuffer(/* snapshot de valores */));

            yield return entry.Entity;
        }
    }
}
```

### 6. ProxyHandler

```csharp
public class ProxyHandler : QueryPipelineHandlerBase
{
    private readonly IProxyFactory? _proxyFactory;
    private readonly ILazyLoader _lazyLoader;

    protected override QueryKind[] ApplicableKinds => [QueryKind.Entity];

    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken ct)
    {
        var result = await next(context, ct);

        if (_proxyFactory is null || result is not PipelineResult.Streaming streaming)
        {
            return result;
        }

        var proxied = CreateProxies(streaming.Items, context);
        return new PipelineResult.Streaming(proxied, result.Context);
    }

    private async IAsyncEnumerable<object> CreateProxies(
        IAsyncEnumerable<object> entities,
        PipelineContext context)
    {
        var entityType = context.QueryContext.Model.FindEntityType(context.EntityType!);

        await foreach (var entity in entities)
        {
            var proxy = _proxyFactory!.CreateLazyLoadingProxy(
                entityType!,
                entity,
                _lazyLoader);

            yield return proxy;
        }
    }
}
```

---

## Flujo de Datos

```
AST ──────────────────────▶ ResolvedQuery ──────────────────────▶ DocumentSnapshot[] ──────────────────────▶ Entity<T>[]
     │                           │                                      │                                        │
     │  ErrorHandler             │  ExecutionHandler                    │  ConvertHandler                        │
     │  LogAstHandler            │                                      │  TrackingHandler                       │
     │  CacheHandler             │                                      │  ProxyHandler                          │
     │  ResolverHandler          │                                      │  IncludeHandler                        │
     ▼                           ▼                                      ▼                                        ▼
  (metadata)               (query ejecutable)                    (datos crudos)                          (entidades listas)
```

---

## Error Handling

### Excepciones del Pipeline

```csharp
public abstract class FirestorePipelineException : Exception
{
    public PipelineContext Context { get; }

    protected FirestorePipelineException(
        string message,
        PipelineContext context,
        Exception? inner = null)
        : base(message, inner)
    {
        Context = context;
    }
}

public class FirestoreQueryExecutionException : FirestorePipelineException
{
    public string Collection { get; }
    public bool IsTransient { get; }
}

public class FirestoreDeserializationException : FirestorePipelineException
{
    public string DocumentId { get; }
    public Type TargetType { get; }
}
```

### Clasificación de errores transitorios

```csharp
public static class FirestoreExceptionClassifier
{
    public static bool IsTransient(RpcException ex)
    {
        return ex.StatusCode switch
        {
            StatusCode.Unavailable => true,
            StatusCode.DeadlineExceeded => true,
            StatusCode.ResourceExhausted => true,  // Rate limiting
            StatusCode.Aborted => true,            // Transaction conflict
            _ => false
        };
    }
}
```

---

## Piezas (Services)

| Interfaz | Implementación | Responsabilidad |
|----------|----------------|-----------------|
| `IQueryBuilder` | `FirestoreQueryBuilder` | ResolvedQuery → Query SDK |
| `IDocumentDeserializer` | `FirestoreDocumentDeserializer` | DocumentSnapshot → Entity |
| `ITypeConverter` | `FirestoreTypeConverter` | Conversión Firestore → CLR |
| `IProxyFactory?` | (de EF Core Proxies) | Entity → Proxy |
| `IEntityTracker` | `FirestoreEntityTracker` | Wrapper sobre IStateManager |
| `IIncludeLoader` | `FirestoreIncludeLoader` | Cargar navegaciones |
| `IFirestoreLazyLoader` | `FirestoreLazyLoader` | Lazy loading con pipeline |

**Nota:** Usamos `ResolvedFirestoreQuery` existente en `Query/Resolved/ResolvedTypes.cs`.

---

## Conversiones de Tipos (ConvertHandler)

### Agregaciones

| Agregación | Firestore retorna | CLR espera |
|------------|-------------------|------------|
| `Count()` | `long` | `int` |
| `Sum(x => x.Price)` | `double` | `decimal` |
| `Min(x => x.Date)` | `Timestamp` | `DateTime` |
| `Average(x => x.Rating)` | `double` | `decimal` / `double` |

### Propiedades de Entidades

| Tipo Firestore | Tipo CLR |
|----------------|----------|
| `double` | `decimal` |
| `Timestamp` | `DateTime` |
| `string` (enum) | `Enum` |
| `long` | `int` |
| `GeoPoint` | Custom complex type |

---

## Lazy Loading

### Problema

Cuando el usuario accede a `Menu.Categories`, el proxy debe:
1. Ejecutar query para cargar Categories
2. Trackear las entidades cargadas
3. Retornar la colección

### Solución: FirestoreLazyLoader

```csharp
public class FirestoreLazyLoader : ILazyLoader
{
    private readonly IQueryPipelineMediator _mediator;

    public async Task LoadAsync(object entity, string navigationName, CancellationToken ct)
    {
        // 1. Construir AST para la navegación
        // 2. Ejecutar pipeline completo
        // 3. Las entidades vienen trackeadas con sus propios proxies
        // 4. Asignar a la propiedad de navegación
    }
}
```

### Flujo de Lazy Loading

```
Menu.Categories (proxy intercepta)
    ↓
FirestoreLazyLoader.LoadAsync(menu, "Categories")
    ↓
Pipeline completo para Categories:
    Resolver → Execute → Convert → Tracking → Proxy → Include
    ↓
Categories[] (trackeadas, con sus propios proxies para Items)
```

---

## Mediator

```csharp
public interface IQueryPipelineMediator
{
    IAsyncEnumerable<T> ExecuteAsync<T>(PipelineContext context, CancellationToken ct);
}

public class QueryPipelineMediator : IQueryPipelineMediator
{
    private readonly IReadOnlyList<IQueryPipelineHandler> _handlers;

    public QueryPipelineMediator(IEnumerable<IQueryPipelineHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    public async IAsyncEnumerable<T> ExecuteAsync<T>(
        PipelineContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pipeline = BuildPipeline();
        var result = await pipeline(context, ct);

        await foreach (var item in UnwrapResult<T>(result, ct))
        {
            yield return item;
        }
    }

    private PipelineDelegate BuildPipeline()
    {
        PipelineDelegate pipeline = (ctx, ct) =>
            Task.FromResult<PipelineResult>(new PipelineResult.Empty(ctx));

        for (var i = _handlers.Count - 1; i >= 0; i--)
        {
            var handler = _handlers[i];
            var next = pipeline;

            pipeline = (ctx, ct) => handler.HandleAsync(ctx, next, ct);
        }

        return pipeline;
    }

    private async IAsyncEnumerable<T> UnwrapResult<T>(
        PipelineResult result,
        [EnumeratorCancellation] CancellationToken ct)
    {
        switch (result)
        {
            case PipelineResult.Streaming streaming:
                await foreach (var item in streaming.Items.WithCancellation(ct))
                    yield return (T)item;
                break;

            case PipelineResult.Materialized materialized:
                foreach (var item in materialized.Items)
                    yield return (T)item;
                break;

            case PipelineResult.Scalar scalar:
                yield return (T)scalar.Value;
                break;

            case PipelineResult.Empty:
                yield break;
        }
    }
}
```

---

## Enumerable Unificado

```csharp
public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
{
    private readonly IQueryPipelineMediator _mediator;
    private readonly FirestoreQueryExpression _ast;
    private readonly IFirestoreQueryContext _queryContext;
    private readonly bool _isTracking;
    private readonly QueryKind _kind;

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
    {
        var context = new PipelineContext
        {
            Ast = _ast,
            QueryContext = _queryContext,
            IsTracking = _isTracking,
            ResultType = typeof(T),
            Kind = _kind,
            EntityType = typeof(T)
        };

        await foreach (var entity in _mediator.ExecuteAsync<T>(context, ct))
        {
            yield return entity;
        }
    }
}
```

**Nota:** Se elimina `FirestoreAggregationQueryingEnumerable`. Un solo Enumerable maneja todos los casos.

---

## Registro en DI

```csharp
public static class FirestorePipelineServiceCollectionExtensions
{
    public static IServiceCollection AddFirestoreQueryPipeline(
        this IServiceCollection services,
        Action<FirestorePipelineOptions>? configure = null)
    {
        var options = new FirestorePipelineOptions();
        configure?.Invoke(options);

        // Mediator
        services.AddScoped<IQueryPipelineMediator, QueryPipelineMediator>();

        // Handlers (el orden de registro determina el orden de ejecución)
        services.AddScoped<IQueryPipelineHandler, ErrorHandlingHandler>();

        if (options.EnableAstLogging)
            services.AddScoped<IQueryPipelineHandler, LogAstHandler>();

        if (options.EnableCaching)
            services.AddScoped<IQueryPipelineHandler, CacheHandler>();

        services.AddScoped<IQueryPipelineHandler, ResolverHandler>();

        if (options.EnableQueryLogging)
            services.AddScoped<IQueryPipelineHandler, LogQueryHandler>();

        services.AddScoped<IQueryPipelineHandler, ExecutionHandler>();
        services.AddScoped<IQueryPipelineHandler, ConvertHandler>();
        services.AddScoped<IQueryPipelineHandler, TrackingHandler>();
        services.AddScoped<IQueryPipelineHandler, ProxyHandler>();
        services.AddScoped<IQueryPipelineHandler, IncludeHandler>();

        // Services
        services.AddScoped<IQueryBuilder, FirestoreQueryBuilder>();
        services.AddScoped<IDocumentDeserializer, FirestoreDocumentDeserializer>();
        services.AddScoped<ITypeConverter, FirestoreTypeConverter>();
        services.AddScoped<IEntityTracker, FirestoreEntityTracker>();
        services.AddScoped<IIncludeLoader, FirestoreIncludeLoader>();
        services.AddScoped<ILazyLoader, FirestoreLazyLoader>();

        // Options
        services.Configure<FirestoreErrorHandlingOptions>(o =>
        {
            o.MaxRetries = options.MaxRetries;
            o.InitialDelay = options.RetryInitialDelay;
        });

        return services;
    }
}

public class FirestorePipelineOptions
{
    public bool EnableAstLogging { get; set; } = false;
    public bool EnableQueryLogging { get; set; } = true;
    public bool EnableCaching { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
```

---

## Beneficios

1. **SRP**: Cada handler tiene una sola responsabilidad
2. **DI puro**: Sin service locator, sin reflexión
3. **Extensible**: Agregar handler sin modificar existentes
4. **Testeable**: Cada handler se testea aislado
5. **Configurable**: Quitar/reordenar handlers vía DI
6. **Logging**: Handlers dedicados para observabilidad
7. **Cache**: Se puede implementar en cualquier punto
8. **Retries**: Manejo automático de errores transitorios

---

## Plan de Implementación

### Fase 1: Infraestructura del Pipeline ✅ (59ec1c6)
- [x] `PipelineResult` (con variantes Streaming/Materialized/Scalar/Empty)
- [x] `PipelineContext` (con QueryKind y Metadata)
- [x] `PipelineDelegate`
- [x] `IQueryPipelineHandler`
- [x] `QueryPipelineHandlerBase` (con skip por QueryKind)
- [x] `IQueryPipelineMediator`
- [x] `QueryPipelineMediator`
- [x] `PipelineMetadataKeys`
- [x] Extension methods para contexto
- [x] `PipelineResultExtensions` (MaterializeAsync, ToStreaming)

### Fase 2: Error Handling ✅ (5af697b)
- [x] `FirestorePipelineException` y derivadas
- [x] `FirestoreExceptionClassifier`
- [x] `ErrorHandlingHandler`
- [x] `FirestoreErrorHandlingOptions`

### Fase 3: Handlers Core
- [x] `ResolverHandler` ✅ (6571748)
- [x] `ExecutionHandler` + `IQueryBuilder` ✅ (751316a)
  - Min/Max ejecutan: `SELECT campo ORDER BY campo LIMIT 1`
  - Min/Max retornan `Streaming` (no `Scalar`)
  - ConvertHandler extrae el valor del campo y maneja secuencia vacía
  - Secuencia vacía: nullable → null, non-nullable → exception (comportamiento EF Core)
- [x] `ConvertHandler` + `ITypeConverter` + `IDocumentDeserializer` ✅ (800e96a)
  - Scalar: convierte valores de agregación a tipos CLR
  - Streaming entidades: deserializa DocumentSnapshots
  - Min/Max: extrae valor del campo, maneja secuencia vacía
  - Empty: nullable → null, non-nullable → InvalidOperationException

### Fase 4: Handlers de Materialización
- [x] `TrackingHandler` ✅ (333da61)
  - Extiende QueryPipelineHandlerBase, solo aplica a QueryKind.Entity
  - Usa IStateManager directamente (no dbContext.Attach)
  - Identity resolution: previene instancias duplicadas
- [x] `ProxyHandler` + `IProxyFactory` ✅ (ba55733)
  - Extiende QueryPipelineHandlerBase, solo aplica a QueryKind.Entity
  - IProxyFactory nullable: null = proxies deshabilitados
  - Envuelve entidades en lazy-loading proxies cuando factory disponible
  - Pasa sin modificar si entityType no encontrado en modelo
- [ ] `IncludeHandler` + `IIncludeLoader`

### Fase 5: Handlers Opcionales
- [ ] `LogAstHandler`
- [ ] `LogQueryHandler`
- [ ] `CacheHandler`

### Fase 6: Lazy Loading
- [ ] `FirestoreLazyLoader : ILazyLoader`

### Fase 7: Integración
- [ ] `FirestorePipelineServiceCollectionExtensions`
- [ ] `FirestorePipelineOptions`
- [ ] Unificar Enumerables
- [ ] Actualizar Shaper
- [ ] Eliminar `FirestoreQueryExecutor`

---

## Archivos a Crear

```
Query/Pipeline/
├── PipelineContext.cs
├── PipelineResult.cs
├── PipelineDelegate.cs
├── IQueryPipelineHandler.cs
├── QueryPipelineHandlerBase.cs
├── IQueryPipelineMediator.cs
├── QueryPipelineMediator.cs
├── PipelineMetadataKeys.cs
├── Exceptions/
│   ├── FirestorePipelineException.cs
│   ├── FirestoreQueryExecutionException.cs
│   └── FirestoreDeserializationException.cs
└── Handlers/
    ├── ErrorHandlingHandler.cs
    ├── LogAstHandler.cs
    ├── CacheHandler.cs
    ├── ResolverHandler.cs
    ├── LogQueryHandler.cs
    ├── ExecutionHandler.cs
    ├── ConvertHandler.cs
    ├── TrackingHandler.cs
    ├── ProxyHandler.cs
    └── IncludeHandler.cs

Query/Services/
├── IQueryBuilder.cs
├── FirestoreQueryBuilder.cs
├── ITypeConverter.cs
├── FirestoreTypeConverter.cs
├── IEntityTracker.cs
├── FirestoreEntityTracker.cs
├── IIncludeLoader.cs
├── FirestoreIncludeLoader.cs
└── FirestoreLazyLoader.cs

Infrastructure/
└── FirestorePipelineServiceCollectionExtensions.cs
```

---

## Archivos a Eliminar (Fase 7)

- `Query/FirestoreQueryExecutor.cs` (2187 líneas)
- `Query/FirestoreAggregationQueryingEnumerable.cs`
- Código de reflexión en `FirestoreDocumentDeserializer.cs`
