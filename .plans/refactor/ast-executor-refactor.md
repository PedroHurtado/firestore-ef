# Refactor: AST y Executor

## Resumen Ejecutivo

Este documento describe los problemas arquitectónicos identificados en el pipeline de queries del provider de Firestore para EF Core, y propone una solución integral.

---

## Problemas Identificados

### 1. AST Híbrido

El AST (`FirestoreQueryExpression`) tiene propiedades duplicadas para el mismo concepto:

```csharp
// Constante vs Expresión parametrizada
public int? Limit { get; set; }
public Expression? LimitExpression { get; set; }

public int? Skip { get; set; }
public Expression? SkipExpression { get; set; }

public int? LimitToLast { get; set; }
public Expression? LimitToLastExpression { get; set; }
```

**Causa:** EF Core cachea `Func<QueryContext, TResult>`. Las constantes se embeben directamente (una entrada de caché por valor), mientras que las expresiones parametrizadas se resuelven en runtime desde `QueryContext.ParameterValues`.

**Decisión:** Aceptamos el AST híbrido como necesario por el modelo de cacheo de EF Core.

---

### 2. Lógica de Evaluación Dispersa

La evaluación de `QueryContext.ParameterValues` ocurre en múltiples lugares:

| Ubicación | Método | Qué evalúa |
|-----------|--------|------------|
| `FirestoreWhereClause` | `EvaluateValue()` | Valores de filtros WHERE |
| `FirestoreQueryExecutor` | `EvaluateIntExpression()` | Limit, Skip, LimitToLast |
| `FirestoreQueryExecutor` | `EvaluateIdExpression()` | IdValueExpression |
| `FirestoreQueryExecutor` | `CompileFilterPredicate()` | Filtros de Include |
| `FirestoreShapedQueryCompilingExpressionVisitor` | `CompileFilterPredicate()` | Filtros de Include (duplicado) |

**Problema:** Viola SRP, código duplicado, difícil de mantener.

---

### 3. Executor Conoce QueryContext

`FirestoreQueryExecutor` recibe `QueryContext` directamente:

```csharp
ExecuteQueryAsync<T>(
    FirestoreQueryExpression queryExpression,
    QueryContext queryContext,  // ← Dependencia de EF Core
    DbContext dbContext,
    bool isTracking,
    CancellationToken cancellationToken)
```

**Problema:** El Executor debería ser agnóstico a EF Core. Solo debería recibir datos ya resueltos.

---

### 4. FirestoreQueryContext Vacío

```csharp
public class FirestoreQueryContext : QueryContext
{
    // Vacío - no expone nada
}
```

Otros providers exponen su cliente:
- **Cosmos:** `CosmosClient`
- **InMemory:** `Store`
- **Relational:** `Connection`

**Problema:** No aprovechamos `FirestoreQueryContext` para exponer `IFirestoreQueryExecutor`.

---

### 5. DbContext Redundante en Executor

`IFirestoreQueryExecutor` recibe `DbContext` como parámetro, pero ya está disponible en `QueryContext.Context`.

**Problema:** Parámetro redundante, indica desconocimiento del pipeline.

---

### 6. Dependencias de EF Core en el AST

`FirestoreQueryExpression` tiene dependencias directas de EF Core:

```csharp
public IEntityType EntityType { get; set; }
public List<IReadOnlyNavigation> PendingIncludes { get; set; }
public List<IncludeInfo> PendingIncludesWithFilters { get; set; }
public List<LambdaExpression> ComplexTypeIncludes { get; set; }
```

**Problema:** El AST no es puro, tiene acoplamiento con tipos internos de EF Core.

---

### 7. Lógica en DTOs del AST

`FirestoreWhereClause` tiene un Visitor embebido:

```csharp
public class FirestoreWhereClause
{
    public Expression ValueExpression { get; set; }

    // ❌ Lógica de evaluación en un DTO
    public object? EvaluateValue(QueryContext queryContext)
    {
        var replacer = new QueryContextParameterReplacer(queryContext);
        // ...
    }

    // ❌ Visitor embebido en un DTO
    private class QueryContextParameterReplacer : ExpressionVisitor { }
}
```

**Problema:** Un DTO no debería tener lógica de evaluación ni visitors internos.

---

### 8. Falta de Translators

Solo existe `FirestoreWhereTranslator`. La lógica de traducción para otras operaciones está dispersa en el Visitor principal.

**Problema:** Inconsistencia en el patrón de traducción, viola SRP.

---

## Translators Necesarios

Para consistencia total, necesitamos un Translator por cada operación LINQ:

| Translator | Qué traduce | Estado actual |
|------------|-------------|---------------|
| `FirestoreWhereTranslator` | Where | ✅ Existe |
| `FirestoreOrderByTranslator` | OrderBy/OrderByDescending/ThenBy | ❌ Disperso en Visitor |
| `FirestoreProjectionTranslator` | Select | ❌ Pendiente |
| `FirestoreLimitTranslator` | Take/TakeLast | ❌ Disperso en Visitor |
| `FirestoreSkipTranslator` | Skip | ❌ Disperso en Visitor |
| `FirestoreIncludeTranslator` | Include/ThenInclude | ❌ Disperso en Visitor |
| `FirestoreIdTranslator` | FirstOrDefault(x => x.Id == ...) | ❌ Disperso en Visitor |
| `FirestoreAggregationTranslator` | Count/Sum/Average/Min/Max/Any | ❌ Disperso en Visitor |

**Principio:** El Visitor solo orquesta, cada Translator traduce una operación específica.

---

## Propuesta de Solución

