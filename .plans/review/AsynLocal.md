# Investigación: AsyncLocal vs Cast Directo en EF Core Providers

## Contexto

Se investigó cómo los providers oficiales de EF Core (Cosmos DB, InMemory, SQL Server) comunican datos entre las diferentes fases del pipeline de queries, específicamente entre `QueryTranslationPreprocessor` y `ShapedQueryCompilingExpressionVisitor`.

## Hallazgo Principal

**Los providers oficiales NO usan `AsyncLocal`**. En su lugar, hacen un **cast directo** del `QueryCompilationContext` base a su tipo derivado.

### Evidencia del Provider de Cosmos DB

```csharp
// Fuente: efcore/src/EFCore.Cosmos/Query/Internal/CosmosQueryableMethodTranslatingExpressionVisitorFactory.cs
public virtual QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
    => new CosmosQueryableMethodTranslatingExpressionVisitor(
        Dependencies,
        (CosmosQueryCompilationContext)queryCompilationContext,  // ← CAST DIRECTO
        sqlExpressionFactory,
        typeMappingSource,
        memberTranslatorProvider,
        methodCallTranslatorProvider);
```

### Registros de Servicios en Cosmos

```csharp
// Fuente: efcore/src/EFCore.Cosmos/Extensions/CosmosServiceCollectionExtensions.cs
.TryAdd<IQueryCompilationContextFactory, CosmosQueryCompilationContextFactory>()
.TryAdd<IQueryTranslationPreprocessorFactory, CosmosQueryTranslationPreprocessorFactory>()
.TryAdd<IShapedQueryCompilingExpressionVisitorFactory, CosmosShapedQueryCompilingExpressionVisitorFactory>()
```

## Por Qué Funciona el Cast

1. El provider registra su propia `IQueryCompilationContextFactory` (ej: `CosmosQueryCompilationContextFactory`)
2. Esta factory crea instancias del tipo derivado (ej: `CosmosQueryCompilationContext`)
3. EF Core pasa esta instancia a través de todo el pipeline
4. Aunque el parámetro está tipado como `QueryCompilationContext` (base), el objeto real ES el tipo derivado
5. El cast es seguro porque el provider controla qué factory está registrada

## Solución Recomendada para Firestore Provider

### 1. Crear el QueryCompilationContext derivado

```csharp
public class FirestoreQueryCompilationContext : QueryCompilationContext
{
    public FirestoreQueryCompilationContext(
        QueryCompilationContextDependencies dependencies,
        bool async)
        : base(dependencies, async)
    {
    }

    /// <summary>
    /// Almacena los Include de ComplexTypes extraídos durante el preprocesamiento
    /// para ser utilizados durante la materialización.
    /// </summary>
    public List<LambdaExpression> ComplexTypeIncludes { get; } = new();
}
```

### 2. En el Visitor de Preprocesamiento

```csharp
internal class ComplexTypeIncludeExtractorVisitor : ExpressionVisitor
{
    private readonly FirestoreQueryCompilationContext _firestoreContext;

    public ComplexTypeIncludeExtractorVisitor(QueryCompilationContext queryCompilationContext)
    {
        // Cast directo - igual que Cosmos DB
        _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
    }

    private void StoreComplexTypeInclude(LambdaExpression includeExpression)
    {
        _firestoreContext.ComplexTypeIncludes.Add(includeExpression);
    }
}
```

### 3. En el ShapedQueryCompilingExpressionVisitor

```csharp
public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
{
    private readonly FirestoreQueryCompilationContext _firestoreContext;

    public FirestoreShapedQueryCompilingExpressionVisitor(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext)
        : base(dependencies, queryCompilationContext)
    {
        _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
    }

    // Acceder a los includes almacenados:
    // var complexTypeIncludes = _firestoreContext.ComplexTypeIncludes;
}
```

## Comparativa

| Aspecto | AsyncLocal | Cast Directo |
|---------|------------|--------------|
| Usado por providers oficiales | ❌ No | ✅ Sí |
| Thread-safe | ✅ | ✅ (mismo contexto de ejecución) |
| Acoplamiento | ⚠️ Implícito/global | ✅ Explícito |
| Debugging | ⚠️ Difícil de rastrear | ✅ Flujo claro |
| Mantenibilidad | ⚠️ "Magia" oculta | ✅ Patrón estándar de EF Core |

## Conclusión

**Eliminar `AsyncLocal` y usar el patrón de cast directo** que utilizan los providers oficiales. Es más limpio, más mantenible, y es el patrón establecido por el equipo de EF Core.

El cast es seguro porque:
1. Controlamos el registro de `FirestoreQueryCompilationContextFactory`
2. EF Core garantiza que la misma instancia de `QueryCompilationContext` fluye por todo el pipeline de una query
3. Es exactamente lo que hacen Cosmos DB, SQL Server y otros providers oficiales

## Referencias

- [CosmosQueryableMethodTranslatingExpressionVisitorFactory.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosQueryableMethodTranslatingExpressionVisitorFactory.cs)
- [CosmosServiceCollectionExtensions.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Extensions/CosmosServiceCollectionExtensions.cs)