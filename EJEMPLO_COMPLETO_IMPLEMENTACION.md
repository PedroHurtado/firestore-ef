# Ejemplo Completo: Implementación del ShapedQueryCompilingExpressionVisitor

## Resumen Ejecutivo

El problema raíz es que `VisitShapedQuery` NO debe retornar un `Lambda<Func<QueryContext, IAsyncEnumerable<T>>>`.

Debe retornar un `Expression.New` que cree una instancia de una clase que implemente `IAsyncEnumerable<T>`.

## Paso 1: Crear FirestoreQueryingEnumerable.cs

**Ubicación:** `firestore-efcore-provider/Query/FirestoreQueryingEnumerable.cs`

```csharp
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Storage;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Enumerable que ejecuta queries de Firestore y materializa entidades.
    /// Implementa IAsyncEnumerable para integrarse con el pipeline de EF Core.
    /// </summary>
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
            _queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            _queryExpression = queryExpression ?? throw new ArgumentNullException(nameof(queryExpression));
            _shaper = shaper ?? throw new ArgumentNullException(nameof(shaper));
            _contextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
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
            private readonly Type _contextType;

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
                _contextType = enumerable._contextType;
                _cancellationToken = cancellationToken;
            }

            public T Current { get; private set; }

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    // Ejecutar la query en la primera iteración
                    if (!_isExecuted)
                    {
                        await ExecuteQueryAsync();
                        _isExecuted = true;
                    }

                    // Iterar sobre los documentos
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
                    // Log del error
                    var logger = GetLogger();
                    logger?.LogError(ex, "Error during Firestore query enumeration for {EntityType}", _contextType.Name);
                    throw;
                }
            }

            private async Task ExecuteQueryAsync()
            {
                // Obtener servicios del QueryContext
                var dbContext = _queryContext.Context;
                var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;

                // Obtener las dependencias necesarias
                var clientWrapper = (IFirestoreClientWrapper)
                    serviceProvider.GetService(typeof(IFirestoreClientWrapper))
                    ?? throw new InvalidOperationException("IFirestoreClientWrapper not registered");

                var loggerFactory = (ILoggerFactory)
                    serviceProvider.GetService(typeof(ILoggerFactory));

                // Crear el executor
                var executorLogger = loggerFactory?.CreateLogger<FirestoreQueryExecutor>();
                var executor = new FirestoreQueryExecutor(clientWrapper, executorLogger);

                // Log de la query
                var logger = GetLogger();
                logger?.LogDebug("Executing Firestore query for {EntityType}: {Query}",
                    _contextType.Name, _queryExpression.ToString());

                // Ejecutar la query
                var snapshot = await executor.ExecuteQueryAsync(_queryExpression, _cancellationToken);

                logger?.LogDebug("Query returned {Count} documents for {EntityType}",
                    snapshot.Count, _contextType.Name);

                // Guardar el enumerador de documentos
                _documentsEnumerator = snapshot.Documents.GetEnumerator();
            }

            private ILogger GetLogger()
            {
                try
                {
                    var dbContext = _queryContext.Context;
                    var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;
                    var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory));
                    return loggerFactory?.CreateLogger<FirestoreQueryingEnumerable<T>>();
                }
                catch
                {
                    return null;
                }
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

## Paso 2: Modificar FirestoreShapedQueryCompilingExpressionVisitor

**Ubicación:** `firestore-efcore-provider/Infrastructure/FirestoreServiceCollectionExtensions.cs`

Reemplazar el método `VisitShapedQuery` actual con esta implementación:

```csharp
public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
{
    public FirestoreShapedQueryCompilingExpressionVisitor(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext)
        : base(dependencies, queryCompilationContext)
    {
    }

    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        // 1. Obtener el FirestoreQueryExpression
        var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

        // 2. Obtener el tipo de entidad
        var entityType = firestoreQueryExpression.EntityType.ClrType;

        // 3. Crear el shaper: una función que toma (QueryContext, DocumentSnapshot) y retorna T
        var shaperLambda = CreateShaper(shapedQueryExpression, entityType);

        // 4. Compilar el shaper a un delegate
        var compiledShaper = Expression.Constant(shaperLambda.Compile());

        // 5. Crear expresión constante con el query expression
        var queryExpressionConstant = Expression.Constant(firestoreQueryExpression);

        // 6. Obtener el tipo de contexto
        var contextTypeConstant = Expression.Constant(QueryCompilationContext.ContextType);

        // 7. Crear la expresión que instancia FirestoreQueryingEnumerable<T>
        var queryingEnumerableType = typeof(Query.FirestoreQueryingEnumerable<>)
            .MakeGenericType(entityType);

        var constructor = queryingEnumerableType.GetConstructors(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)[0];

        // 8. Crear la expresión: new FirestoreQueryingEnumerable<T>(
        //        queryContext,
        //        firestoreQueryExpression,
        //        shaper,
        //        contextType)
        var newExpression = Expression.New(
            constructor,
            QueryCompilationContext.QueryContextParameter,  // QueryContext queryContext
            queryExpressionConstant,                         // FirestoreQueryExpression queryExpression
            compiledShaper,                                  // Func<QueryContext, DocumentSnapshot, T> shaper
            contextTypeConstant);                            // Type contextType

        // 9. CRÍTICO: Retornar la expresión NEW directamente
        // NO crear un Lambda que retorne esto
        return newExpression;
    }

    /// <summary>
    /// Crea el shaper: una función que toma QueryContext y DocumentSnapshot y retorna la entidad materializada
    /// </summary>
    private LambdaExpression CreateShaper(ShapedQueryExpression shapedQueryExpression, Type entityType)
    {
        // Parámetros del shaper
        var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
        var documentParameter = Expression.Parameter(typeof(DocumentSnapshot), "document");

        // Obtener el deserializer del service provider
        var dbContextProperty = Expression.Property(queryContextParameter, "Context");

        var serviceProviderProperty = Expression.Property(
            Expression.Convert(dbContextProperty, typeof(IInfrastructure<IServiceProvider>)),
            "Instance");

        // Obtener IModel
        var getModelMethod = typeof(DbContextExtensions).GetMethod(
            nameof(DbContextExtensions.GetModel),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var modelExpression = Expression.Call(getModelMethod, dbContextProperty);

        // Obtener ITypeMappingSource
        var typeMappingSourceType = typeof(ITypeMappingSource);
        var getTypeMappingSourceMethod = typeof(ServiceProviderServiceExtensions).GetMethod(
            nameof(ServiceProviderServiceExtensions.GetService),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(IServiceProvider) },
            null)!.MakeGenericMethod(typeMappingSourceType);

        var typeMappingSourceExpression = Expression.Call(
            getTypeMappingSourceMethod,
            serviceProviderProperty);

        // Obtener IFirestoreCollectionManager
        var collectionManagerType = typeof(IFirestoreCollectionManager);
        var getCollectionManagerMethod = typeof(ServiceProviderServiceExtensions).GetMethod(
            nameof(ServiceProviderServiceExtensions.GetService),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(IServiceProvider) },
            null)!.MakeGenericMethod(collectionManagerType);

        var collectionManagerExpression = Expression.Call(
            getCollectionManagerMethod,
            serviceProviderProperty);

        // Obtener ILoggerFactory
        var loggerFactoryType = typeof(ILoggerFactory);
        var getLoggerFactoryMethod = typeof(ServiceProviderServiceExtensions).GetMethod(
            nameof(ServiceProviderServiceExtensions.GetService),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(IServiceProvider) },
            null)!.MakeGenericMethod(loggerFactoryType);

        var loggerFactoryExpression = Expression.Call(
            getLoggerFactoryMethod,
            serviceProviderProperty);

        // Crear logger para el deserializer
        var createLoggerMethod = typeof(LoggerFactoryExtensions).GetMethod(
            nameof(LoggerFactoryExtensions.CreateLogger),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(ILoggerFactory) },
            null)!.MakeGenericMethod(typeof(FirestoreDocumentDeserializer));

        var loggerExpression = Expression.Call(
            createLoggerMethod,
            loggerFactoryExpression);

        // Crear instancia del deserializer
        var deserializerConstructor = typeof(FirestoreDocumentDeserializer).GetConstructors()[0];
        var deserializerExpression = Expression.New(
            deserializerConstructor,
            modelExpression,
            typeMappingSourceExpression,
            collectionManagerExpression,
            loggerExpression);

        // Llamar a DeserializeEntity<T>(document)
        var deserializeMethod = typeof(FirestoreDocumentDeserializer)
            .GetMethod(nameof(FirestoreDocumentDeserializer.DeserializeEntity))
            .MakeGenericMethod(entityType);

        var deserializeCall = Expression.Call(
            deserializerExpression,
            deserializeMethod,
            documentParameter);

        // Crear el lambda: (queryContext, document) => deserializer.DeserializeEntity<T>(document)
        var shaperLambdaType = typeof(Func<,,>).MakeGenericType(
            typeof(QueryContext),
            typeof(DocumentSnapshot),
            entityType);

        var shaperLambda = Expression.Lambda(
            shaperLambdaType,
            deserializeCall,
            queryContextParameter,
            documentParameter);

        return shaperLambda;
    }
}
```

## Paso 3: Extensiones necesarias

Agregar al archivo si no existen:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
```