### Arquitectura Propuesta

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              VISITOR (Orquestador)                          │
│  - Detecta qué operación LINQ se está procesando                            │
│  - Delega a Translators específicos                                         │
│  - NO contiene lógica de traducción                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TRANSLATORS                                     │
│  WhereTranslator, OrderByTranslator, LimitTranslator, SkipTranslator,       │
│  ProjectionTranslator, IncludeTranslator, IdTranslator, AggregationTranslator│
│  - Cada uno traduce una operación específica                                 │
│  - Retornan estructuras del AST                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FirestoreQueryExpression (AST)                       │
│  - Solo almacena datos                                                       │
│  - Puede contener Expressions (para cacheo)                                  │
│  - NO contiene lógica de evaluación                                          │
│  - Se cachea por EF Core                                                     │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SHAPER                                          │
│  - Crea FirestoreQueryingEnumerable                                          │
│  - Pasa QueryContext + AST + AstResolver + Executor                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FirestoreQueryingEnumerable                          │
│  - Orquesta la ejecución                                                     │
│  - Llama a AstResolver.Resolve(AST, QueryContext)                            │
│  - Llama a Executor.Execute(ResolvedQuery)                                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    ▼                                   ▼
┌───────────────────────────────────┐   ┌───────────────────────────────────┐
│           AST RESOLVER            │   │            EXECUTOR               │
│  - Recibe AST + QueryContext      │   │  - Recibe ResolvedFirestoreQuery  │
│  - Evalúa todas las Expressions   │   │  - NO conoce QueryContext         │
│  - Centraliza evaluación          │   │  - NO conoce Expression           │
│  - Retorna ResolvedFirestoreQuery │   │  - Solo construye y ejecuta query │
└───────────────────────────────────┘   └───────────────────────────────────┘
```

---

## Estructuras de Datos

### AST Actual (FirestoreQueryExpression)

Se mantiene para el cacheo de EF Core. Puede contener Expressions.

```csharp
public class FirestoreQueryExpression : Expression
{
    // Dependencias de EF Core (necesarias para el Visitor)
    public IEntityType EntityType { get; set; }
    public List<IReadOnlyNavigation> PendingIncludes { get; set; }

    // Valores híbridos (constante o expresión)
    public int? Limit { get; set; }
    public Expression? LimitExpression { get; set; }

    // Filtros con expresiones
    public List<FirestoreWhereClause> Filters { get; set; }

    // ... resto igual
}
```

### AST Resuelto (ResolvedFirestoreQuery)

Nueva estructura que llega al Executor. Sin dependencias de EF Core ni Expressions.

```csharp
public class ResolvedFirestoreQuery
{
    // Información básica
    public string CollectionName { get; set; }
    public Type EntityClrType { get; set; }

    // Valores ya resueltos (sin Expression)
    public int? Limit { get; set; }
    public int? LimitToLast { get; set; }
    public int? Skip { get; set; }
    public object? IdValue { get; set; }

    // Filtros con valores resueltos
    public List<ResolvedWhereClause> Filters { get; set; }
    public List<ResolvedOrFilterGroup> OrFilterGroups { get; set; }

    // Ordenamiento (ya es puro, no cambia)
    public List<FirestoreOrderByClause> OrderByClauses { get; set; }

    // Cursor (ya es puro, no cambia)
    public FirestoreCursor? StartAfterCursor { get; set; }

    // Includes resueltos
    public List<ResolvedInclude> Includes { get; set; }

    // Agregación (ya es puro, no cambia)
    public FirestoreAggregationType AggregationType { get; set; }
    public string? AggregationPropertyName { get; set; }
    public Type? AggregationResultType { get; set; }

    // Proyección (referencia a estructura existente)
    public FirestoreProjectionDefinition? Projection { get; set; }

    // Flags
    public bool IsIdOnlyQuery => IdValue != null;
    public bool IsAggregation => AggregationType != FirestoreAggregationType.None;
    public bool IsTracking { get; set; }
}
```

### ResolvedWhereClause

```csharp
public class ResolvedWhereClause
{
    public string PropertyName { get; set; }
    public FirestoreOperator Operator { get; set; }
    public object? Value { get; set; }  // Ya evaluado, no Expression
    public Type? EnumType { get; set; }
}
```

### ResolvedInclude

```csharp
public class ResolvedInclude
{
    public string NavigationName { get; set; }
    public Type TargetEntityType { get; set; }
    public string SubCollectionName { get; set; }
    public bool IsCollection { get; set; }
    public bool IsSubCollection { get; set; }

    // Para Filtered Includes
    public Func<object, bool>? FilterPredicate { get; set; }  // Ya compilado
    public int? Take { get; set; }
    public int? Skip { get; set; }

    // Includes anidados
    public List<ResolvedInclude> NestedIncludes { get; set; }
}
```

---

## AstResolver

Nueva clase que centraliza toda la evaluación de Expressions.

```csharp
public class FirestoreAstResolver
{
    public ResolvedFirestoreQuery Resolve(
        FirestoreQueryExpression ast,
        QueryContext queryContext,
        bool isTracking)
    {
        return new ResolvedFirestoreQuery
        {
            CollectionName = ast.CollectionName,
            EntityClrType = ast.EntityType.ClrType,
            IsTracking = isTracking,

            // Resolver valores
            Limit = ResolveLimit(ast, queryContext),
            LimitToLast = ResolveLimitToLast(ast, queryContext),
            Skip = ResolveSkip(ast, queryContext),
            IdValue = ResolveIdValue(ast, queryContext),

            // Resolver filtros
            Filters = ResolveFilters(ast.Filters, queryContext),
            OrFilterGroups = ResolveOrFilterGroups(ast.OrFilterGroups, queryContext),

            // Copiar valores puros
            OrderByClauses = ast.OrderByClauses,
            StartAfterCursor = ast.StartAfterCursor,

            // Resolver includes
            Includes = ResolveIncludes(ast, queryContext),

            // Copiar agregación
            AggregationType = ast.AggregationType,
            AggregationPropertyName = ast.AggregationPropertyName,
            AggregationResultType = ast.AggregationResultType,

            // Copiar proyección
            Projection = ast.Projection
        };
    }

