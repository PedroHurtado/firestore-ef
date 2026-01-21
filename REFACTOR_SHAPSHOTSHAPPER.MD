# Refactor: SnapshotShaper

## Resumen Ejecutivo

Este documento describe las mejoras propuestas para `SnapshotShaper.cs` en el proyecto Fudie.Firestore.EntityFrameworkCore. El objetivo es mejorar la mantenibilidad, corregir bugs y optimizar el rendimiento sin romper la compatibilidad con el `Materializer` actual.

---

## 1. Introducción de `ShapeOptions`

### Problema

`ShapeNode` tiene 7 parámetros, lo que dificulta la legibilidad y el mantenimiento:

```csharp
private static Dictionary<string, object?> ShapeNode(
    ShapingContext context,
    SnapshotInfo info,
    IReadOnlyList<ResolvedInclude> includes,
    IReadOnlyList<ResolvedSubcollectionProjection>? projectionSubcollections = null,
    bool includePk = true,
    string? pkName = null,
    IReadOnlyList<FirestoreProjectedField>? projectedFields = null)
```

### Solución

Crear un record `ShapeOptions` que agrupe los parámetros de configuración:

```csharp
public record ShapeOptions(
    IReadOnlyList<ResolvedInclude> Includes,
    IReadOnlyList<ResolvedSubcollectionProjection>? Subcollections = null,
    IReadOnlyList<FirestoreProjectedField>? ProjectedFields = null,
    bool IncludePk = true,
    string? PkName = null);
```

### Nueva firma

```csharp
private static Dictionary<string, object?> ShapeNode(
    ShapingContext context,
    SnapshotInfo info,
    ShapeOptions options)
```

---

## 2. Separación de Responsabilidades en `ShapeNode`

### Problema

`ShapeNode` actualmente maneja 5 responsabilidades distintas:

1. Extracción de datos del snapshot
2. Aplicación de proyección de campos
3. Adición de Primary Key
4. Resolución de includes (colecciones y referencias)
5. Resolución de subcollections de proyección

### Solución

Refactorizar a un pipeline de métodos especializados:

```csharp
private static Dictionary<string, object?> ShapeNode(
    ShapingContext context,
    SnapshotInfo info,
    ShapeOptions options)
{
    var nodePath = GetRelativeDocumentPath(info);
    var rawDict = info.Snapshot.ToDictionary();

    var dict = ExtractFields(rawDict, options);
    
    AddPrimaryKey(dict, info.DocumentId, options);
    
    ResolveIncludes(context, dict, nodePath, options.Includes);
    
    ResolveSubcollections(context, dict, nodePath, options.Subcollections);

    return dict;
}
```

### Métodos Extraídos

#### 2.1 ExtractFields

```csharp
private static Dictionary<string, object?> ExtractFields(
    Dictionary<string, object> rawDict,
    ShapeOptions options)
{
    var dict = new Dictionary<string, object?>();

    if (options.ProjectedFields is { Count: > 0 })
    {
        foreach (var field in options.ProjectedFields)
        {
            if (rawDict.TryGetValue(field.FieldPath, out var value))
            {
                dict[field.ResultName] = value;
            }
        }
    }
    else
    {
        foreach (var kvp in rawDict)
        {
            dict[kvp.Key] = kvp.Value;
        }
    }

    return dict;
}
```

#### 2.2 AddPrimaryKey

```csharp
private static void AddPrimaryKey(
    Dictionary<string, object?> dict,
    string documentId,
    ShapeOptions options)
{
    if (options.IncludePk && options.PkName != null)
    {
        dict[options.PkName] = documentId;
    }
}
```

#### 2.3 ResolveIncludes

```csharp
private static void ResolveIncludes(
    ShapingContext context,
    Dictionary<string, object?> dict,
    string nodePath,
    IReadOnlyList<ResolvedInclude> includes)
{
    foreach (var include in includes)
    {
        if (include.IsCollection)
        {
            dict[include.NavigationName] = ResolveCollectionInclude(context, nodePath, include);
        }
        else
        {
            ResolveReferenceInPlace(context, dict, include);
        }
    }
}

private static List<Dictionary<string, object?>> ResolveCollectionInclude(
    ShapingContext context,
    string parentPath,
    ResolvedInclude include)
{
    var childOptions = new ShapeOptions(
        Includes: include.NestedIncludes,
        PkName: include.PrimaryKeyPropertyName,
        IncludePk: true);

    return ResolveChildren(context, parentPath, include.CollectionPath, childOptions);
}
```

