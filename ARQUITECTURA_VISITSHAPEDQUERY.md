# Arquitectura del VisitShapedQuery: Cómo Funciona IAsyncEnumerable

## Diagrama de Flujo Completo

```
┌─────────────────────────────────────────────────────────────────────┐
│  USUARIO                                                              │
│  var productos = await context.Productos.ToListAsync();              │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  EF CORE QUERY PIPELINE                                               │
│  - Parsea el LINQ expression tree                                    │
│  - Invoca QueryableMethodTranslatingExpressionVisitor               │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryableMethodTranslatingExpressionVisitor                │
│  - TranslateWhere, TranslateOrderBy, etc.                           │
│  - Produce: ShapedQueryExpression {                                 │
│      QueryExpression: FirestoreQueryExpression,                     │
│      ShaperExpression: StructuralTypeShaperExpression               │
│    }                                                                 │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FirestoreShapedQueryCompilingExpressionVisitor                      │
│  - VisitShapedQuery() recibe el ShapedQueryExpression               │
│  - Compila a código ejecutable                                      │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                ┌───────────────┴────────────────┐
                │  VisitShapedQuery()            │
                │  ==========================     │
                │                                 │
                │  1. Extraer info:               │
                │     - FirestoreQueryExpression  │
                │     - Tipo de entidad (T)       │
                │                                 │
                │  2. Compilar Shaper:            │
                │     Lambda<Func<QueryContext,   │
                │            DocumentSnapshot, T>>│
                │                                 │
                │  3. RETORNAR:                   │
                │     Expression.New(             │
                │       typeof(FirestoreQuerying  │
                │              Enumerable<T>),    │
                │       queryContext,             │
                │       firestoreQueryExpression, │
                │       compiledShaper,           │
                │       contextType)              │
                └─────────────┬───────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  EF CORE COMPILA A CÓDIGO C#                                          │
│                                                                       │
│  var enumerable = new FirestoreQueryingEnumerable<Producto>(         │
│      queryContext,                                                   │
│      firestoreQueryExpression,  // Filters, OrderBy, Limit           │
│      shaper,                     // Func que materializa entidades   │
│      contextType);               // Tipo del DbContext               │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  EJECUCIÓN EN RUNTIME                                                 │
│  await foreach (var item in enumerable) { ... }                      │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryingEnumerable<T>.GetAsyncEnumerator()                 │
│  - Retorna AsyncEnumerator                                           │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  AsyncEnumerator.MoveNextAsync() - Primera llamada                   │
│                                                                       │
│  1. Obtener servicios del QueryContext:                             │
│     - IFirestoreClientWrapper                                        │
│     - ILoggerFactory                                                 │
│                                                                       │
│  2. Crear FirestoreQueryExecutor                                     │
│                                                                       │
│  3. Ejecutar query:                                                  │
│     var snapshot = await executor.ExecuteQueryAsync(                 │
│         firestoreQueryExpression, cancellationToken);                │
│                                                                       │
│  4. Guardar enumerador de documentos:                                │
│     _documentsEnumerator = snapshot.Documents.GetEnumerator();       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryExecutor.ExecuteQueryAsync()                          │
│                                                                       │
│  1. Construir Google.Cloud.Firestore.Query:                          │
│     var query = db.Collection("productos");                          │
│     query = query.WhereGreaterThan("Precio", 100);                   │
│     query = query.OrderBy("Nombre");                                 │
│     query = query.Limit(10);                                         │
│                                                                       │
│  2. Ejecutar:                                                        │
│     var snapshot = await query.GetSnapshotAsync();                   │
│                                                                       │
│  3. Retornar QuerySnapshot con DocumentSnapshots                     │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  AsyncEnumerator.MoveNextAsync() - Siguientes llamadas               │
│                                                                       │
│  1. Avanzar al siguiente documento:                                  │
│     if (!_documentsEnumerator.MoveNext()) return false;              │
│                                                                       │
│  2. Materializar entidad con el shaper:                              │
│     var document = _documentsEnumerator.Current;                     │
│     Current = _shaper(queryContext, document);                       │
│                                                                       │
│  3. Retornar true (hay más elementos)                                │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Shaper: Func<QueryContext, DocumentSnapshot, T>                     │
│                                                                       │
│  (queryContext, document) =>                                         │
│  {                                                                   │
│      // Obtener servicios                                           │
│      var model = dbContext.Model;                                   │
│      var typeMappingSource = serviceProvider.GetService<...>();     │
│      var collectionManager = serviceProvider.GetService<...>();     │
│      var loggerFactory = serviceProvider.GetService<...>();         │
│                                                                       │
│      // Crear deserializer                                          │
│      var deserializer = new FirestoreDocumentDeserializer(          │
│          model, typeMappingSource, collectionManager, logger);      │
│                                                                       │
│      // Deserializar                                                │
│      return deserializer.DeserializeEntity<T>(document);            │
│  }                                                                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FirestoreDocumentDeserializer.DeserializeEntity<T>()                │
│                                                                       │
│  1. Crear instancia: var entity = new T();                           │
│                                                                       │
│  2. Deserializar ID:                                                 │
│     entity.Id = document.Id;                                         │
│                                                                       │
│  3. Deserializar propiedades simples:                                │
│     var data = document.ToDictionary();                              │
│     entity.Nombre = (string)data["Nombre"];                          │
│     entity.Precio = (decimal)(double)data["Precio"];                 │
│                                                                       │
│  4. Deserializar Complex Properties (Value Objects):                 │
│     entity.Direccion = DeserializeComplexType(data["Direccion"]);   │
│                                                                       │
│  5. Deserializar GeoPoints:                                          │
│     entity.Ubicacion = DeserializeGeoPoint(data["Ubicacion"]);      │
│                                                                       │
│  6. Retornar entidad completa                                        │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  RESULTADO FINAL                                                      │
│  List<Producto> { producto1, producto2, producto3, ... }             │
└─────────────────────────────────────────────────────────────────────┘
```

