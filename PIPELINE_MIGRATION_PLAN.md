# Plan de Migración del Pipeline de Queries

## Resumen Ejecutivo

Reemplazar `ConvertHandler` (que usa `IProjectionMaterializer` + `IFirestoreDocumentDeserializer`) por `SnapshotShapingHandler` (que usa `ISnapshotShaper` + `IMaterializer`).

---

## Estado Actual del Pipeline

### Orden de Handlers (FirestoreServiceCollectionExtensions.cs)

```
ErrorHandling → Resolver → Log → Proxy → Tracking → Convert → SnapshotShaping → Execution
```

### Flujo de Resultados (inverso)

```
Execution (retorna snapshots/scalar)
    ↓
SnapshotShaping (solo debug actualmente)
    ↓
Convert (materializa con ProjectionMaterializer/DocumentDeserializer)
    ↓
Tracking (trackea entidades)
    ↓
Proxy (wrappea si hay proxies)
    ↓
Log → Resolver → ErrorHandling → Usuario
```

### Tipos de PipelineResult

| Tipo | Origen | Descripción |
|------|--------|-------------|
| `Scalar` | ExecutionHandler | Count, Any, Sum, Average, Min, Max |
| `Materialized` | ExecutionHandler | Entidades y Proyecciones (snapshots en metadata) |

---

## Componentes Actuales

### A Eliminar

| Componente | Líneas | Responsabilidad |
|------------|--------|-----------------|
| `ConvertHandler` | ~265 | Orquesta conversión Scalar + Materialized |
| `IProjectionMaterializer` | - | Interfaz |
| `ProjectionMaterializer` | ~950 | Materializa proyecciones (complejo, problemático) |
| `IFirestoreDocumentDeserializer` | - | Interfaz |
| `FirestoreDocumentDeserializer` | ~??? | Deserializa entidades |

### A Mantener/Promover

| Componente | Responsabilidad |
|------------|-----------------|
| `ISnapshotShaper` / `SnapshotShaper` | Shapea snapshots en diccionarios jerárquicos |
| `IMaterializer` / `Materializer` | Convierte diccionarios en instancias CLR |
| `ITypeConverter` / `FirestoreTypeConverter` | Convierte tipos escalares (Firestore → CLR) |

---

## Nuevo Pipeline Propuesto

### Orden de Handlers

```
ErrorHandling → Resolver → Log → Proxy → Tracking → SnapshotShaping → Execution
```

**Nota:** `ConvertHandler` se elimina. `SnapshotShapingHandler` asume su rol.

### Flujo de Resultados

```
Execution
    ├── Aggregations (Count, Any, Sum, Avg, Min, Max) → Scalar
    └── Entity/Projection queries → Materialized (snapshots en metadata)
           ↓
SnapshotShaping
    ├── Scalar → Convierte tipo con ITypeConverter → retorna Scalar
    └── Materialized → Shape + Materialize → retorna Materialized (items)
           ↓
Tracking → Proxy → Log → Resolver → ErrorHandling → Usuario
```

---

## Manejo de Escalares

### Origen
Los escalares se generan en `ExecutionHandler`:

```csharp
// Aggregations: Count, Any, Sum, Average
if (resolved.IsAggregation && resolved.AggregationType != Min/Max)
    return new PipelineResult.Scalar(value, context);

// Min/Max: Query with OrderBy + Limit(1)
if (resolved.AggregationType == Min || Max)
    return new PipelineResult.Scalar(fieldValue, context);
```

### Conversión Actual (ConvertHandler)

```csharp
if (result is PipelineResult.Scalar scalar)
{
    var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
    return new PipelineResult.Scalar(converted!, context);
}
```

### Propuesta: Mover a SnapshotShapingHandler

```csharp
public async Task<PipelineResult> HandleAsync(...)
{
    var result = await next(context, cancellationToken);

    // Scalar: convert type using ITypeConverter
    if (result is PipelineResult.Scalar scalar)
    {
        var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
        return new PipelineResult.Scalar(converted!, context);
    }

    // Materialized: shape + materialize
    if (result is PipelineResult.Materialized materialized)
    {
        // ... código existente de shaping + materialización
        return new PipelineResult.Materialized(materializedItems, context);
    }

    return result;
}
```

---

## Cambios Detallados

### 1. SnapshotShapingHandler.cs

**Inyección de dependencias:**
```csharp
// Añadir
private readonly ITypeConverter _typeConverter;

public SnapshotShapingHandler(
    ISnapshotShaper snapshotShaper,
    IMaterializer materializer,
    ITypeConverter typeConverter)  // Nuevo
{
    _snapshotShaper = snapshotShaper;
    _materializer = materializer;
    _typeConverter = typeConverter;
}
```