#### 2.4 ResolveSubcollections

```csharp
private static void ResolveSubcollections(
    ShapingContext context,
    Dictionary<string, object?> dict,
    string nodePath,
    IReadOnlyList<ResolvedSubcollectionProjection>? subcollections)
{
    if (subcollections == null) return;

    foreach (var subcol in subcollections)
    {
        dict[subcol.ResultName] = ResolveProjectionSubcollection(context, nodePath, subcol);
    }
}
```

---

## 3. Tercer Índice para Lookup O(1)

### Problema

En `FindFullParentPath` y `FindAndShapeReference` se busca linealmente por `DocumentId` dentro de una colección:

```csharp
var found = candidates.FirstOrDefault(c => c.DocumentId == documentId);
```

Si hay 1000 documentos en "Pedidos", cada lookup es O(1000).

### Ubicaciones del problema

1. `FindFullParentPath`:
```csharp
if (context.ByCollection.TryGetValue(collectionPath, out var candidates))
{
    var found = candidates.FirstOrDefault(c => c.DocumentId == documentId);  // O(n)
    // ...
}
```

2. `FindAndShapeReference`:
```csharp
if (!context.ByCollection.TryGetValue(include.CollectionPath, out var candidates))
    return null;

var found = candidates.FirstOrDefault(c => c.DocumentId == refDocId);  // O(n)
```

### Solución

Añadir un tercer índice `ByFullPath` a `ShapingContext`:

```csharp
internal record ShapingContext(
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByCollection,
    IReadOnlyDictionary<string, List<SnapshotInfo>> ByParentPath,
    IReadOnlyDictionary<string, SnapshotInfo> ByFullPath,  // NUEVO
    IReadOnlyDictionary<string, object> Aggregations)
```

### Modificar `ShapingContext.Create`

```csharp
public static ShapingContext Create(
    IReadOnlyList<DocumentSnapshot> snapshots,
    Dictionary<string, object>? aggregations)
{
    var byCollection = new Dictionary<string, List<SnapshotInfo>>();
    var byParentPath = new Dictionary<string, List<SnapshotInfo>>();
    var byFullPath = new Dictionary<string, SnapshotInfo>();  // NUEVO

    foreach (var snapshot in snapshots)
    {
        var info = ParseSnapshotPath(snapshot);

        // Index by collection
        if (!byCollection.TryGetValue(info.CollectionPath, out var collectionList))
        {
            collectionList = new List<SnapshotInfo>();
            byCollection[info.CollectionPath] = collectionList;
        }
        collectionList.Add(info);

        // Index by parent path
        if (info.ParentDocumentPath != null)
        {
            if (!byParentPath.TryGetValue(info.ParentDocumentPath, out var parentList))
            {
                parentList = new List<SnapshotInfo>();
                byParentPath[info.ParentDocumentPath] = parentList;
            }
            parentList.Add(info);
        }

        // NUEVO: Index by full path (collection/documentId)
        var fullPath = $"{info.CollectionPath}/{info.DocumentId}";
        byFullPath[fullPath] = info;
    }

    return new ShapingContext(byCollection, byParentPath, byFullPath, aggregations ?? new Dictionary<string, object>());
}
```

### Actualizar lookups

#### FindFullParentPath

```csharp
private static string? FindFullParentPath(ShapingContext context, string relativePath)
{
    var parts = relativePath.Split('/');
    if (parts.Length < 2)
        return null;

    var collectionPath = parts[^2];
    var documentId = parts[^1];
    var key = $"{collectionPath}/{documentId}";

    if (context.ByFullPath.TryGetValue(key, out var found))
        return found.Snapshot.Reference.Path;

    return null;
}
```

#### FindAndShapeReference

```csharp
private static Dictionary<string, object?>? FindAndShapeReference(
    ShapingContext context,
    DocumentReference docRef,
    ResolvedInclude include)
{
    var key = $"{include.CollectionPath}/{docRef.Id}";

    if (!context.ByFullPath.TryGetValue(key, out var found))
        return null;

    var childOptions = new ShapeOptions(
        Includes: include.NestedIncludes,
        PkName: include.PrimaryKeyPropertyName,
        IncludePk: true);

    return ShapeNode(context, found, childOptions);
}
```