    private int? ResolveLimit(FirestoreQueryExpression ast, QueryContext ctx)
    {
        if (ast.Limit.HasValue) return ast.Limit;
        if (ast.LimitExpression != null) return EvaluateInt(ast.LimitExpression, ctx);
        return null;
    }

    private List<ResolvedWhereClause> ResolveFilters(
        List<FirestoreWhereClause> filters,
        QueryContext ctx)
    {
        return filters.Select(f => new ResolvedWhereClause
        {
            PropertyName = f.PropertyName,
            Operator = f.Operator,
            Value = EvaluateExpression(f.ValueExpression, ctx),
            EnumType = f.EnumType
        }).ToList();
    }

    // ... métodos privados de evaluación
}
```

---

## FirestoreQueryContext Refactorizado

```csharp
public class FirestoreQueryContext : QueryContext
{
    public IFirestoreQueryExecutor QueryExecutor { get; }
    public FirestoreAstResolver AstResolver { get; }

    public FirestoreQueryContext(
        QueryContextDependencies dependencies,
        IFirestoreQueryExecutor queryExecutor,
        FirestoreAstResolver astResolver)
        : base(dependencies)
    {
        QueryExecutor = queryExecutor;
        AstResolver = astResolver;
    }
}
```

---

## Executor Refactorizado

```csharp
public interface IFirestoreQueryExecutor
{
    // Sin QueryContext, sin DbContext redundante
    IAsyncEnumerable<T> ExecuteQueryAsync<T>(
        ResolvedFirestoreQuery query,
        CancellationToken cancellationToken = default) where T : class;