## Comparación: Antes vs Después

### ❌ ANTES (INCORRECTO):

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    var executeCall = Expression.Call(
        executeQueryMethod,
        queryContextParameter,
        queryExpressionConstant,
        cancellationTokenParameter);

    // Crea un Lambda que RETORNA IAsyncEnumerable
    var lambda = Expression.Lambda(
        executeCall,
        queryContextParameter);

    return lambda;  // ❌ TIPO: Expression<Func<QueryContext, IAsyncEnumerable<T>>>
}
```

**Problema:** Retorna un `Func<QueryContext, IAsyncEnumerable<T>>` (una función que devuelve un enumerable), no el enumerable directamente.

### ✅ DESPUÉS (CORRECTO):

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // Crea una INSTANCIA de una clase que ES IAsyncEnumerable
    var newExpression = Expression.New(
        typeof(FirestoreQueryingEnumerable<T>).GetConstructor(...),
        queryContextParameter,
        queryExpressionConstant,
        compiledShaper,
        contextTypeConstant);

    return newExpression;  // ✅ TIPO: NewExpression que crea IAsyncEnumerable<T>
}
```

**Solución:** Retorna directamente una expresión que crea una instancia de `FirestoreQueryingEnumerable<T>`, que implementa `IAsyncEnumerable<T>`.