---

## 4. Eliminar Doble Pasada: Unificar Shaping + Metadatos

### Problema

Actualmente hay dos pasadas sobre los datos:

```csharp
// Pasada 1: ShapeNode construye Dictionary<string, object?>
var shapedNodes = roots
    .Select(root => ShapeNode(...))
    .ToList();

// Pasada 2: BuildTypedItems añade metadatos
var typedItems = BuildTypedItems(shapedNodes, query);

return new ShapedResult(shapedNodes, typedItems, query.HasProjection);
```

Esto:
- Recorre los datos dos veces
- Mantiene dos estructuras paralelas
- Tiene un **bug**: las subcollections se procesan dos veces cuando no hay proyección

### El Bug en Detalle

En `BuildTypedItem`:

```csharp
if (projectedFields.Count > 0)
{
    // ... usa projectedFields
}
else
{
    // No projection - copia todo de rawItem (incluidas subcollections)
    foreach (var kvp in rawItem)
    {
        // ...
    }
}

// BUG: Esto se ejecuta SIEMPRE, incluso cuando ya procesamos subcollections arriba
foreach (var subcol in subcollections)
{
    if (rawItem.TryGetValue(subcol.ResultName, out var value))
    {
        // Sobrescribe lo que ya se procesó
    }
}
```

### Solución

`ShapeNode` debe devolver `ShapedItem` directamente, construyendo los metadatos en la misma pasada.

#### Nueva firma de ShapeNode

```csharp
private static ShapedItem ShapeNode(
    ShapingContext context,
    SnapshotInfo info,
    ShapeOptions options)
{
    var nodePath = GetRelativeDocumentPath(info);
    var rawDict = info.Snapshot.ToDictionary();
    var shapedItem = new ShapedItem();

    ExtractFields(rawDict, options, shapedItem);
    
    AddPrimaryKey(shapedItem, info.DocumentId, options);
    
    ResolveIncludes(context, shapedItem, nodePath, options);
    
    ResolveSubcollections(context, shapedItem, nodePath, options);

    return shapedItem;
}
```

#### ExtractFields con metadatos inline

```csharp
private static void ExtractFields(
    Dictionary<string, object> rawDict,
    ShapeOptions options,
    ShapedItem shapedItem)
{
    if (options.ProjectedFields is { Count: > 0 })
    {
        foreach (var field in options.ProjectedFields)
        {
            if (rawDict.TryGetValue(field.FieldPath, out var value))
            {
                var kind = DetermineValueKind(field.FieldType, value);
                shapedItem.Values[field.ResultName] = new ShapedValue(value, field.FieldType, kind);
            }
        }
    }
    else
    {
        foreach (var kvp in rawDict)
        {
            var inferredType = InferType(kvp.Value);
            var kind = DetermineValueKindFromValue(kvp.Value);
            shapedItem.Values[kvp.Key] = new ShapedValue(kvp.Value, inferredType, kind);
        }
    }
}
```

#### AddPrimaryKey actualizado

```csharp
private static void AddPrimaryKey(
    ShapedItem shapedItem,
    string documentId,
    ShapeOptions options)
{
    if (options.IncludePk && options.PkName != null)
    {
        shapedItem.Values[options.PkName] = new ShapedValue(
            documentId, 
            typeof(string), 
            ValueKind.Scalar);
    }
}
```

#### ResolveIncludes actualizado

```csharp
private static void ResolveIncludes(
    ShapingContext context,
    ShapedItem shapedItem,
    string nodePath,
    IReadOnlyList<ResolvedInclude> includes)
{
    foreach (var include in includes)
    {
        if (include.IsCollection)
        {
            var children = ResolveCollectionInclude(context, nodePath, include);
            var targetType = typeof(IEnumerable<>).MakeGenericType(typeof(object));
            shapedItem.Values[include.NavigationName] = new ShapedValue(
                children, 
                targetType, 
                ValueKind.ObjectList);
        }
        else
        {
            ResolveReferenceInPlace(context, shapedItem, include);
        }
    }
}
```