    Task<T> ExecuteAggregationAsync<T>(
        ResolvedFirestoreQuery query,
        CancellationToken cancellationToken = default);
}
```

---

## FirestoreQueryingEnumerable Refactorizado

```csharp
public class FirestoreQueryingEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly FirestoreQueryContext _queryContext;
    private readonly FirestoreQueryExpression _ast;

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
    {
        // 1. Resolver AST
        var resolvedQuery = _queryContext.AstResolver.Resolve(
            _ast,
            _queryContext,
            _isTracking);

        // 2. Ejecutar
        await foreach (var entity in _queryContext.QueryExecutor
            .ExecuteQueryAsync<T>(resolvedQuery, ct))
        {
            yield return entity;
        }
    }
}
```

---

## Plan de Implementación (TDD)

### Reglas de Ejecución

1. **Pedir autorización** después de cada paso (TEST, IMPL, INTEGRAR, VERIFICAR)
2. **Actualizar este documento** marcando el paso completado con [x]
3. **Añadir commit ID** cuando se complete cada tarea
4. **Commitear este documento** después de cada actualización

### Flujo TDD por cada tarea

```
1. TEST     → Escribir tests que fallen     → Pedir autorización
2. IMPL     → Implementar hasta que pasen   → Pedir autorización
3. INTEGRAR → Usar en Visitor               → Pedir autorización
4. VERIFICAR → Tests de integración pasan   → Pedir autorización + Commit
```

---

## FASE 1: Translators

Crear Translators nuevos. Por cada uno seguir el flujo TDD.

### 1.1 FirestoreOrderByTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests del translator | `Tests/Query/Translators/FirestoreOrderByTranslatorTests.cs` |
| IMPL | [x] | Implementar translator | `Query/Translators/FirestoreOrderByTranslator.cs` |
| INTEGRAR | [x] | Usar Translator en Visitor | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [x] | Ejecutar tests de OrderBy existentes | `Tests/Query/OrderByTests.cs` |

**Qué traduce:** `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`

**Lógica movida al Translator:**
- `ExtractPropertyName` - Extrae nombre de propiedad de LambdaExpression
- `BuildPropertyPath` - Construye path para propiedades anidadas (ej: "Address.City")
- Manejo de `UnaryExpression` (Convert) para value types

**Código que PERMANECE en el Visitor (temporalmente):**
- `ExtractPropertyNameFromKeySelector` - Usado por TranslateAverage, TranslateMax, TranslateMin, TranslateSum
- `BuildPropertyPath` - Usado por los métodos de agregación anteriores
- **Se eliminará en 1.6 FirestoreAggregationTranslator** cuando esos métodos deleguen al nuevo Translator

**Cobertura:**
- ANTES: 0% unitaria (solo tests de integración)
- DESPUÉS: 100% unitaria en Translator (12 tests), agregaciones siguen sin cobertura unitaria

**Reorganización de carpetas:**
- Creada carpeta `Query/Ast/` para DTOs (FirestoreOrderByClause, FirestoreWhereClause, etc.)
- Creada carpeta `Query/Translators/` para Translators
- Movido `FirestoreWhereTranslator` de `Visitors/` a `Translators/`

**Commit:** 587860a

---

### 1.1b Refactor AST: Eliminación método Update + Comandos DDD

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [x] | **Eliminar** método `Update` completamente | `Query/Ast/FirestoreQueryExpression.cs` |
| IMPL | [x] | Constructor solo con parámetros obligatorios | `Query/Ast/FirestoreQueryExpression.cs` |
| IMPL | [x] | Propiedades con `protected set` | `Query/Ast/FirestoreQueryExpression.cs` |
| IMPL | [x] | Listas como `IReadOnlyList<T>` con backing field privado | `Query/Ast/FirestoreQueryExpression.cs` |
| IMPL | [x] | Comandos modifican directamente y retornan `this` (Fluent API) | `Query/Ast/FirestoreQueryExpression.cs` |
| INTEGRAR | [x] | Actualizar Visitor para usar comandos | `Query/Visitors/*.cs` |
| INTEGRAR | [x] | Actualizar Executor para aceptar `IReadOnlyList<T>` | `Query/FirestoreQueryExecutor.cs` |
| REFACTOR | [x] | Unificar `TranslateOrderBy`/`TranslateThenBy` en `TranslateOrderByCore` | `Query/Visitors/...Visitor.cs` |
| VERIFICAR | [x] | Todos los tests pasan | 642 unit + 172 integration |

**Qué cambió:**
- El método `Update` con 20+ parámetros fue **ELIMINADO** (no solo privado)
- Constructor solo acepta `(IEntityType entityType, string collectionName)`
- Properties con `protected set` para permitir herencia
- Listas usan patrón inmutable: `private readonly List<T> _field` + `public IReadOnlyList<T> Field => _field`
- Comandos modifican la instancia directamente y retornan `this` (Fluent API)
- `TranslateOrderBy` y `TranslateThenBy` unificados en un helper `TranslateOrderByCore(isFirst: bool)`

**Comandos disponibles en FirestoreQueryExpression:**
- `AddFilter(filter)` - Agrega un filtro WHERE
- `AddFilters(filters)` - Agrega múltiples filtros WHERE
- `AddOrFilterGroup(group)` - Agrega un grupo OR
- `SetOrderBy(orderBy)` - Reemplaza todos los ordenamientos (OrderBy)
- `AddOrderBy(orderBy)` - Agrega ordenamiento (ThenBy)
- `WithLimit(limit)` / `WithLimitExpression(expr)` - Limit
- `WithLimitToLast(limit)` / `WithLimitToLastExpression(expr)` - LimitToLast
- `WithSkip(skip)` / `WithSkipExpression(expr)` - Skip
- `WithStartAfter(cursor)` - Paginación con cursor
- `WithIdValueExpression(expr)` - Query por ID único
- `ClearIdValueExpressionWithFilters(filters)` - Convierte IdOnlyQuery a normal
- `AddInclude(navigation)` - Agrega navegación Include
- `AddIncludeWithFilters(includeInfo)` - Agrega Include con filtros
- `WithComplexTypeIncludes(includes)` - Includes en ComplexTypes
- `WithProjection(projection)` - Proyección Select
- `WithCount()`, `WithAny()`, `WithSum()`, `WithAverage()`, `WithMin()`, `WithMax()` - Agregaciones

**Beneficio:**
- El AST ya no puede ser modificado de formas inesperadas
- Cada modificación es explícita y nombrada según su intención de negocio
- Código duplicado en Visitor eliminado (OrderBy/ThenBy)
- Patrón DDD aplicado correctamente al agregado

**Commit:** 4c421d9

---

### 1.1c Patrón MicroDomain: Partial Classes para Features del AST

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [x] | Hacer `FirestoreQueryExpression` partial class | `Query/Ast/FirestoreQueryExpression.cs` |
| IMPL | [x] | Crear feature file OrderBy con Record + Commands + TranslateOrderBy | `Query/Ast/FirestoreQueryExpression_OrderBy.cs` |
| INTEGRAR | [x] | Actualizar Visitor con one-liners | `Query/Visitors/...Visitor.cs` |
| VERIFICAR | [x] | Todos los tests pasan | 642 unit + 172 integration |

**Patrón MicroDomain aplicado:**

El AST ahora usa partial classes donde cada feature tiene su propio archivo:

```
Query/Ast/
├── FirestoreQueryExpression.cs           ← DTO: todas las propiedades + constructor
├── FirestoreQueryExpression_OrderBy.cs   ← Record + Commands + TranslateOrderBy static
├── FirestoreQueryExpression_Limit.cs     ← (pendiente) Record + Commands + TranslateLimit static
├── FirestoreQueryExpression_Filter.cs    ← (pendiente) Record + Commands + TranslateFilter static
└── ...
```

**Estructura del feature file (OrderBy como ejemplo):**

```csharp
// Record para agrupar parámetros
public record TranslateOrderByRequest(
    ShapedQueryExpression Source,
    LambdaExpression KeySelector,
    bool Ascending,
    bool IsFirst);

public partial class FirestoreQueryExpression
{
    #region OrderBy Commands
    public FirestoreQueryExpression SetOrderBy(FirestoreOrderByClause orderBy) { ... }
    public FirestoreQueryExpression AddOrderBy(FirestoreOrderByClause orderBy) { ... }
    #endregion

    #region OrderBy Translation
    public static ShapedQueryExpression? TranslateOrderBy(TranslateOrderByRequest request) { ... }
    #endregion
}
```

**Visitor con one-liners:**

```csharp
protected override ShapedQueryExpression? TranslateOrderBy(...)
    => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: true));

protected override ShapedQueryExpression? TranslateThenBy(...)
    => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: false));
```

**Beneficios:**
- Propiedades en DTO base: ves todo el AST de un vistazo
- Cada feature en su propio archivo: fácil de encontrar y mantener
- Record agrupa parámetros: evita métodos con muchos parámetros
- Visitor reducido a one-liners: solo orquesta, no contiene lógica
- Translator separado (FirestoreOrderByTranslator): reutilizable para queries, includes, proyecciones

**Commit:** 92ccf2f

---

### 1.2 FirestoreLimitTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests del translator | `Tests/Query/Translators/FirestoreLimitTranslatorTests.cs` |
| TEST | [x] | Crear tests del feature file | `Tests/Query/Ast/FirestoreQueryExpression_LimitTests.cs` |
| IMPL | [x] | Implementar translator | `Query/Translators/FirestoreLimitTranslator.cs` |
| IMPL | [x] | Crear feature file con Record + Commands + TranslateLimit | `Query/Ast/FirestoreQueryExpression_Limit.cs` |
| INTEGRAR | [x] | Actualizar Visitor con one-liners | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| INTEGRAR | [x] | Mover commands del DTO base al feature file | `Query/Ast/FirestoreQueryExpression.cs` |
| VERIFICAR | [x] | Ejecutar todos los tests | 682 unit + 172 integration |

**Qué traduce:** `Take`, `TakeLast`

**Patrón MicroDomain aplicado:**
- Record `TranslateLimitRequest(Source, Count, IsLimitToLast)` agrupa parámetros
- Commands `WithLimit`, `WithLimitExpression`, `WithLimitToLast`, `WithLimitToLastExpression` movidos al feature file
- `TranslateLimit` estático usa `FirestoreLimitTranslator` para extraer el valor
- Visitor reducido a one-liners

**Nota sobre ExtractIntConstant:**
- Se mantiene en el Visitor para `TranslateSkip` (será movido en 1.3)
- `FirestoreLimitTranslator` tiene su propia implementación
- En 1.3 `FirestoreSkipTranslator` heredará de `FirestoreLimitTranslator` para reutilizar la lógica

**Commit:** 7b921fc

---

### 1.3 FirestoreSkipTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests del translator | `Tests/Query/Translator/FirestoreSkipTranslatorTests.cs` |
| TEST | [x] | Crear tests del feature file | `Tests/Query/Ast/FirestoreQueryExpression_SkipTests.cs` |
| IMPL | [x] | Implementar translator | `Query/Translators/FirestoreSkipTranslator.cs` |
| IMPL | [x] | Crear feature file con Record + Commands + TranslateSkip | `Query/Ast/FirestoreQueryExpression_Skip.cs` |
| INTEGRAR | [x] | Actualizar Visitor con one-liner | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| INTEGRAR | [x] | Mover commands del DTO base al feature file | `Query/Ast/FirestoreQueryExpression.cs` |
| INTEGRAR | [x] | Eliminar ExtractIntConstant del Visitor | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [x] | Ejecutar todos los tests | 706 unit + 172 integration |

**Qué traduce:** `Skip`

**Patrón MicroDomain aplicado:**
- Record `TranslateSkipRequest(Source, Count)` agrupa parámetros
- Commands `WithSkip`, `WithSkipExpression` movidos al feature file
- `TranslateSkip` estático usa `FirestoreSkipTranslator` para extraer el valor
- `FirestoreSkipTranslator` hereda de `FirestoreLimitTranslator` (reutiliza `ExtractIntConstant`)
- Visitor reducido a one-liner
- `ExtractIntConstant` eliminado del Visitor (ya no se usa)

**Commit:** f4bce71

---

### 1.4 FirestoreIdTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Crear tests del translator | `Tests/Query/Translators/FirestoreIdTranslatorTests.cs` |
| IMPL | [ ] | Implementar translator | `Query/Translators/FirestoreIdTranslator.cs` |
| INTEGRAR | [ ] | Mover lógica del Visitor al Translator | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [ ] | Ejecutar tests de queries por Id | `Tests/Query/IdQueryTests.cs` |

**Qué traduce:** `FirstOrDefault(x => x.Id == ...)`, `SingleOrDefault(x => x.Id == ...)`

**Commit:**

---

### 1.4b Query Slices: FirstOrDefault, SingleOrDefault, Any, Count, DefaultIfEmpty

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests de FirstOrDefault | `Tests/Query/Ast/FirestoreQueryExpression_FirstOrDefaultTests.cs` |
| TEST | [x] | Crear tests de SingleOrDefault | `Tests/Query/Ast/FirestoreQueryExpression_SingleOrDefaultTests.cs` |
| TEST | [x] | Crear tests de Any | `Tests/Query/Ast/FirestoreQueryExpression_AnyTests.cs` |
| TEST | [x] | Crear tests de Count | `Tests/Query/Ast/FirestoreQueryExpression_CountTests.cs` |
| TEST | [x] | Crear tests de DefaultIfEmpty | `Tests/Query/Ast/FirestoreQueryExpression_DefaultIfEmptyTests.cs` |
| TEST | [x] | Crear integration tests de DefaultIfEmpty (Skip) | `Tests/Query/DefaultIfEmptyTests.cs` |
| IMPL | [x] | Feature file FirstOrDefault (Id optimization, ReturnDefault, Limit 1) | `Query/Ast/FirestoreQueryExpression_FirstOrDefault.cs` |
| IMPL | [x] | Feature file SingleOrDefault (Limit 2 for duplicate detection) | `Query/Ast/FirestoreQueryExpression_SingleOrDefault.cs` |
| IMPL | [x] | Feature file Any (FirestoreWhereTranslator for predicates) | `Query/Ast/FirestoreQueryExpression_Any.cs` |
| IMPL | [x] | Feature file Count (FirestoreWhereTranslator for predicates) | `Query/Ast/FirestoreQueryExpression_Count.cs` |
| IMPL | [x] | Feature file DefaultIfEmpty (stores default value expression) | `Query/Ast/FirestoreQueryExpression_DefaultIfEmpty.cs` |
| INTEGRAR | [x] | Visitor methods convertidos a one-liners | `Query/Visitors/...Visitor.cs` |
| VERIFICAR | [x] | Todos los tests pasan | 761 unit tests |

**Patrón MicroDomain aplicado:**

Cada slice tiene su propio feature file con:
- Record para parámetros (ej: `TranslateFirstOrDefaultRequest`)
- Propiedades específicas del feature
- Commands específicos (`WithX` methods)
- Método estático `TranslateX` que encapsula la lógica

**Estructura de cada feature file:**

```csharp
// Record para agrupar parámetros del Visitor
public record TranslateXRequest(...);

public partial class FirestoreQueryExpression
{
    #region X Properties
    // Propiedades específicas del feature
    #endregion

    #region X Commands
    // Métodos WithX que modifican el estado
    #endregion

    #region X Translation
    public static ShapedQueryExpression? TranslateX(TranslateXRequest request) { ... }
    #endregion
}
```

**Visitor con one-liners:**

```csharp
protected override ShapedQueryExpression? TranslateFirstOrDefault(...)
    => FirestoreQueryExpression.TranslateFirstOrDefault(new(source, predicate, returnType, returnDefault));

protected override ShapedQueryExpression? TranslateSingleOrDefault(...)
    => FirestoreQueryExpression.TranslateSingleOrDefault(new(source, predicate, returnType, returnDefault));

protected override ShapedQueryExpression? TranslateAny(...)
    => FirestoreQueryExpression.TranslateAny(new(source, predicate));

protected override ShapedQueryExpression? TranslateCount(...)
    => FirestoreQueryExpression.TranslateCount(new(source, predicate));

protected override ShapedQueryExpression? TranslateDefaultIfEmpty(...)
    => FirestoreQueryExpression.TranslateDefaultIfEmpty(new(source, defaultValue));
```

**Detalles de implementación:**

- **FirstOrDefault**: Id optimization (GetDocumentAsync), ReturnDefault property, Limit 1
- **SingleOrDefault**: Limit 2 para detectar duplicados (no puede usar Id optimization)
- **Any**: Usa FirestoreWhereTranslator para traducir predicados
- **Count**: Usa FirestoreWhereTranslator para traducir predicados
- **DefaultIfEmpty**: Almacena DefaultValueExpression para uso del Executor

**Commit:** e820da7

---

### 1.4c Query Slice: Where

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests de Where | `Tests/Query/Ast/FirestoreQueryExpression_WhereTests.cs` |
| IMPL | [x] | Feature file Where | `Query/Ast/FirestoreQueryExpression_Where.cs` |
| INTEGRAR | [x] | Visitor delega a slice (casi one-liner) | `Query/Visitors/...Visitor.cs` |
| VERIFICAR | [x] | Todos los tests pasan | 772 unit tests |

**Detalles de implementación:**

- `TranslateWhereRequest(Source, PredicateBody)` - recibe el body ya preprocesado por RuntimeParameterReplacer
- Incluye `PreprocessArrayContainsPatterns` para detectar patrones de array
- Incluye `ExtractPropertyNameFromEFPropertyChain` (duplicado pendiente de limpieza en 5.4)
- Maneja Id optimization (IdOnlyQuery)
- Maneja AND/OR expressions
- Convierte IdOnlyQuery a normal query cuando se añaden más filtros

**Visitor resultante (casi one-liner):**

```csharp
protected override ShapedQueryExpression? TranslateWhere(...)
{
    var parameterReplacer = new RuntimeParameterReplacer(QueryCompilationContext);
    var evaluatedBody = parameterReplacer.Visit(predicate.Body);
    return FirestoreQueryExpression.TranslateWhere(new(source, evaluatedBody));
}
```

**Nota:** El Visitor mantiene `RuntimeParameterReplacer` porque depende de `QueryCompilationContext`. También mantiene `PreprocessArrayContainsPatterns` porque se usa en `Visit()`. La limpieza de duplicados se hará en Fase 5.4.

**Commit:** f4e3669

---

### 1.5 FirestoreIncludeTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Crear tests del translator | `Tests/Query/Translators/FirestoreIncludeTranslatorTests.cs` |
| IMPL | [ ] | Implementar translator | `Query/Translators/FirestoreIncludeTranslator.cs` |
| INTEGRAR | [ ] | Mover lógica del Visitor al Translator | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [ ] | Ejecutar tests de Include existentes | `Tests/Query/IncludeTests.cs` |

**Qué traduce:** `Include`, `ThenInclude`, Filtered Includes

**Commit:**

---

### 1.6 FirestoreAggregationTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Crear tests del translator | `Tests/Query/Translators/FirestoreAggregationTranslatorTests.cs` |
| IMPL | [ ] | Implementar translator | `Query/Translators/FirestoreAggregationTranslator.cs` |
| INTEGRAR | [ ] | Mover lógica del Visitor al Translator | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [ ] | Ejecutar tests de agregación existentes | `Tests/Query/AggregationTests.cs` |

**Qué traduce:** `Count`, `Any`, `Sum`, `Average`, `Min`, `Max`

**IMPORTANTE - Limpieza pendiente de 1.1:**
Al completar esta tarea, ELIMINAR del Visitor:
- `ExtractPropertyNameFromKeySelector` (líneas ~400-418)
- `BuildPropertyPath` (líneas ~425-439)

Estos métodos quedaron temporalmente en el Visitor porque `TranslateAverage`, `TranslateMax`, `TranslateMin`, `TranslateSum` los usan. Una vez que estos métodos deleguen al `FirestoreAggregationTranslator`, el código duplicado se puede eliminar.

**Commit:**

---

### 1.7 FirestoreProjectionTranslator

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Crear tests del translator | `Tests/Query/Translators/FirestoreProjectionTranslatorTests.cs` |
| IMPL | [ ] | Implementar translator | `Query/Translators/FirestoreProjectionTranslator.cs` |
| INTEGRAR | [ ] | Mover lógica del Visitor al Translator | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| VERIFICAR | [ ] | Habilitar tests de Select (actualmente en Skip) | `Tests/Query/SelectTests.cs` |

**Qué traduce:** `Select` (proyecciones)

**Commit:**

---

## FASE 2: Estructuras Resueltas

Solo después de que TODOS los Translators estén funcionando.

### 2.1 ResolvedFirestoreQuery

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Tests de construcción y propiedades | `Tests/Query/ResolvedFirestoreQueryTests.cs` |
| IMPL | [ ] | Crear estructura | `Query/ResolvedFirestoreQuery.cs` |

**Commit:**

---

### 2.2 ResolvedWhereClause

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Tests de construcción | `Tests/Query/ResolvedWhereClauseTests.cs` |
| IMPL | [ ] | Crear estructura | `Query/ResolvedWhereClause.cs` |

**Commit:**

---

### 2.3 ResolvedInclude

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Tests de construcción | `Tests/Query/ResolvedIncludeTests.cs` |
| IMPL | [ ] | Crear estructura | `Query/ResolvedInclude.cs` |

**Commit:**

---

## FASE 3: AstResolver

### 3.1 FirestoreAstResolver

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Tests de resolución de cada tipo de Expression | `Tests/Query/FirestoreAstResolverTests.cs` |
| IMPL | [ ] | Implementar resolver | `Query/FirestoreAstResolver.cs` |
| INTEGRAR | [ ] | Mover lógica de evaluación del Executor y DTOs | Varios |
| VERIFICAR | [ ] | Todos los tests de integración pasan | `Tests/Query/*` |

**Qué mueve:**
- `EvaluateIntExpression` del Executor
- `EvaluateIdExpression` del Executor
- `EvaluateValue` de `FirestoreWhereClause`
- `CompileFilterPredicate` del Executor y Shaper

**Commit:**

---

## FASE 4: Refactorizar Executor

### 4.1 Cambiar firma del Executor

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Actualizar tests del Executor | `Tests/Query/FirestoreQueryExecutorTests.cs` |
| IMPL | [ ] | Cambiar para recibir `ResolvedFirestoreQuery` | `Query/FirestoreQueryExecutor.cs` |
| INTEGRAR | [ ] | Actualizar `FirestoreQueryingEnumerable` | `Query/FirestoreQueryingEnumerable.cs` |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/Query/*` |

**Commit:**

---

### 4.2 Refactorizar FirestoreQueryContext

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [ ] | Tests del QueryContext | `Tests/Query/FirestoreQueryContextTests.cs` |
| IMPL | [ ] | Exponer `IFirestoreQueryExecutor` y `AstResolver` | `Query/FirestoreQueryContext.cs` |
| INTEGRAR | [ ] | Actualizar Factory | `Query/FirestoreQueryContextFactory.cs` |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/Query/*` |

**Commit:**

---

## FASE 5: Limpieza

### 5.1 Limpiar DTOs del AST

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [ ] | Eliminar `EvaluateValue()` | `Query/FirestoreWhereClause.cs` |
| IMPL | [ ] | Eliminar `QueryContextParameterReplacer` | `Query/FirestoreWhereClause.cs` |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/Query/*` |

**Commit:**

---

### 5.2 Limpiar código duplicado

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [ ] | Eliminar `CompileFilterPredicate` duplicado del Shaper | `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` |
| IMPL | [ ] | Eliminar métodos de evaluación del Executor | `Query/FirestoreQueryExecutor.cs` |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/Query/*` |

**Commit:**

---

### 5.3 Centralizar IsVowel/Pluralize

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [ ] | Eliminar `IsVowel` y `Pluralize` duplicados | `Query/FirestoreQueryExecutor.cs`, `Query/Visitors/*.cs` |
| IMPL | [ ] | Usar `FirestoreCollectionManager` como única fuente | Varios |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/*` |

**Código duplicado en:**
- `FirestoreCollectionManager.cs` (original)
- `FirestoreQueryExecutor.cs`
- `FirestoreQueryableMethodTranslatingExpressionVisitor.cs`
- `FirestoreShapedQueryCompilingExpressionVisitor.cs`

**Commit:**

---

### 5.4 ArrayContainsPatternTransformer (Patrón de Transformer Testeable)

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| TEST | [x] | Crear tests unitarios del Transformer | `Tests/Query/Preprocessing/ArrayContainsPatternTransformerTests.cs` |
| IMPL | [x] | Crear clase estática Transformer | `Query/Preprocessing/ArrayContainsPatternTransformer.cs` |
| INTEGRAR | [x] | Usar Transformer en Visitor.Visit() | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| IMPL | [x] | Eliminar código duplicado del Visitor | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| IMPL | [x] | Eliminar código duplicado del Translator | `Query/Translators/FirestoreWhereTranslator.cs` |
| IMPL | [x] | Eliminar código muerto del Slice Where | `Query/Ast/FirestoreQueryExpression_Where.cs` |
| VERIFICAR | [x] | Todos los tests pasan | 777 unit + 172 integration |

**Problema detectado:**

Código duplicado para transformar patrones de array Contains en 3 ubicaciones:
1. `Visitor.PreprocessArrayContainsPatterns()` - ~140 líneas
2. `FirestoreQueryExpression_Where.PreprocessArrayContainsPatterns()` - ~230 líneas (código muerto)
3. `FirestoreWhereTranslator` Case 4 + `ExtractPropertyNameFromEFPropertyChain` - ~37 líneas

**Solución aplicada - Patrón Transformer Testeable:**

```csharp
// Query/Preprocessing/ArrayContainsPatternTransformer.cs
public static class ArrayContainsPatternTransformer
{
    /// <summary>
    /// Transforms array Contains patterns into Firestore marker expressions.
    /// Returns the original expression if no patterns are found.
    /// </summary>
    public static Expression Transform(Expression expression)
    {
        // Recursively transforms:
        // - EF.Property<List<T>>().AsQueryable().Contains(value) → FirestoreArrayContainsExpression
        // - array.Any(t => list.Contains(t)) → FirestoreArrayContainsAnyExpression
    }
}
```

**Uso en el Visitor (one-liner):**

```csharp
public override Expression? Visit(Expression? expression)
{
    if (expression == null) return null;
    var preprocessed = ArrayContainsPatternTransformer.Transform(expression);
    return base.Visit(preprocessed);
}
```

**Código eliminado:**
- Del Visitor: `PreprocessArrayContainsPatterns`, `VisitMethodCall` (duplicado), `ExtractPropertyNameFromEFPropertyChain`, `ExtractListFromContainsPredicate`, `IsParameterReference` (~200 líneas)
- Del Slice Where: `PreprocessArrayContainsPatterns` y helpers (~230 líneas código muerto)
- Del Translator: Case 4 y `ExtractPropertyNameFromEFPropertyChain` (~37 líneas)

**Patrón reutilizable:**

Este patrón se puede aplicar cuando:
1. Hay lógica de transformación de expresiones embebida en el Visitor
2. La lógica no tiene dependencias de estado del Visitor
3. Se necesita testear la transformación de forma aislada

Estructura:
```
Query/Preprocessing/
├── ArrayContainsPatternTransformer.cs   ← Transformer testeable
└── [OtroTransformer].cs                 ← Futuros transformers

Tests/Query/Preprocessing/
├── ArrayContainsPatternTransformerTests.cs
└── [OtroTransformerTests].cs
```

**Commit:** 2d77c0e

---

### 5.4b Unificar BuildPropertyPath (pendiente)

| Paso | Estado | Acción | Archivo |
|------|--------|--------|---------|
| IMPL | [ ] | Mover `BuildPropertyPath` a clase helper | `Query/Helpers/PropertyExtractionHelper.cs` |
| IMPL | [ ] | Eliminar duplicado del Visitor | `Query/Visitors/FirestoreQueryableMethodTranslatingExpressionVisitor.cs` |
| IMPL | [ ] | Eliminar duplicado del Translator | `Query/Translators/FirestoreWhereTranslator.cs` |
| VERIFICAR | [ ] | Todos los tests pasan | `Tests/Query/*` |

**Código duplicado - BuildPropertyPath:**

```csharp
// Visitor (ExtractPropertyNameFromKeySelector)
private string BuildPropertyPath(MemberExpression memberExpr)
{
    var parts = new List<string>();
    Expression? current = memberExpr;
    while (current is MemberExpression member)
    {
        parts.Add(member.Member.Name);
        current = member.Expression;
    }
    parts.Reverse();
    return string.Join(".", parts);
}

// Translator (TranslateBinaryExpression) - IDÉNTICO
private string BuildPropertyPath(MemberExpression memberExpr) { /* mismo código */ }
```

**Solución propuesta:**

```csharp
// Query/Helpers/PropertyExtractionHelper.cs
public static class PropertyExtractionHelper
{
    /// <summary>
    /// Construye path completo de propiedades anidadas (ComplexTypes)
    /// Ej: e.Direccion.Ciudad → "Direccion.Ciudad"
    /// </summary>
    public static string BuildPropertyPath(MemberExpression memberExpr) { ... }
}
```

**Commit:**

---

### 5.5 Análisis de QueryContextParameterReplacer vs RuntimeParameterReplacer

**NO son duplicados** - tienen propósitos opuestos en el pipeline:

| Clase | Ubicación | Momento | Propósito |
|-------|-----------|---------|-----------|
| `RuntimeParameterReplacer` | `Visitors/RuntimeParameterReplacer.cs` | Compilación | Transforma `__p_X` → `QueryContext.ParameterValues["__p_X"]` (construye expresión) |
| `QueryContextParameterReplacer` | `Ast/FirestoreWhereClause.cs` línea 100 | Ejecución | Evalúa `QueryContext.ParameterValues["__p_X"]` → valor real |

**Flujo:**

```
COMPILACIÓN (se cachea):
  x.Name == nombre
      ↓ RuntimeParameterReplacer
  x.Name == QueryContext.ParameterValues["__p_0"]

