# Investigación: Cómo el Provider InMemory Maneja el Tracking de Entidades

## Arquitectura de Queries en EF Core

El pipeline de queries en EF Core tiene estas fases:
```
1. LINQ Expression Tree (tu query)
       ↓
2. QueryTranslationPreprocessor (normaliza la query)
       ↓
3. QueryableMethodTranslatingExpressionVisitor (traduce métodos LINQ)
       ↓
4. QueryTranslationPostprocessor (optimizaciones finales)
       ↓
5. ShapedQueryCompilingExpressionVisitor (genera código ejecutable)
       ↓
6. QueryingEnumerable<T> (ejecuta y materializa entidades)
```

## Cómo InMemory Implementa el Tracking

### Archivos Clave del Provider InMemory
```
EFCore.InMemory/Query/Internal/
├── InMemoryShapedQueryCompilingExpressionVisitor.cs      ← Compila el shaper
├── InMemoryShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs  ← Ejecuta queries
├── InMemoryQueryableMethodTranslatingExpressionVisitor.cs  ← Traduce LINQ
└── InMemoryQueryExpression.cs  ← Representa la query traducida
```

### El Punto Crítico: EntityMaterializerInjectingExpressionVisitor

EF Core base (no el provider) tiene un visitor que **automáticamente inyecta código de tracking** en el shaper generado. Este visitor está en la clase base `ShapedQueryCompilingExpressionVisitor`:
```csharp
// En ShapedQueryCompilingExpressionVisitor (clase base de EF Core)
private readonly EntityMaterializerInjectingExpressionVisitor 
    _entityMaterializerInjectingExpressionVisitor;

protected virtual Expression InjectEntityMaterializers(Expression expression)
{
    return _entityMaterializerInjectingExpressionVisitor.Inject(expression);
}
```

Este visitor reemplaza cada `EntityShaperExpression` con código que:
1. Lee los valores del ValueBuffer
2. Llama a `QueryContext.StartTracking()` para registrar la entidad
3. Hidrata las shadow properties (incluyendo FKs)

### Lo Que InMemory Hace (y Firestore Debe Hacer)

El provider InMemory **NO implementa tracking custom**. Solo:

1. **Hereda de `ShapedQueryCompilingExpressionVisitor`** que ya tiene la lógica de tracking
2. **Llama a `InjectEntityMaterializers()`** en su método `VisitShapedQuery`
3. **Pasa el shaper al QueryingEnumerable** que ejecuta el código generado
```csharp
// Simplificación de InMemoryShapedQueryCompilingExpressionVisitor.VisitShapedQuery
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    var inMemoryQueryExpression = (InMemoryQueryExpression)shapedQueryExpression.QueryExpression;
    
    // CRÍTICO: Esta línea inyecta el código de tracking
    var shaper = InjectEntityMaterializers(shapedQueryExpression.ShaperExpression);
    
    // Compila el shaper a un Func<QueryContext, ValueBuffer, T>
    var shaperLambda = Expression.Lambda<Func<QueryContext, ValueBuffer, T>>(
        shaper, 
        QueryCompilationContext.QueryContextParameter,
        inMemoryQueryExpression.CurrentParameter);
    
    // Retorna el enumerable que ejecutará la query
    return Expression.New(
        typeof(QueryingEnumerable<>).MakeGenericType(elementType).GetConstructors()[0],
        Expression.Constant(_queryContext),
        Expression.Constant(serverEnumerable),
        Expression.Constant(shaperLambda.Compile()),
        // ... otros parámetros
    );
}
```

## El Problema en el Provider Firestore

Si las entidades no están siendo tracked, hay dos posibles causas:

### Causa 1: No se llama a InjectEntityMaterializers

Si tu `FirestoreShapedQueryCompilingExpressionVisitor` no llama a `InjectEntityMaterializers()`, el shaper no tendrá código de tracking.

**Verificar:** ¿Tu `VisitShapedQuery` llama a `InjectEntityMaterializers(shapedQueryExpression.ShaperExpression)`?

### Causa 2: El ValueBuffer no contiene las shadow properties

`InjectEntityMaterializers` genera código que lee shadow properties del `ValueBuffer`. Si el ValueBuffer no las contiene, el tracking falla silenciosamente.

**Verificar:** ¿Tu materialización incluye TODAS las propiedades, incluyendo shadow properties como `CategoriaId`?

## Diagnóstico Sugerido

Añade logging temporal en tu ShapedQueryCompilingExpressionVisitor:
```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    var originalShaper = shapedQueryExpression.ShaperExpression;
    Console.WriteLine($"Original Shaper Type: {originalShaper.GetType().Name}");
    
    var injectedShaper = InjectEntityMaterializers(originalShaper);
    Console.WriteLine($"Injected Shaper Type: {injectedShaper.GetType().Name}");
    Console.WriteLine($"Shaper changed: {originalShaper != injectedShaper}");
    
    // ... resto del código
}
```

Si `Shaper changed: false`, el materializer no está siendo inyectado correctamente.

## Solución Probable

El provider Firestore debe asegurar que:

1. **`FirestoreShapedQueryCompilingExpressionVisitor` hereda de `ShapedQueryCompilingExpressionVisitor`**

2. **Llama a `InjectEntityMaterializers()` en `VisitShapedQuery`**

3. **El `ValueBuffer` que pasa al shaper contiene TODAS las propiedades** incluyendo:
   - Propiedades regulares
   - Shadow properties (FKs como `CategoriaId`)
   - Propiedades de navegación materialized como DocumentReferences

4. **El índice de cada propiedad en el ValueBuffer coincide** con lo que espera el shaper generado

## Referencia: Estructura del ValueBuffer
```csharp
// El ValueBuffer es básicamente un object[] donde cada índice
// corresponde a una propiedad en el orden definido por el modelo

// Para una entidad como:
// ArticuloLazy { Id, Nombre, CategoriaId (shadow) }

// El ValueBuffer sería:
// valueBuffer[0] = "art_123"        // Id
// valueBuffer[1] = "Laptop"         // Nombre  
// valueBuffer[2] = "cat_456"        // CategoriaId (shadow FK)
```

## Resumen Ejecutivo

| Aspecto | InMemory | ¿Firestore debe hacer igual? |
|---------|----------|------------------------------|
| Heredar de ShapedQueryCompilingExpressionVisitor | ✅ Sí | ✅ Sí |
| Llamar a InjectEntityMaterializers() | ✅ Sí | ✅ Sí |
| Implementar tracking custom | ❌ No | ❌ No |
| Poblar shadow properties en ValueBuffer | ✅ Sí | ✅ **VERIFICAR** |

**Prioridad: Verificar que el ValueBuffer incluye las shadow properties y que se llama a InjectEntityMaterializers().**