---

## 5. Compatibilidad con Materializer Actual

### Estrategia de Migración

Para no romper el `Materializer` actual, `ShapedItem` proporcionará un método de conversión:

```csharp
public class ShapedItem
{
    public Dictionary<string, ShapedValue> Values { get; init; } = new();

    /// <summary>
    /// Conversión temporal para compatibilidad con el Materializer actual.
    /// ELIMINAR cuando el Materializer use ShapedItem directamente.
    /// </summary>
    [Obsolete("Usar Values directamente. Eliminar cuando Materializer esté migrado.")]
    public Dictionary<string, object?> ToLegacyDict() 
        => Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
}
```

### Shape() con compatibilidad

```csharp
public ShapedResult Shape(
    ResolvedFirestoreQuery query,
    IReadOnlyList<DocumentSnapshot> snapshots,
    Dictionary<string, object>? aggregations = null)
{
    if (snapshots.Count == 0)
        return new ShapedResult([], [], false);

    var context = ShapingContext.Create(snapshots, aggregations);
    var roots = FindRoots(context, query);

    if (roots.Count == 0)
        return new ShapedResult([], [], false);

    var options = BuildRootOptions(query);

    // UNA SOLA PASADA: ShapedItem es la fuente de verdad
    var typedItems = roots
        .Select(root => ShapeNode(context, root, options))
        .ToList();

    // Flatten para proyecciones
    if (query.HasProjection)
    {
        typedItems = typedItems.Select(FlattenShapedItem).ToList();
    }

    // LEGACY: Derivar Dictionary para el Materializer actual
    // TODO: Eliminar cuando Materializer use TypedItems
    #pragma warning disable CS0618
    var legacyItems = typedItems.Select(t => t.ToLegacyDict()).ToList();
    #pragma warning restore CS0618

    return new ShapedResult(legacyItems, typedItems, query.HasProjection);
}

private static ShapeOptions BuildRootOptions(ResolvedFirestoreQuery query)
{
    var pkName = query.PrimaryKeyPropertyName;
    var includePk = ShouldIncludePk(query.Projection?.Fields, pkName);

    return new ShapeOptions(
        Includes: query.Includes,
        Subcollections: query.Projection?.Subcollections,
        ProjectedFields: query.Projection?.Fields,
        IncludePk: includePk,
        PkName: pkName);
}
```

### Plan de Eliminación de Legacy

1. **Fase 1 (Este refactor)**: 
   - `ShapedItem` es la fuente de verdad
   - `ToLegacyDict()` genera el formato antiguo
   - `ShapedResult` mantiene ambos campos

2. **Fase 2 (Migrar Materializer)**:
   - Actualizar `Materializer` para usar `TypedItems` y `ShapedValue`
   - El `Materializer` ya no necesita inferir tipos, los recibe en `ValueKind`

3. **Fase 3 (Limpieza)**:
   - Eliminar `ToLegacyDict()`
   - Eliminar campo `Items` de `ShapedResult`
   - Eliminar el atributo `[Obsolete]`

---

## 6. Eliminar `FlattenForProjection` — Mantener Jerarquía

### Problema

Actualmente hay una tercera pasada para aplanar diccionarios:

```csharp
// Pasada 1: ShapeNode
var shapedNodes = roots.Select(root => ShapeNode(...)).ToList();

// Pasada 2: FlattenForProjection
if (query.HasProjection)
{
    shapedNodes = shapedNodes.Select(FlattenForProjection).ToList();
}

// Pasada 3: BuildTypedItems
var typedItems = BuildTypedItems(shapedNodes, query);
```

El flatten transforma:

```csharp
// Input:
{ 
  "Direccion": { 
    "Ciudad": "Bilbao", 
    "Coordenadas": { "Altitud": 19 } 
  } 
}

// Output:
{ 
  "Direccion.Ciudad": "Bilbao", 
  "Direccion.Coordenadas.Altitud": 19 
}
```

### Problemas del Flatten

1. **Es otra pasada más** — 3 pasadas cuando debería ser 1
2. **Pierde información estructural** — No distingue entre un campo nested y uno que literalmente se llama `"Direccion.Ciudad"`
3. **El Materializer necesita información extra** — Para `"Direccion.Ciudad": "Bilbao"`, necesita saber el tipo de `Direccion` para instanciarlo

