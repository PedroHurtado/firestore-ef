# Solución al Error de VisitShapedQuery con IAsyncEnumerable

## El Problema

El error que estás obteniendo:
```
Expression of type 'System.Func`2[Microsoft.EntityFrameworkCore.Query.QueryContext,System.Collections.Generic.IAsyncEnumerable`1[Cliente]]'
cannot be used for return type 'System.Collections.Generic.IAsyncEnumerable`1[Cliente]'
```

Indica que estás retornando un **Lambda/Func** que devuelve `IAsyncEnumerable<T>`, cuando EF Core necesita una **expresión que cree directamente una instancia** de algo que implemente `IAsyncEnumerable<T>`.

## La Solución: Patrón QueryingEnumerable

Basado en cómo lo implementa el provider de Cosmos DB y otros providers oficiales de EF Core, la solución correcta es:

### 1. Crear una clase QueryingEnumerable<T> que implemente IAsyncEnumerable<T>

```csharp
// Archivo: Query/FirestoreQueryingEnumerable.cs

using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Enumerable que ejecuta queries de Firestore de forma asíncrona.
    /// Implementa IAsyncEnumerable para integrarse con el pipeline de EF Core.
    /// </summary>
    internal sealed class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
        where T : class, new()
    {
        private readonly QueryContext _queryContext;
        private readonly FirestoreQueryExpression _queryExpression;
        private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
        private readonly Type _contextType;
        private readonly ILogger _logger;

        public FirestoreQueryingEnumerable(
            QueryContext queryContext,
            FirestoreQueryExpression queryExpression,
            Func<QueryContext, DocumentSnapshot, T> shaper,
            Type contextType,
            ILogger logger)
        {
            _queryContext = queryContext;
            _queryExpression = queryExpression;
            _shaper = shaper;
            _contextType = contextType;
            _logger = logger;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotSupportedException(
                "Firestore queries must be executed asynchronously. Use ToListAsync() instead of ToList().");
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly FirestoreQueryingEnumerable<T> _enumerable;
            private readonly QueryContext _queryContext;
            private readonly FirestoreQueryExpression _queryExpression;
            private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
            private readonly CancellationToken _cancellationToken;
            private readonly ILogger _logger;

            private IEnumerator<DocumentSnapshot> _documentsEnumerator;
            private bool _isExecuted;

            public AsyncEnumerator(
                FirestoreQueryingEnumerable<T> enumerable,
                CancellationToken cancellationToken)
            {
                _enumerable = enumerable;
                _queryContext = enumerable._queryContext;
                _queryExpression = enumerable._queryExpression;
                _shaper = enumerable._shaper;
                _cancellationToken = cancellationToken;
                _logger = enumerable._logger;
            }

            public T Current { get; private set; }

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    if (!_isExecuted)
                    {
                        // Ejecutar la query una sola vez
                        await ExecuteQueryAsync();
                        _isExecuted = true;
                    }

                    if (_documentsEnumerator == null || !_documentsEnumerator.MoveNext())
                    {
                        Current = default;
                        return false;
                    }

                    // Materializar la entidad usando el shaper
                    var document = _documentsEnumerator.Current;
                    Current = _shaper(_queryContext, document);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during Firestore query enumeration");
                    throw;
                }
            }

            private async Task ExecuteQueryAsync()
            {
                // Obtener servicios del QueryContext
                var dbContext = _queryContext.Context;
                var serviceProvider = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)dbContext).Instance;

                // Obtener las dependencias necesarias
                var clientWrapper = (Infrastructure.IFirestoreClientWrapper)
                    serviceProvider.GetService(typeof(Infrastructure.IFirestoreClientWrapper));

                var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)
                    serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory));

                // Crear el executor
                var executorLogger = loggerFactory?.CreateLogger<FirestoreQueryExecutor>();
                var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

                // Ejecutar la query
                _logger?.LogDebug("Executing Firestore query: {Query}", _queryExpression.ToString());
                var snapshot = await executor.ExecuteQueryAsync(_queryExpression, _cancellationToken);

                _logger?.LogDebug("Query returned {Count} documents", snapshot.Count);

                // Guardar el enumerador de documentos
                _documentsEnumerator = snapshot.Documents.GetEnumerator();
            }

            public ValueTask DisposeAsync()
            {
                _documentsEnumerator?.Dispose();
                _documentsEnumerator = null;
                return default;
            }
        }
    }
}
```

### 2. Modificar VisitShapedQuery para usar Expression.New

Ahora, en tu `FirestoreShapedQueryCompilingExpressionVisitor`, el método `VisitShapedQuery` debe crear una expresión que instancie `FirestoreQueryingEnumerable<T>`:

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // 1. Obtener el FirestoreQueryExpression
    var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

    // 2. Obtener el tipo de entidad
    var entityType = firestoreQueryExpression.EntityType.ClrType;

    // 3. Compilar el shaper
    // El shaper toma un QueryContext y un DocumentSnapshot y retorna la entidad materializada
    var shaperExpression = BuildShaper(shapedQueryExpression.ShaperExpression);
    var shaperLambda = Expression.Lambda<Func<QueryContext, DocumentSnapshot, object>>(
        shaperExpression,
        QueryCompilationContext.QueryContextParameter,
        Expression.Parameter(typeof(DocumentSnapshot), "document"));

    var compiledShaper = Expression.Constant(shaperLambda.Compile());

    // 4. Crear expresión constante con el query expression
    var queryExpressionConstant = Expression.Constant(firestoreQueryExpression);

    // 5. Obtener el tipo de contexto
    var contextTypeConstant = Expression.Constant(QueryCompilationContext.ContextType);

    // 6. Crear logger (opcional, puede ser null)
    var loggerConstant = Expression.Constant(null, typeof(ILogger));

    // 7. Crear la expresión que instancia FirestoreQueryingEnumerable<T>
    var queryingEnumerableType = typeof(Query.FirestoreQueryingEnumerable<>)
        .MakeGenericType(entityType);

    var constructor = queryingEnumerableType.GetConstructors()[0];

    // 8. Crear la expresión: new FirestoreQueryingEnumerable<T>(
    //        queryContext,
    //        firestoreQueryExpression,
    //        shaper,
    //        contextType,
    //        logger)
    var newExpression = Expression.New(
        constructor,
        QueryCompilationContext.QueryContextParameter,  // QueryContext queryContext
        queryExpressionConstant,                         // FirestoreQueryExpression queryExpression
        compiledShaper,                                  // Func<QueryContext, DocumentSnapshot, T> shaper
        contextTypeConstant,                             // Type contextType
        loggerConstant);                                 // ILogger logger

    // 9. IMPORTANTE: Retornar la expresión NEW directamente, NO wrapped en un Lambda
    return newExpression;
}

private Expression BuildShaper(Expression shaperExpression)
{
    // Este método debe convertir el ShaperExpression de EF Core
    // en una expresión que tome un DocumentSnapshot y retorne la entidad

    // Por ahora, una implementación simple que asume que el shaper
    // es un StructuralTypeShaperExpression

    // TODO: Implementar correctamente la traducción del shaper
    // Esto involucra procesar ProjectionBindingExpression y
    // crear código que deserialice desde DocumentSnapshot

    return shaperExpression;
}
```