EJECUCIÓN (cada request):
  QueryContext.ParameterValues["__p_0"]
      ↓ QueryContextParameterReplacer (en EvaluateValue)
  "Juan"
```

**Problema arquitectónico:**
- `EvaluateValue()` y `QueryContextParameterReplacer` están en un DTO (`FirestoreWhereClause`)
- Esto viola SRP: un DTO no debería tener lógica de evaluación

**Solución (ya documentada en 3.1 y 5.1):**
- Mover `EvaluateValue()` al `FirestoreAstResolver` (Fase 3.1)
- Eliminar `QueryContextParameterReplacer` de `FirestoreWhereClause` (Fase 5.1)

**Nota:** `RuntimeParameterReplacer` permanece en el Visitor porque opera en tiempo de compilación del query.

---

## Resumen de Orden

```
FASE 1: Translators (7 tareas)
  1.1 FirestoreOrderByTranslator      ← EMPEZAR AQUÍ
  1.2 FirestoreLimitTranslator
  1.3 FirestoreSkipTranslator
  1.4 FirestoreIdTranslator
  1.5 FirestoreIncludeTranslator
  1.6 FirestoreAggregationTranslator
  1.7 FirestoreProjectionTranslator

FASE 2: Estructuras (3 tareas)
  2.1 ResolvedFirestoreQuery
  2.2 ResolvedWhereClause
  2.3 ResolvedInclude