### Solución: Estructura Jerárquica de ShapedItem

No aplanar nunca. La jerarquía de `ShapedItem` refleja la jerarquía del objeto destino:

```csharp
// En vez de:
Values["Direccion.Ciudad"] = new ShapedValue("Bilbao", typeof(string), ValueKind.Scalar)

// Mantener:
Values["Direccion"] = new ShapedValue(
    Value: new ShapedItem {
        Values = {
            ["Ciudad"] = new ShapedValue("Bilbao", typeof(string), ValueKind.Scalar),
            ["Coordenadas"] = new ShapedValue(
                Value: new ShapedItem {
                    Values = {
                        ["Altitud"] = new ShapedValue(19, typeof(int), ValueKind.Scalar)
                    }
                },
                TargetType: typeof(CoordenadasVO),
                Kind: ValueKind.ComplexType
            )
        }
    },
    TargetType: typeof(DireccionVO),
    Kind: ValueKind.ComplexType
);
```

### Ventajas

| Aspecto | Con Flatten | Sin Flatten (Jerárquico) |
|---------|-------------|--------------------------|
| Pasadas sobre datos | 3 | 1 |
| Información de tipos intermedios | Perdida | Preservada en `TargetType` |
| Materializer necesita | Parsear paths, inferir tipos | Nada extra |
| Complejidad | Alta | Baja |

### Materializer Simplificado

Con la estructura jerárquica, el Materializer se vuelve recursivo y simple:

```csharp
private object? Materialize(ShapedItem item, Type targetType)
{
    var instance = CreateInstance(targetType);

    foreach (var (name, shaped) in item.Values)
    {
        var value = shaped.Kind switch
        {
            ValueKind.Scalar => ConvertScalar(shaped.Value, shaped.TargetType),
            ValueKind.ComplexType => Materialize((ShapedItem)shaped.Value!, shaped.TargetType),
            ValueKind.ObjectList => MaterializeList((IList<ShapedItem>)shaped.Value!, shaped.TargetType),
            ValueKind.ScalarList => ConvertScalarList(shaped.Value, shaped.TargetType),
            ValueKind.Entity => MaterializeEntity(shaped.Value, shaped.TargetType),
            _ => shaped.Value
        };

        SetProperty(instance, name, value);
    }

    return instance;
}
```

### Modificar ExtractFields para Construir Jerarquía

En lugar de extraer valores planos, construir `ShapedItem` anidados:

```csharp
private static void ExtractFields(
    Dictionary<string, object> rawDict,
    ShapeOptions options,
    ShapedItem shapedItem)
{
    if (options.ProjectedFields is { Count: > 0 })
    {
        // Agrupar campos por primer segmento del path
        var grouped = options.ProjectedFields
            .GroupBy(f => f.FieldPath.Split('.')[0])
            .ToList();

        foreach (var group in grouped)
        {
            var rootKey = group.Key;
            var fields = group.ToList();

            if (fields.Count == 1 && !fields[0].FieldPath.Contains('.'))
            {
                // Campo simple: "Nombre"
                var field = fields[0];
                if (rawDict.TryGetValue(field.FieldPath, out var value))
                {
                    var kind = DetermineValueKind(field.FieldType, value);
                    shapedItem.Values[field.ResultName] = new ShapedValue(value, field.FieldType, kind);
                }
            }
            else
            {
                // Campos anidados: "Direccion.Ciudad", "Direccion.Coordenadas.Altitud"
                if (rawDict.TryGetValue(rootKey, out var nestedRaw) && nestedRaw is Dictionary<string, object> nestedDict)
                {
                    var nestedItem = ExtractNestedFields(nestedDict, fields, rootKey);
                    var containerType = DetermineContainerType(fields);
                    shapedItem.Values[rootKey] = new ShapedValue(nestedItem, containerType, ValueKind.ComplexType);
                }
            }
        }
    }
    else
    {
        // Sin proyección - extraer todo preservando jerarquía
        foreach (var kvp in rawDict)
        {
            shapedItem.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value);
        }
    }
}

private static ShapedValue ExtractValuePreservingHierarchy(object? value)
{
    return value switch
    {
        Dictionary<string, object?> dict => new ShapedValue(
            Value: ExtractDictAsShapedItem(dict),
            TargetType: typeof(object), // Se inferirá del AST o modelo
            Kind: ValueKind.ComplexType),
        
        IList<Dictionary<string, object?>> list => new ShapedValue(
            Value: list.Select(ExtractDictAsShapedItem).ToList(),
            TargetType: typeof(IEnumerable<object>),
            Kind: ValueKind.ObjectList),
        
        _ => new ShapedValue(
            Value: value,
            TargetType: InferType(value),
            Kind: DetermineValueKindFromValue(value))
    };
}

private static ShapedItem ExtractDictAsShapedItem(Dictionary<string, object?> dict)
{
    var item = new ShapedItem();
    foreach (var kvp in dict)
    {
        item.Values[kvp.Key] = ExtractValuePreservingHierarchy(kvp.Value);
    }
    return item;
}
```