## Tipos de Expresiones en Cada Paso

### Paso 1: VisitShapedQuery - LO QUE NO DEBES HACER ❌

```csharp
protected override Expression VisitShapedQuery(...)
{
    var lambda = Expression.Lambda(
        Expression.Call(executeMethod, ...),
        queryContextParameter);

    return lambda;
}

// Tipo retornado: Expression<Func<QueryContext, IAsyncEnumerable<T>>>
//                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                 Una FUNCIÓN que retorna IAsyncEnumerable
```

**Problema:** EF Core no puede consumir un `Func<...>`, necesita el `IAsyncEnumerable<T>` directamente.

### Paso 2: VisitShapedQuery - LO CORRECTO ✅

```csharp
protected override Expression VisitShapedQuery(...)
{
    var newExpression = Expression.New(
        typeof(FirestoreQueryingEnumerable<T>).GetConstructor(...),
        queryContext,
        firestoreQueryExpression,
        compiledShaper,
        contextType);

    return newExpression;
}

// Tipo retornado: NewExpression -> IAsyncEnumerable<T>
//                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                 Una expresión que CREA una instancia de IAsyncEnumerable
```

**Solución:** EF Core compila esto a código que crea directamente el enumerable.

## Comparación Visual de Tipos

```
┌────────────────────────────────────────────────────────────────┐
│  INCORRECTO ❌                                                  │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Expression.Lambda(...)                                         │
│       │                                                         │
│       └──> Tipo: Expression<Func<QueryContext,                 │
│                                  IAsyncEnumerable<T>>>          │
│                                                                 │
│  Cuando EF Core lo ejecuta:                                     │
│       │                                                         │
│       └──> Resultado: Func<QueryContext, IAsyncEnumerable<T>>  │
│                       ^^^^^^^^^^^^^^^^                          │
│                       Una función!                              │
│                                                                 │
│  EF Core intenta usar esto como IAsyncEnumerable<T>:           │
│       ❌ ERROR: No se puede convertir Func a IAsyncEnumerable  │
│                                                                 │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│  CORRECTO ✅                                                    │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Expression.New(typeof(FirestoreQueryingEnumerable<T>), ...)   │
│       │                                                         │
│       └──> Tipo: NewExpression -> IAsyncEnumerable<T>          │
│                                                                 │
│  Cuando EF Core lo ejecuta:                                     │
│       │                                                         │
│       └──> Resultado: FirestoreQueryingEnumerable<T>           │
│                       ^^^^^^^^^^^^^^^^^^^^^^^^^^                │
│                       Una instancia que implementa              │
│                       IAsyncEnumerable<T>                       │
│                                                                 │
│  EF Core lo usa como IAsyncEnumerable<T>:                       │
│       ✅ FUNCIONA: Es un IAsyncEnumerable                       │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

## Roles de Cada Componente

### FirestoreQueryExpression
- **Qué es:** Representación interna de una query (Filters, OrderBy, Limit)
- **Cuándo se crea:** En `QueryableMethodTranslatingExpressionVisitor`
- **Dónde se usa:** En `FirestoreQueryExecutor` para construir la query de Firestore

### ShapedQueryExpression
- **Qué es:** Contiene el `QueryExpression` + el `ShaperExpression`
- **Cuándo se crea:** En `QueryableMethodTranslatingExpressionVisitor`
- **Dónde se usa:** En `ShapedQueryCompilingExpressionVisitor.VisitShapedQuery`

### Shaper (Func<QueryContext, DocumentSnapshot, T>)
- **Qué es:** Una función que materializa una entidad desde un `DocumentSnapshot`
- **Cuándo se crea:** En `VisitShapedQuery`, compilado desde `ShaperExpression`
- **Dónde se usa:** En `AsyncEnumerator.MoveNextAsync()` para cada documento

### FirestoreQueryingEnumerable<T>
- **Qué es:** Clase que implementa `IAsyncEnumerable<T>`
- **Cuándo se crea:** En runtime, cuando EF Core ejecuta la expresión compilada
- **Dónde se usa:** Por `ToListAsync()`, `ForEachAsync()`, etc.

### AsyncEnumerator
- **Qué es:** Implementación de `IAsyncEnumerator<T>`
- **Cuándo se crea:** Cuando se llama `GetAsyncEnumerator()` en el enumerable
- **Dónde se usa:** Para iterar sobre los resultados de forma asíncrona

### FirestoreQueryExecutor
- **Qué es:** Ejecuta queries de Firestore
- **Cuándo se crea:** En `AsyncEnumerator.MoveNextAsync()` (primera iteración)
- **Dónde se usa:** Para traducir `FirestoreQueryExpression` a `Google.Cloud.Firestore.Query`

### FirestoreDocumentDeserializer
- **Qué es:** Convierte `DocumentSnapshot` a entidades C#
- **Cuándo se crea:** Dentro del shaper (o en el executor)
- **Dónde se usa:** Para materializar cada entidad desde Firestore

## Puntos Clave para Recordar

1. **VisitShapedQuery NO ejecuta la query**, solo crea una expresión que EF Core compilará
2. **Expression.New es la clave**, crea una instancia de un tipo que implementa `IAsyncEnumerable<T>`
3. **El shaper debe ser compilado** con `.Compile()` antes de pasarlo al constructor
4. **La ejecución real ocurre en AsyncEnumerator.MoveNextAsync()**, de forma lazy
5. **El deserializador se invoca por cada documento**, dentro del shaper

## Debugging Tips

### Si el error dice "cannot be used for return type"

```
Expression of type 'Func<QueryContext, IAsyncEnumerable<T>>'
cannot be used for return type 'IAsyncEnumerable<T>'
```

**Causa:** Estás retornando un Lambda en lugar de Expression.New

**Solución:** Cambiar de `Expression.Lambda(...)` a `Expression.New(...)`

### Si el error dice "constructor not found"

```
InvalidOperationException: No suitable constructor found for FirestoreQueryingEnumerable<T>
```

**Causa:** El constructor debe ser accesible (puede ser `internal`)

**Solución:** Verificar que el constructor tiene los parámetros correctos y es `public` o `internal`

### Si el error dice "cannot convert DocumentSnapshot to T"

```
InvalidCastException: Unable to cast object of type 'DocumentSnapshot' to type 'Producto'
```

**Causa:** El shaper no está deserializando correctamente

**Solución:** Verificar que el shaper llama al deserializador y retorna una entidad tipada

## Referencias de Código Fuente

### Cosmos DB Provider (Referencia principal)

**Archivo:** `CosmosShapedQueryCompilingExpressionVisitor.cs`
```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ... procesamiento ...

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

**Archivo:** `QueryingEnumerable.cs`
```csharp
private sealed class QueryingEnumerable<T> : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator(this, cancellationToken);

    private sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        public async ValueTask<bool> MoveNextAsync()
        {
            // Ejecutar query y materializar entidades
        }
    }
}
```

### InMemory Provider (Más sencillo)

**Archivo:** `InMemoryShapedQueryCompilingExpressionVisitor.cs`
```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ... procesamiento ...

    return Expression.New(
        typeof(QueryingEnumerable<>).MakeGenericType(valueBufferType).GetConstructors()[0],
        QueryCompilationContext.QueryContextParameter,
        Expression.Constant(inMemoryQueryExpression.ServerQueryExpression),
        Expression.Constant(shaperLambda.Compile()),
        Expression.Constant(QueryCompilationContext.ContextType),
        Expression.Constant(standAloneStateManager),
        Expression.Constant(threadSafetyChecksEnabled));
}
```

## Resumen Ultra-Simplificado

```
VisitShapedQuery debe retornar:
    new FirestoreQueryingEnumerable<T>(...)

NO debe retornar:
    () => new FirestoreQueryingEnumerable<T>(...)
```

La diferencia es sutil pero crítica: **una instancia vs una función que retorna una instancia**.