FASE 3: AstResolver (1 tarea)
  3.1 FirestoreAstResolver

FASE 4: Refactorizar (2 tareas)
  4.1 Cambiar firma del Executor
  4.2 Refactorizar FirestoreQueryContext

FASE 5: Limpieza (4 tareas)
  5.1 Limpiar DTOs del AST (EvaluateValue, QueryContextParameterReplacer)
  5.2 Limpiar código duplicado (CompileFilterPredicate, métodos evaluación)
  5.3 Centralizar IsVowel/Pluralize
  5.4 Unificar ExtractPropertyNameFromEFPropertyChain y BuildPropertyPath
```

---

## Comando para empezar

Cuando quieras empezar, di:

> "Haz 1.1 FirestoreOrderByTranslator - TEST"

Y seguiré el flujo TDD para esa tarea específica.

---

## Beneficios

1. **Separación de responsabilidades clara:**
   - Visitor: orquesta
   - Translators: traducen
   - AST: almacena
   - AstResolver: evalúa
   - Executor: ejecuta

2. **Executor desacoplado de EF Core:**
   - No conoce `QueryContext`
   - No conoce `Expression`
   - No conoce `IEntityType`/`IReadOnlyNavigation`

3. **Lógica de evaluación centralizada:**
   - Un solo lugar para resolver Expressions
   - Sin código duplicado

4. **DTOs puros:**
   - Sin lógica embebida
   - Fáciles de testear

5. **Consistencia en Translators:**
   - Un Translator por operación LINQ
   - Patrón uniforme

---

## Notas

- El AST híbrido (`Limit`/`LimitExpression`) se mantiene por el modelo de cacheo de EF Core
- `ResolvedFirestoreQuery` es la "vista materializada" del AST para el Executor
- El `AstResolver` actúa como puente entre el mundo de EF Core y el mundo de Firestore