## Explicación Clave del Error

### Lo que ESTABAS haciendo (INCORRECTO):

```csharp
// Esto crea un Lambda que RETORNA IAsyncEnumerable
var lambda = Expression.Lambda(
    Expression.Call(executeQueryMethod, ...),  // Retorna IAsyncEnumerable<T>
    queryContextParameter);

return lambda;  // ❌ Tipo: Expression<Func<QueryContext, IAsyncEnumerable<T>>>
```

Esto retorna un `Expression<Func<QueryContext, IAsyncEnumerable<T>>>`, es decir, una función que cuando se ejecuta devuelve un `IAsyncEnumerable<T>`.

### Lo que DEBES hacer (CORRECTO):

```csharp
// Esto crea una instancia de una clase que ES IAsyncEnumerable
var newExpression = Expression.New(
    typeof(FirestoreQueryingEnumerable<T>).GetConstructor(...),
    queryContextParameter,
    ...otrosParametros);

return newExpression;  // ✅ Tipo: NewExpression que crea IAsyncEnumerable<T>
```

Esto retorna un `NewExpression` que cuando se ejecuta crea directamente una instancia de `FirestoreQueryingEnumerable<T>`, que implementa `IAsyncEnumerable<T>`.

## Flujo de Ejecución

```
Usuario: context.Productos.ToListAsync()
    ↓
EF Core compila la query
    ↓
VisitShapedQuery se ejecuta
    ↓
Retorna: Expression.New(FirestoreQueryingEnumerable<Producto>(...))
    ↓
EF Core compila esta expresión a código
    ↓
En runtime: var enumerable = new FirestoreQueryingEnumerable<Producto>(...)
    ↓
EF Core llama: await enumerable.GetAsyncEnumerator()
    ↓
AsyncEnumerator.MoveNextAsync() ejecuta la query de Firestore
    ↓
Cada iteración materializa una entidad usando el shaper
    ↓
ToListAsync() recolecta todos los resultados en una List<Producto>
```