### Eliminar Código

Tras este refactor, eliminar:

- `FlattenForProjection`
- `FlattenRecursive`
- El bloque `if (query.HasProjection) { shapedNodes = shapedNodes.Select(FlattenForProjection)... }`
- `FlattenShapedItem` (si existe una versión para `ShapedItem`)

---

## 7. Resumen de Cambios

| Archivo | Cambio |
|---------|--------|
| `ShapedResult.cs` | Añadir `ToLegacyDict()` a `ShapedItem` |
| `ShapingContext` | Añadir índice `ByFullPath` |
| `SnapshotShaper.cs` | Introducir `ShapeOptions` |
| `SnapshotShaper.cs` | Extraer métodos: `ExtractFields`, `AddPrimaryKey`, `ResolveIncludes`, `ResolveSubcollections` |
| `SnapshotShaper.cs` | `ShapeNode` devuelve `ShapedItem` directamente |
| `SnapshotShaper.cs` | Eliminar `BuildTypedItems` (integrado en `ShapeNode`) |
| `SnapshotShaper.cs` | Eliminar `FlattenForProjection` y `FlattenRecursive` |
| `SnapshotShaper.cs` | `ExtractFields` construye jerarquía de `ShapedItem` anidados |
| `SnapshotShaper.cs` | Actualizar `FindFullParentPath` y `FindAndShapeReference` para usar `ByFullPath` |

---

## 8. Orden de Implementación Recomendado

1. **Paso 1**: Crear `ShapeOptions` y refactorizar firma de `ShapeNode` (sin cambiar lógica interna)
2. **Paso 2**: Extraer métodos (`ExtractFields`, `AddPrimaryKey`, etc.)
3. **Paso 3**: Añadir índice `ByFullPath` a `ShapingContext`
4. **Paso 4**: Actualizar lookups para usar `ByFullPath`
5. **Paso 5**: Modificar `ExtractFields` para construir jerarquía de `ShapedItem` anidados
6. **Paso 6**: Eliminar `FlattenForProjection`, `FlattenRecursive` y llamadas relacionadas
7. **Paso 7**: Modificar `ShapeNode` para devolver `ShapedItem`
8. **Paso 8**: Añadir `ToLegacyDict()` recursivo y actualizar `Shape()`
9. **Paso 9**: Eliminar `BuildTypedItems`
10. **Paso 10**: Ejecutar tests y verificar que todo funciona

---

## 9. Tests a Verificar

Tras el refactor, asegurar que pasan:

- [ ] Queries sin proyección
- [ ] Queries con proyección de campos
- [ ] Queries con proyección de campos anidados (ej: `Direccion.Ciudad`)
- [ ] Queries con includes de colección (1:N)
- [ ] Queries con includes de referencia (N:1)
- [ ] Queries con subcollections anidadas (ThenInclude)
- [ ] Queries con agregaciones
- [ ] Queries con proyección sobre subcollections

---

## 10. Métricas de Éxito

| Métrica | Antes | Después |
|---------|-------|---------|
| Parámetros en `ShapeNode` | 7 | 3 |
| Pasadas sobre datos | 3 | 1 |
| Lookup por DocumentId | O(n) | O(1) |
| Bug de subcollections duplicadas | Sí | No |
| Líneas en `ShapeNode` | ~60 | ~15 |
| Flatten necesario | Sí | No |
| Información de tipos intermedios | Perdida | Preservada |