## Flujo de Ejecución Completo

```
1. Usuario escribe:
   var productos = await context.Productos.ToListAsync();

2. EF Core compila el LINQ:
   - QueryableMethodTranslatingExpressionVisitor traduce a FirestoreQueryExpression
   - ShapedQueryCompilingExpressionVisitor.VisitShapedQuery se ejecuta

3. VisitShapedQuery retorna:
   Expression.New(
       typeof(FirestoreQueryingEnumerable<Producto>),
       queryContext,
       firestoreQueryExpression,
       shaper,
       contextType)

4. EF Core compila esto a código C#:
   var enumerable = new FirestoreQueryingEnumerable<Producto>(
       queryContext, firestoreQueryExpression, shaper, contextType);

5. ToListAsync() llama:
   await foreach (var item in enumerable) { ... }

6. FirestoreQueryingEnumerable.GetAsyncEnumerator() retorna AsyncEnumerator

7. AsyncEnumerator.MoveNextAsync():
   - Primera llamada: ejecuta la query de Firestore
   - Siguientes llamadas: itera sobre los DocumentSnapshots
   - Para cada documento: llama al shaper que deserializa la entidad

8. Resultado: List<Producto> con todas las entidades materializadas
```

## Errores Comunes

### Error 1: Retornar un Lambda en lugar de Expression.New

```csharp
// ❌ MAL
var lambda = Expression.Lambda(executeCall, queryContextParameter);
return lambda;
```

**Síntoma:** Error "Expression of type 'Func<...>' cannot be used for return type"

**Solución:** Usar `Expression.New` para crear la instancia directamente.

### Error 2: Intentar usar async methods en el shaper

```csharp
// ❌ MAL
var deserializeCall = Expression.Call(deserializeAsyncMethod, ...);
```

**Síntoma:** El shaper debe ser síncrono (no async)

**Solución:** La ejecución async está en `AsyncEnumerator.MoveNextAsync()`, el shaper solo materializa documentos ya obtenidos.

### Error 3: No compilar el shaper

```csharp
// ❌ MAL
var newExpression = Expression.New(
    constructor,
    queryContextParameter,
    queryExpressionConstant,
    shaperLambda,  // ❌ Sin compilar
    contextTypeConstant);
```

**Síntoma:** Error de tipo, espera un `Func<...>` no un `Expression<Func<...>>`

**Solución:** Compilar el shaper: `Expression.Constant(shaperLambda.Compile())`

## Referencias del Código Fuente de EF Core

### Cosmos DB Provider

- **Archivo:** `CosmosShapedQueryCompilingExpressionVisitor.cs`
- **Patrón:** Usa `Expression.New` con `QueryingEnumerable<T>`
- **GitHub:** https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosShapedQueryCompilingExpressionVisitor.cs

### InMemory Provider

- **Archivo:** `InMemoryShapedQueryCompilingExpressionVisitor.cs`
- **Patrón:** Usa `Expression.New` con `QueryingEnumerable<T>`
- **GitHub:** https://github.com/dotnet/efcore/blob/main/src/EFCore.InMemory/Query/Internal/InMemoryShapedQueryCompilingExpressionVisitor.cs

## Próximos Pasos

1. ✅ Crear `FirestoreQueryingEnumerable.cs`
2. ✅ Modificar `VisitShapedQuery` en `FirestoreShapedQueryCompilingExpressionVisitor`
3. ✅ Implementar `CreateShaper` para materializar entidades
4. ⚠️ Probar con `context.Productos.ToListAsync()`
5. ⚠️ Implementar soporte para Where, OrderBy, Take (Fase 2)

## Validación

Para verificar que funciona correctamente, prueba:

```csharp
using var context = new MiDbContext();

// Debe funcionar sin errores
var productos = await context.Productos.ToListAsync();

Console.WriteLine($"Se obtuvieron {productos.Count} productos");
foreach (var p in productos)
{
    Console.WriteLine($"- {p.Nombre}: {p.Precio}");
}
```

Si ves los productos impresos correctamente, la implementación está funcionando.