## Comparación con Cosmos DB Provider

El provider de Cosmos DB hace exactamente esto:

```csharp
// CosmosShapedQueryCompilingExpressionVisitor.cs
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ... procesar shaper ...

    // Crear instancia de QueryingEnumerable
    return Expression.New(
        typeof(QueryingEnumerable<>).MakeGenericType(elementType).GetConstructors()[0],
        cosmosQueryContextConstant,
        sqlExpressionFactory,
        querySqlGeneratorFactory,
        selectExpression,
        shaperLambda,
        contextType,
        rootEntityType,
        partitionKeyValues,
        standAloneStateManager,
        threadSafetyChecksEnabled);
}
```

## Archivos Necesarios

1. **Query/FirestoreQueryingEnumerable.cs** (NUEVO)
   - Clase que implementa `IAsyncEnumerable<T>`
   - Contiene la lógica de ejecución de la query
   - Contiene el `AsyncEnumerator` que maneja la iteración

2. **Infrastructure/FirestoreServiceCollectionExtensions.cs** (MODIFICAR)
   - Actualizar `VisitShapedQuery` para usar `Expression.New`
   - Implementar `BuildShaper` correctamente

3. **Storage/FirestoreDocumentDeserializer.cs** (USAR EXISTENTE)
   - Ya existe y puede ser usado por el shaper
   - Convierte `DocumentSnapshot` a entidades

## Siguiente Paso: Implementar el Shaper Correctamente

El shaper es la parte más compleja. Debe:

1. Extraer el `ProjectionBindingExpression` del `ShaperExpression`
2. Crear una expresión que:
   - Tome un `DocumentSnapshot` como parámetro
   - Llame al deserializador para convertirlo en entidad
   - Retorne la entidad tipada

Ejemplo básico:

```csharp
private Expression BuildShaper(Expression shaperExpression)
{
    // Parámetro: DocumentSnapshot document
    var documentParameter = Expression.Parameter(typeof(DocumentSnapshot), "document");

    // Obtener el deserializador del service provider
    var deserializerMethod = typeof(FirestoreDocumentDeserializer)
        .GetMethod(nameof(FirestoreDocumentDeserializer.DeserializeEntity))
        .MakeGenericMethod(firestoreQueryExpression.EntityType.ClrType);

    // Crear expresión: deserializer.DeserializeEntity<T>(document)
    // (simplificado - necesitas obtener el deserializador del context)

    return shaperExpression; // Por ahora, retornar el original
}
```

## Referencias de Implementación

- **EF Core Cosmos Provider**: `CosmosShapedQueryCompilingExpressionVisitor.cs` y `QueryingEnumerable.cs`
- **EF Core InMemory Provider**: `InMemoryShapedQueryCompilingExpressionVisitor.cs`

Ambos usan el patrón `Expression.New` + `QueryingEnumerable` + `AsyncEnumerator`.
