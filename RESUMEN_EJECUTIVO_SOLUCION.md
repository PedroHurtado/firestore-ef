# Resumen Ejecutivo: Solución al Error de VisitShapedQuery

## TL;DR (Resumen Ultra-Corto)

**Problema:**
```csharp
// ❌ INCORRECTO
var lambda = Expression.Lambda(asyncMethod, queryContext);
return lambda;  // Error: Func<...> no es IAsyncEnumerable<T>
```

**Solución:**
```csharp
// ✅ CORRECTO
var newExpr = Expression.New(typeof(QueryingEnumerable<T>).GetConstructor(...), ...);
return newExpr;  // Crea directamente una instancia de IAsyncEnumerable<T>
```

## El Error Completo

```
Expression of type 'System.Func`2[Microsoft.EntityFrameworkCore.Query.QueryContext,System.Collections.Generic.IAsyncEnumerable`1[Cliente]]'
cannot be used for return type 'System.Collections.Generic.IAsyncEnumerable`1[Cliente]'
```

**Traducción:** Estás retornando una función que devuelve un `IAsyncEnumerable<T>`, cuando debes retornar directamente el `IAsyncEnumerable<T>`.

## Causa Raíz

Tu implementación actual:

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ...

    var executeCall = Expression.Call(
        executeQueryMethod,  // Método async que retorna IAsyncEnumerable<T>
        queryContextParameter,
        queryExpressionConstant,
        cancellationTokenParameter);

    // ❌ PROBLEMA: Crear un Lambda
    var lambda = Expression.Lambda(
        executeCall,
        queryContextParameter);

    return lambda;  // Tipo: Expression<Func<QueryContext, IAsyncEnumerable<T>>>
}
```

**Por qué falla:**
- `Expression.Lambda` crea un delegado (una función)
- EF Core espera una expresión que **cree una instancia** de `IAsyncEnumerable<T>`
- No puede convertir `Func<QueryContext, IAsyncEnumerable<T>>` a `IAsyncEnumerable<T>`

## La Solución en 3 Pasos

### Paso 1: Crear FirestoreQueryingEnumerable<T>

**Archivo:** `firestore-efcore-provider/Query/FirestoreQueryingEnumerable.cs`

```csharp
internal sealed class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>
    where T : class, new()
{
    private readonly QueryContext _queryContext;
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Func<QueryContext, DocumentSnapshot, T> _shaper;
    private readonly Type _contextType;

    // Constructor
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

    // Implementar IAsyncEnumerable
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator(this, cancellationToken);

    // Bloquear uso sincrónico
    public IEnumerator<T> GetEnumerator()
        => throw new NotSupportedException("Use ToListAsync() instead of ToList()");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Enumerador asíncrono anidado
    private sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        // ... implementación ...
        // Ver archivo EJEMPLO_COMPLETO_IMPLEMENTACION.md para código completo
    }
}
```

### Paso 2: Modificar VisitShapedQuery

**Archivo:** `firestore-efcore-provider/Infrastructure/FirestoreServiceCollectionExtensions.cs`

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // 1. Obtener FirestoreQueryExpression
    var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

    // 2. Obtener tipo de entidad
    var entityType = firestoreQueryExpression.EntityType.ClrType;

    // 3. Crear el shaper
    var shaperLambda = CreateShaper(shapedQueryExpression, entityType);

    // 4. ✅ CLAVE: Compilar el shaper
    var compiledShaper = Expression.Constant(shaperLambda.Compile());

    // 5. Crear constantes
    var queryExpressionConstant = Expression.Constant(firestoreQueryExpression);
    var contextTypeConstant = Expression.Constant(QueryCompilationContext.ContextType);

    // 6. Obtener el tipo genérico
    var queryingEnumerableType = typeof(Query.FirestoreQueryingEnumerable<>)
        .MakeGenericType(entityType);

    // 7. Obtener el constructor
    var constructor = queryingEnumerableType.GetConstructors(
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance)[0];

    // 8. ✅ CLAVE: Expression.New (NO Lambda!)
    return Expression.New(
        constructor,
        QueryCompilationContext.QueryContextParameter,  // QueryContext
        queryExpressionConstant,                         // FirestoreQueryExpression
        compiledShaper,                                  // Func<QueryContext, DocumentSnapshot, T>
        contextTypeConstant);                            // Type
}
```

### Paso 3: Implementar CreateShaper

```csharp
private LambdaExpression CreateShaper(ShapedQueryExpression shapedQueryExpression, Type entityType)
{
    // Parámetros del shaper
    var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
    var documentParameter = Expression.Parameter(typeof(DocumentSnapshot), "document");

    // Obtener servicios del QueryContext
    var dbContextProperty = Expression.Property(queryContextParameter, "Context");
    var serviceProviderProperty = Expression.Property(
        Expression.Convert(dbContextProperty, typeof(IInfrastructure<IServiceProvider>)),
        "Instance");

    // ... código para obtener IModel, ITypeMappingSource, etc ...
    // Ver EJEMPLO_COMPLETO_IMPLEMENTACION.md para código completo

    // Crear instancia del deserializer
    var deserializerExpression = Expression.New(
        typeof(FirestoreDocumentDeserializer).GetConstructors()[0],
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

    // Crear lambda: (queryContext, document) => deserializer.DeserializeEntity<T>(document)
    var shaperLambdaType = typeof(Func<,,>).MakeGenericType(
        typeof(QueryContext),
        typeof(DocumentSnapshot),
        entityType);

    return Expression.Lambda(
        shaperLambdaType,
        deserializeCall,
        queryContextParameter,
        documentParameter);
}
```

## Comparación Visual

```
┌──────────────────────────────────────────────────────────────┐
│  LO QUE ESTABAS HACIENDO ❌                                   │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  VisitShapedQuery retorna:                                    │
│      Expression<Func<QueryContext, IAsyncEnumerable<T>>>     │
│                                                               │
│  Cuando EF Core lo ejecuta:                                   │
│      Func<QueryContext, IAsyncEnumerable<T>> función         │
│                                                               │
│  EF Core intenta usarlo como IAsyncEnumerable<T>:            │
│      ❌ ERROR: No puede convertir Func a IAsyncEnumerable    │
│                                                               │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  LO QUE DEBES HACER ✅                                        │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  VisitShapedQuery retorna:                                    │
│      NewExpression -> FirestoreQueryingEnumerable<T>         │
│                                                               │
│  Cuando EF Core lo ejecuta:                                   │
│      FirestoreQueryingEnumerable<T> instancia                │
│                                                               │
│  EF Core lo usa como IAsyncEnumerable<T>:                     │
│      ✅ FUNCIONA: Es un IAsyncEnumerable<T>                   │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

## Flujo de Ejecución

```
1. Usuario ejecuta:
   var productos = await context.Productos.ToListAsync();

2. EF Core compila el LINQ a Expression Tree

3. VisitShapedQuery se invoca:
   - Retorna: Expression.New(FirestoreQueryingEnumerable<Producto>(...))

4. EF Core compila a código C#:
   var enumerable = new FirestoreQueryingEnumerable<Producto>(...);

5. ToListAsync() itera:
   await foreach (var item in enumerable) { ... }

6. Primera iteración (MoveNextAsync):
   - Ejecuta la query de Firestore
   - Obtiene QuerySnapshot con DocumentSnapshots

7. Iteraciones siguientes:
   - Para cada DocumentSnapshot:
     * Aplicar shaper
     * Deserializar a entidad
     * Retornar entidad

8. Resultado: List<Producto>
```

## Archivos a Crear/Modificar

### Nuevos Archivos

1. **firestore-efcore-provider/Query/FirestoreQueryingEnumerable.cs**
   - Clase que implementa `IAsyncEnumerable<T>`
   - Contiene `AsyncEnumerator` anidado

### Archivos a Modificar

1. **firestore-efcore-provider/Infrastructure/FirestoreServiceCollectionExtensions.cs**
   - Modificar `FirestoreShapedQueryCompilingExpressionVisitor.VisitShapedQuery`
   - Agregar método `CreateShaper`

## Checklist de Implementación

- [ ] Crear `FirestoreQueryingEnumerable.cs`
  - [ ] Constructor con 4 parámetros
  - [ ] Implementar `GetAsyncEnumerator()`
  - [ ] Implementar `GetEnumerator()` con excepción
  - [ ] Crear clase `AsyncEnumerator` anidada
  - [ ] Implementar `MoveNextAsync()` con lazy execution
  - [ ] Implementar `DisposeAsync()`

- [ ] Modificar `VisitShapedQuery`
  - [ ] Obtener `FirestoreQueryExpression`
  - [ ] Crear shaper con `CreateShaper()`
  - [ ] Compilar shaper con `.Compile()`
  - [ ] Usar `Expression.New` (NO `Expression.Lambda`)
  - [ ] Retornar `NewExpression`

- [ ] Implementar `CreateShaper`
  - [ ] Crear parámetros: `QueryContext` y `DocumentSnapshot`
  - [ ] Obtener servicios del `QueryContext`
  - [ ] Crear expresión que instancie `FirestoreDocumentDeserializer`
  - [ ] Crear expresión que llame a `DeserializeEntity<T>`
  - [ ] Retornar `LambdaExpression`

- [ ] Probar
  - [ ] `context.Productos.ToListAsync()` debe funcionar
  - [ ] Las entidades deben deserializarse correctamente
  - [ ] No debe haber errores de tipo

## Puntos Críticos para Recordar

1. **Expression.New, NO Expression.Lambda**
   - `Expression.New` crea una instancia
   - `Expression.Lambda` crea una función

2. **Compilar el shaper antes de pasarlo**
   - `.Compile()` convierte `Expression<Func<...>>` a `Func<...>`

3. **Lazy execution en AsyncEnumerator**
   - La query NO se ejecuta hasta la primera llamada a `MoveNextAsync()`

4. **El shaper debe ser síncrono**
   - Tipo: `Func<QueryContext, DocumentSnapshot, T>`
   - NO async

5. **GetEnumerator() sincrónico lanza excepción**
   - Firestore solo soporta operaciones asíncronas

## Referencias

### Documentación Creada

1. **SOLUCION_VISITSHAPEDQUERY.md** - Explicación detallada del problema y solución
2. **EJEMPLO_COMPLETO_IMPLEMENTACION.md** - Código completo con ejemplos
3. **ARQUITECTURA_VISITSHAPEDQUERY.md** - Diagramas y flujos de ejecución
4. **PATRONES_COSMOSDB_PROVIDER.md** - Patrones del provider oficial de Cosmos DB
5. Este archivo - Resumen ejecutivo

### Código Fuente de EF Core

- **Cosmos DB Provider**: [CosmosShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosShapedQueryCompilingExpressionVisitor.cs)
- **Cosmos DB QueryingEnumerable**: [QueryingEnumerable.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs)
- **InMemory Provider**: [InMemoryShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.InMemory/Query/Internal/InMemoryShapedQueryCompilingExpressionVisitor.cs)

## Resultado Final Esperado

Después de implementar esta solución, deberías poder ejecutar:

```csharp
using var context = new MiDbContext();

// Debe funcionar sin errores
var productos = await context.Productos.ToListAsync();

Console.WriteLine($"Obtenidos {productos.Count} productos");
foreach (var p in productos)
{
    Console.WriteLine($"- {p.Nombre}: ${p.Precio}");
}
```

Y ver los productos impresos correctamente en la consola.

## Próximos Pasos (Después de Esta Implementación)

1. **Fase 2: Implementar Where**
   - `TranslateWhere` en `FirestoreQueryableMethodTranslatingExpressionVisitor`
   - Traducir expresiones binarias a `FirestoreWhereClause`

2. **Fase 3: Implementar OrderBy y Take**
   - `TranslateOrderBy`, `TranslateThenBy`, `TranslateTake`

3. **Fase 4: Implementar FirstOrDefault, Count, Any**
   - Operaciones de agregación

Ver `PLAN_IMPLEMENTACION_LINQ.md` para el plan completo de fases.

## ¿Necesitas Ayuda?

Si encuentras errores durante la implementación:

1. **Error de tipo en Expression.New:**
   - Verifica que los parámetros del constructor coincidan
   - Asegúrate de pasar `Expression.Constant(shaperLambda.Compile())`

2. **Error de "constructor not found":**
   - El constructor puede ser `internal`, usa `BindingFlags.NonPublic`

3. **Error en tiempo de ejecución al deserializar:**
   - Verifica que `FirestoreDocumentDeserializer` esté funcionando correctamente
   - Agrega logging en `CreateShaper` para debug

4. **Null reference exception:**
   - Asegúrate de que todos los servicios están registrados en DI
   - Verifica que `IFirestoreClientWrapper` está disponible

---

**¡Éxito con la implementación!**