**HandleAsync:**
```csharp
public async Task<PipelineResult> HandleAsync(
    PipelineContext context,
    PipelineDelegate next,
    CancellationToken cancellationToken)
{
    var result = await next(context, cancellationToken);

    // Scalar: convert type
    if (result is PipelineResult.Scalar scalar)
    {
        var converted = _typeConverter.Convert(scalar.Value, context.ResultType);
        return new PipelineResult.Scalar(converted!, context);
    }

    // Materialized: shape + materialize
    if (result is PipelineResult.Materialized materialized)
    {
        var resolved = context.ResolvedQuery;
        if (resolved == null)
            return result;

        var allSnapshots = materialized.Context.GetMetadata<Dictionary<string, DocumentSnapshot>>(
            PipelineMetadataKeys.AllSnapshots);

        if (allSnapshots == null || allSnapshots.Count == 0)
            return new PipelineResult.Materialized(Array.Empty<object>(), context);

        var subcollectionAggregations = materialized.Context.GetMetadata<Dictionary<string, object>>(
            PipelineMetadataKeys.SubcollectionAggregations);

        var debugSnapshots = allSnapshots.Values.OfType<DocumentSnapshot>().ToList();
        var shapedResult = _snapshotShaper.Shape(resolved, debugSnapshots, subcollectionAggregations);

        var projectedFields = resolved.Projection?.Fields;
        var materializedItems = _materializer.Materialize(shapedResult, context.ResultType, projectedFields);

        return new PipelineResult.Materialized(materializedItems, context);
    }

    return result;
}
```

### 2. FirestoreServiceCollectionExtensions.cs

**Eliminar registros:**
```csharp
// ELIMINAR estas líneas:
.TryAddScoped<IFirestoreDocumentDeserializer, FirestoreDocumentDeserializer>()
.TryAddScoped<IProjectionMaterializer, ProjectionMaterializer>()

// ELIMINAR este handler:
serviceCollection.AddScoped<IQueryPipelineHandler, ConvertHandler>();
```

**Nuevo orden de handlers:**
```csharp
// Pipeline Handlers (order matters - middleware pattern)
// Order: ErrorHandling → Resolver → Log → Proxy → Tracking → SnapshotShaping → Execution
serviceCollection.AddScoped<IQueryPipelineHandler, ErrorHandlingHandler>();
serviceCollection.AddScoped<IQueryPipelineHandler, ResolverHandler>();
serviceCollection.AddScoped<IQueryPipelineHandler, LogQueryHandler>();
serviceCollection.AddScoped<IQueryPipelineHandler>(sp =>
    new ProxyHandler(sp.GetService<IProxyFactory>()));
serviceCollection.AddScoped<IQueryPipelineHandler, TrackingHandler>();
serviceCollection.AddScoped<IQueryPipelineHandler, SnapshotShapingHandler>();  // Materializa aquí
serviceCollection.AddScoped<IQueryPipelineHandler, ExecutionHandler>();
```

### 3. Archivos a Eliminar (después de validar tests)

```
src/Fudie.Firestore.EntityFrameworkCore/Query/Pipeline/Handlers/ConvertHandler.cs
src/Fudie.Firestore.EntityFrameworkCore/Storage/IProjectionMaterializer.cs
src/Fudie.Firestore.EntityFrameworkCore/Storage/ProjectionMaterializer.cs
src/Fudie.Firestore.EntityFrameworkCore/Infrastructure/Contracts/IFirestoreDocumentDeserializer.cs
src/Fudie.Firestore.EntityFrameworkCore/Storage/FirestoreDocumentDeserializer.cs
```

---

## Diagrama de Migración

```
ANTES:
┌─────────────┐    ┌──────────────────┐    ┌───────────┐
│ Execution   │───▶│ SnapshotShaping  │───▶│ Convert   │───▶ ...
│ (snapshots) │    │ (debug only)     │    │ (PM + DD) │
└─────────────┘    └──────────────────┘    └───────────┘

DESPUÉS:
┌─────────────┐    ┌──────────────────────────────┐
│ Execution   │───▶│ SnapshotShaping              │───▶ ...
│ (snapshots) │    │ (shape + materialize + type) │
└─────────────┘    └──────────────────────────────┘

PM = ProjectionMaterializer (eliminado)
DD = DocumentDeserializer (eliminado)
```

---

## Riesgos y Mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| Tests fallan | Backup creado en `backup_pipeline_20260120.zip` |
| Casos edge no cubiertos | 466 tests de integración existentes |
| Performance | `Materializer` usa cache de estrategias |

---

## Checklist de Implementación

- [ ] Modificar `SnapshotShapingHandler` para manejar Scalar y Materialized
- [ ] Actualizar `FirestoreServiceCollectionExtensions.cs` (orden + eliminar registros)
- [ ] Ejecutar tests de integración (466 tests)
- [ ] Si pasan, eliminar archivos obsoletos
- [ ] Actualizar comentarios del pipeline en el código

---

## Backup

Ubicación: `backup_pipeline_20260120.zip`

Contenido:
- `FirestoreServiceCollectionExtensions.cs`
- `Handlers/` (todos los handlers)
