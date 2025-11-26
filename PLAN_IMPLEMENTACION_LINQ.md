# Plan de Implementación: LINQ Query Pipeline para Firestore EF Core Provider

**Fecha:** 26 de noviembre de 2025
**Objetivo:** Implementar el pipeline completo de traducción de queries LINQ a Firestore queries
**Estado del Proyecto:** Escritura completa ✅ | Lectura - Fase 1 (Fundamentos) ✅

---

## 📊 Resumen Ejecutivo

Este plan detalla la implementación del Query Pipeline de EF Core para el proveedor de Firestore, permitiendo traducir expresiones LINQ (Where, OrderBy, Take, etc.) a queries nativas de Firestore y materializar los resultados en entidades C#.

### Arquitectura del Pipeline

```
LINQ Expression (C#)
    ↓
QueryableMethodTranslatingExpressionVisitor
    ↓ Traduce métodos LINQ a expresiones del provider
FirestoreQueryExpression (representación interna)
    ↓
ShapedQueryCompilingExpressionVisitor
    ↓ Compila la query en código ejecutable
FirestoreQueryExecutor
    ↓ Construye Google.Cloud.Firestore.Query
Firestore SDK
    ↓ Ejecuta la query
QuerySnapshot (DocumentSnapshot[])
    ↓
FirestoreDocumentDeserializer
    ↓ Deserializa y convierte tipos
Entidades C# (List<T>)
```

---

## 🎯 Objetivos del Plan

### Funcionalidades a Implementar

#### ✅ Operaciones Básicas de Lectura
- `context.Productos.ToList()` - Obtener todos los documentos
- `context.Productos.Find(id)` - Buscar por ID
- `context.Productos.FirstOrDefault()` - Primer elemento
- `context.Productos.Count()` - Contar elementos

#### ✅ Filtrado con Where
- `Where(p => p.Precio > 100)` - Operadores de comparación (==, !=, <, >, <=, >=)
- `Where(p => ids.Contains(p.Id))` - Operador IN (WhereIn)
- `Where(p => p.Tags.Contains("new"))` - Array contains
- Múltiples Where encadenados

#### ✅ Ordenamiento
- `OrderBy(p => p.Nombre)` - Orden ascendente
- `OrderByDescending(p => p.Precio)` - Orden descendente
- `ThenBy` - Ordenamiento secundario

#### ✅ Paginación
- `Take(n)` - Limitar resultados (Limit)
- `Skip(n)` - Saltar elementos (usando cursores)

#### ⚠️ Operaciones con Evaluación Parcial
- `Count(predicate)` - Contar en memoria después de filtrar
- `Any(predicate)` - Verificar existencia con Limit(1)
- `Max/Min/Average` - Agregaciones en memoria

#### ❌ Operaciones NO Soportadas (limitaciones de Firestore)
- `OR` compuesto: `Where(p => p.A == 1 || p.B == 2)` - Requiere múltiples queries
- Múltiples rangos: `Where(p => p.Precio > 10 && p.Stock < 100)` - Solo un rango por query
- `Join` / `GroupJoin` - Firestore no soporta JOINs
- `GroupBy` en servidor - Se hace en memoria

---

## 🏗️ Componentes a Implementar

### 1. FirestoreQueryExpression (Query/FirestoreQueryExpression.cs)
**Propósito:** Representación interna de una query de Firestore

```csharp
public class FirestoreQueryExpression : Expression
{
    public IEntityType EntityType { get; set; }
    public string CollectionName { get; set; }
    public List<FirestoreWhereClause> Filters { get; set; }
    public List<FirestoreOrderByClause> OrderByClauses { get; set; }
    public int? Limit { get; set; }
    public DocumentSnapshot? StartAfterDocument { get; set; }

    public override Type Type => typeof(IEnumerable<>).MakeGenericType(EntityType.ClrType);
    public override ExpressionType NodeType => ExpressionType.Extension;
}

public class FirestoreWhereClause
{
    public string PropertyName { get; set; }
    public FirestoreOperator Operator { get; set; }
    public object Value { get; set; }
}

public enum FirestoreOperator
{
    EqualTo,              // ==
    NotEqualTo,           // !=
    LessThan,             // <
    LessThanOrEqualTo,    // <=
    GreaterThan,          // >
    GreaterThanOrEqualTo, // >=
    ArrayContains,        // array-contains
    In,                   // in
    ArrayContainsAny      // array-contains-any
}

public class FirestoreOrderByClause
{
    public string PropertyName { get; set; }
    public bool Descending { get; set; }
}
```

**Archivos:** `Query/FirestoreQueryExpression.cs`

---

### 2. FirestoreExpressionTranslatingExpressionVisitor
**Propósito:** Traducir expresiones C# (BinaryExpression, MemberAccess, etc.) a operaciones de Firestore

```csharp
public class FirestoreExpressionTranslatingExpressionVisitor : ExpressionVisitor
{
    private readonly IModel _model;
    private readonly ITypeMappingSource _typeMappingSource;

    // Traduce: p => p.Precio > 100
    // En: FirestoreWhereClause { PropertyName = "Precio", Operator = GreaterThan, Value = 100 }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Mapear ExpressionType a FirestoreOperator
        // ExpressionType.GreaterThan → FirestoreOperator.GreaterThan
        // Aplicar conversiones: decimal → double, enum → string
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Extraer nombre de propiedad
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // Aplicar conversiones de tipos
    }
}
```

**Archivos:** `Query/FirestoreExpressionTranslatingExpressionVisitor.cs`

---

### 3. FirestoreQueryableMethodTranslatingExpressionVisitor (Completar implementación)
**Propósito:** Traducir métodos LINQ (Where, OrderBy, Take) a FirestoreQueryExpression

**Estado Actual:** Clase existe pero todos los métodos lanzan `NotImplementedException`
**Ubicación:** `Infrastructure/FirestoreServiceCollectionExtensions.cs` líneas 83-293

**Implementación:**

```csharp
protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
{
    // Crear FirestoreQueryExpression inicial para la colección
    var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);
    var queryExpression = new FirestoreQueryExpression
    {
        EntityType = entityType,
        CollectionName = collectionName,
        Filters = new List<FirestoreWhereClause>(),
        OrderByClauses = new List<FirestoreOrderByClause>()
    };

    return new ShapedQueryExpression(
        queryExpression,
        new EntityShaperExpression(entityType, ...));
}

protected override ShapedQueryExpression? TranslateWhere(
    ShapedQueryExpression source, LambdaExpression predicate)
{
    var queryExpression = (FirestoreQueryExpression)source.QueryExpression;

    // Usar FirestoreExpressionTranslatingExpressionVisitor para traducir predicate
    var translatedPredicate = _expressionVisitor.Translate(predicate.Body);

    // Agregar WhereClause a queryExpression.Filters
    queryExpression.Filters.Add(whereClause);

    return source.UpdateQueryExpression(queryExpression);
}

protected override ShapedQueryExpression? TranslateOrderBy(
    ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
{
    var queryExpression = (FirestoreQueryExpression)source.QueryExpression;

    // Extraer nombre de propiedad
    var propertyName = GetPropertyName(keySelector);

    queryExpression.OrderByClauses.Add(new FirestoreOrderByClause
    {
        PropertyName = propertyName,
        Descending = !ascending
    });

    return source.UpdateQueryExpression(queryExpression);
}

protected override ShapedQueryExpression? TranslateTake(
    ShapedQueryExpression source, Expression count)
{
    var queryExpression = (FirestoreQueryExpression)source.QueryExpression;
    var limitValue = (int)((ConstantExpression)count).Value;
    queryExpression.Limit = limitValue;

    return source.UpdateQueryExpression(queryExpression);
}

protected override ShapedQueryExpression? TranslateCount(
    ShapedQueryExpression source, LambdaExpression? predicate)
{
    // Firestore no tiene COUNT nativo - obtener documentos y contar en memoria
    // O marcar como operación de agregación para evaluación diferida
}
```

**Métodos a Implementar (Prioridad):**
1. ✅ **Alta**: CreateShapedQueryExpression, TranslateWhere, TranslateOrderBy, TranslateTake, TranslateFirstOrDefault, TranslateSelect
2. ⚠️ **Media**: TranslateCount, TranslateAny, TranslateThenBy, TranslateSkip
3. ❌ **Baja/No soportado**: TranslateJoin, TranslateGroupBy, TranslateGroupJoin

---

### 4. FirestoreShapedQueryCompilingExpressionVisitor (Completar implementación)
**Propósito:** Compilar FirestoreQueryExpression en código ejecutable que materializa entidades

**Estado Actual:** Clase existe pero VisitShapedQuery lanza `NotImplementedException`
**Ubicación:** `Infrastructure/FirestoreServiceCollectionExtensions.cs` líneas 312-326

**Implementación:**

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    var firestoreQuery = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

    // 1. Generar código para construir Google.Cloud.Firestore.Query
    var queryBuilderExpression = BuildFirestoreQueryExpression(firestoreQuery);

    // 2. Generar código para ejecutar la query
    var executeExpression = Expression.Call(
        _executeMethod,
        queryBuilderExpression,
        Expression.Constant(firestoreQuery.EntityType));

    // 3. Generar código para materializar entidades (shaper)
    var shaperExpression = shapedQueryExpression.ShaperExpression;
    var materializeExpression = Expression.Call(
        _materializeMethod,
        executeExpression,
        shaperExpression);

    return materializeExpression;
}

private Expression BuildFirestoreQueryExpression(FirestoreQueryExpression queryExpression)
{
    // Generar código que construye:
    // var query = db.Collection("productos");
    // query = query.WhereGreaterThan("Precio", 100);
    // query = query.OrderBy("Nombre");
    // query = query.Limit(10);
}
```

---

### 5. FirestoreQueryExecutor (Query/FirestoreQueryExecutor.cs)
**Propósito:** Ejecutar queries de Firestore y obtener DocumentSnapshots

```csharp
public class FirestoreQueryExecutor
{
    private readonly IFirestoreClientWrapper _client;
    private readonly IFirestoreCollectionManager _collectionManager;

    public async Task<QuerySnapshot> ExecuteQueryAsync(
        FirestoreQueryExpression queryExpression,
        CancellationToken cancellationToken = default)
    {
        // Construir Google.Cloud.Firestore.Query
        var query = BuildFirestoreQuery(queryExpression);

        // Ejecutar
        return await _client.ExecuteQueryAsync(query, cancellationToken);
    }

    private Google.Cloud.Firestore.Query BuildFirestoreQuery(
        FirestoreQueryExpression queryExpression)
    {
        Google.Cloud.Firestore.Query query =
            _client.GetCollection(queryExpression.CollectionName);

        // Aplicar filtros
        foreach (var filter in queryExpression.Filters)
        {
            query = ApplyWhereClause(query, filter);
        }

        // Aplicar ordenamiento
        foreach (var orderBy in queryExpression.OrderByClauses)
        {
            query = orderBy.Descending
                ? query.OrderByDescending(orderBy.PropertyName)
                : query.OrderBy(orderBy.PropertyName);
        }

        // Aplicar límite
        if (queryExpression.Limit.HasValue)
        {
            query = query.Limit(queryExpression.Limit.Value);
        }

        // Aplicar cursor (para Skip)
        if (queryExpression.StartAfterDocument != null)
        {
            query = query.StartAfter(queryExpression.StartAfterDocument);
        }

        return query;
    }

    private Google.Cloud.Firestore.Query ApplyWhereClause(
        Google.Cloud.Firestore.Query query,
        FirestoreWhereClause clause)
    {
        // Aplicar conversiones de tipos antes de pasar a Firestore
        var convertedValue = ConvertValue(clause.Value);

        return clause.Operator switch
        {
            FirestoreOperator.EqualTo => query.WhereEqualTo(clause.PropertyName, convertedValue),
            FirestoreOperator.NotEqualTo => query.WhereNotEqualTo(clause.PropertyName, convertedValue),
            FirestoreOperator.LessThan => query.WhereLessThan(clause.PropertyName, convertedValue),
            FirestoreOperator.LessThanOrEqualTo => query.WhereLessThanOrEqualTo(clause.PropertyName, convertedValue),
            FirestoreOperator.GreaterThan => query.WhereGreaterThan(clause.PropertyName, convertedValue),
            FirestoreOperator.GreaterThanOrEqualTo => query.WhereGreaterThanOrEqualTo(clause.PropertyName, convertedValue),
            FirestoreOperator.ArrayContains => query.WhereArrayContains(clause.PropertyName, convertedValue),
            FirestoreOperator.In => query.WhereIn(clause.PropertyName, (IEnumerable)convertedValue),
            FirestoreOperator.ArrayContainsAny => query.WhereArrayContainsAny(clause.PropertyName, (IEnumerable)convertedValue),
            _ => throw new NotSupportedException($"Operator {clause.Operator} not supported")
        };
    }

    private object ConvertValue(object value)
    {
        // Aplicar conversiones: decimal → double, enum → string
        return value switch
        {
            decimal d => (double)d,
            Enum e => e.ToString(),
            _ => value
        };
    }
}
```

**Archivos:** `Query/FirestoreQueryExecutor.cs`

---

### 6. FirestoreDocumentDeserializer (Storage/FirestoreDocumentDeserializer.cs)
**Propósito:** Convertir DocumentSnapshot a entidades C# (inverso de FirestoreDocumentSerializer)

```csharp
public class FirestoreDocumentDeserializer
{
    private readonly IModel _model;
    private readonly ITypeMappingSource _typeMappingSource;
    private readonly IFirestoreCollectionManager _collectionManager;

    public T DeserializeEntity<T>(DocumentSnapshot document) where T : class, new()
    {
        var entityType = _model.FindEntityType(typeof(T));
        if (entityType == null)
            throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in model");

        var entity = new T();
        var data = document.ToDictionary();

        // 1. Deserializar ID
        DeserializeKey(entity, document.Id, entityType);

        // 2. Deserializar propiedades simples
        DeserializeProperties(entity, data, entityType);

        // 3. Deserializar Complex Properties (Value Objects)
        DeserializeComplexProperties(entity, data, entityType);

        // 4. Deserializar referencias (DocumentReference → entidades)
        // Por ahora solo guardar IDs, carga lazy o eager requiere queries adicionales
        DeserializeReferences(entity, data, entityType);

        return entity;
    }

    private void DeserializeProperties(
        object entity,
        IDictionary<string, object> data,
        ITypeBase typeBase)
    {
        foreach (var property in typeBase.GetProperties())
        {
            if (property.IsPrimaryKey() || property.IsForeignKey())
                continue;

            if (!data.TryGetValue(property.Name, out var value))
                continue;

            // Aplicar conversiones inversas
            var convertedValue = ApplyReverseConverter(property, value);
            property.PropertyInfo?.SetValue(entity, convertedValue);
        }
    }

    private object? ApplyReverseConverter(IProperty property, object value)
    {
        // Conversiones inversas:
        // double → decimal
        // string → enum
        // Timestamp → DateTime

        if (value is double d && property.ClrType == typeof(decimal))
            return (decimal)d;

        if (value is string s && property.ClrType.IsEnum)
            return Enum.Parse(property.ClrType, s);

        var converter = property.GetValueConverter() ?? property.GetTypeMapping()?.Converter;
        return converter != null
            ? converter.ConvertFromProvider(value)
            : value;
    }

    private void DeserializeComplexProperties(
        object entity,
        IDictionary<string, object> data,
        ITypeBase typeBase)
    {
        foreach (var complexProperty in typeBase.GetComplexProperties())
        {
            if (!data.TryGetValue(complexProperty.Name, out var value))
                continue;

            // Verificar si es GeoPoint
            if (complexProperty.FindAnnotation("Firestore:IsGeoPoint")?.Value is true)
            {
                var geoPoint = (Google.Cloud.Firestore.GeoPoint)value;
                var complexObject = CreateGeoPointObject(geoPoint, complexProperty.ComplexType);
                complexProperty.PropertyInfo?.SetValue(entity, complexObject);
                continue;
            }

            // Verificar si es Reference (marcar para carga lazy)
            if (complexProperty.FindAnnotation("Firestore:IsReference")?.Value is true)
            {
                // Por ahora no cargar, requiere query adicional
                continue;
            }

            // Complex Type simple o colección
            if (value is IDictionary<string, object> map)
            {
                var complexObject = DeserializeComplexType(map, complexProperty.ComplexType);
                complexProperty.PropertyInfo?.SetValue(entity, complexObject);
            }
            else if (value is IEnumerable enumerable)
            {
                var list = DeserializeComplexTypeCollection(enumerable, complexProperty.ComplexType);
                complexProperty.PropertyInfo?.SetValue(entity, list);
            }
        }
    }

    private object DeserializeComplexType(
        IDictionary<string, object> data,
        IComplexType complexType)
    {
        var instance = Activator.CreateInstance(complexType.ClrType);
        DeserializeProperties(instance, data, complexType);
        DeserializeComplexProperties(instance, data, complexType);
        return instance;
    }
}
```

**Archivos:** `Storage/FirestoreDocumentDeserializer.cs`

---

### 7. Integración con IQueryable<T>

Actualizar `FirestoreDatabase` para soportar queries:

```csharp
// En FirestoreDatabase.cs
public async Task<List<T>> ExecuteQueryAsync<T>(
    FirestoreQueryExpression queryExpression,
    CancellationToken cancellationToken = default) where T : class, new()
{
    var executor = new FirestoreQueryExecutor(_firestoreClient, _collectionManager);
    var snapshot = await executor.ExecuteQueryAsync(queryExpression, cancellationToken);

    var deserializer = new FirestoreDocumentDeserializer(_model, _typeMappingSource, _collectionManager);
    var results = new List<T>();

    foreach (var document in snapshot.Documents)
    {
        var entity = deserializer.DeserializeEntity<T>(document);
        results.Add(entity);
    }

    return results;
}
```

---

## 📋 Mapeo LINQ → Firestore

### Operadores de Comparación

| LINQ | Firestore | Notas |
|------|-----------|-------|
| `p => p.Precio == 100` | `WhereEqualTo("Precio", 100)` | ✅ |
| `p => p.Precio != 100` | `WhereNotEqualTo("Precio", 100)` | ✅ |
| `p => p.Precio > 100` | `WhereGreaterThan("Precio", 100)` | ✅ Máximo un rango por query |
| `p => p.Precio >= 100` | `WhereGreaterThanOrEqualTo("Precio", 100)` | ✅ |
| `p => p.Precio < 100` | `WhereLessThan("Precio", 100)` | ✅ |
| `p => p.Precio <= 100` | `WhereLessThanOrEqualTo("Precio", 100)` | ✅ |

### Operadores de Arrays

| LINQ | Firestore | Notas |
|------|-----------|-------|
| `ids.Contains(p.Id)` | `WhereIn("Id", ids)` | ✅ Máximo 30 elementos en el array |
| `p.Tags.Contains("new")` | `WhereArrayContains("Tags", "new")` | ✅ |

### Ordenamiento y Paginación

| LINQ | Firestore | Notas |
|------|-----------|-------|
| `OrderBy(p => p.Nombre)` | `OrderBy("Nombre")` | ✅ |
| `OrderByDescending(p => p.Precio)` | `OrderByDescending("Precio")` | ✅ |
| `ThenBy(p => p.Id)` | `OrderBy("Nombre").OrderBy("Id")` | ✅ Múltiples OrderBy permitidos |
| `Take(10)` | `Limit(10)` | ✅ |
| `Skip(10)` | `StartAfter(cursor)` | ⚠️ Requiere obtener documento cursor primero |

### Operaciones de Agregación

| LINQ | Implementación | Notas |
|------|---------------|-------|
| `Count()` | En memoria después de obtener documentos | ⚠️ Firestore no tiene COUNT nativo |
| `Any()` | `Limit(1)` + verificar si hay resultados | ✅ Optimizado |
| `First()` | `Limit(1)` + deserializar | ✅ |
| `Max/Min/Average` | En memoria | ⚠️ |

---

## 🚀 Plan de Implementación por Fases

### **FASE 1: Fundamentos (Operaciones Básicas de Lectura)**
**Objetivo:** Poder hacer `context.Productos.ToList()` y `context.Productos.Find(id)`

**Tareas:**
1. ✅ Implementar `FirestoreQueryExpression` (clase base)
2. ✅ Implementar `FirestoreDocumentDeserializer` (deserialización básica)
3. ✅ Completar `CreateShapedQueryExpression` en `FirestoreQueryableMethodTranslatingExpressionVisitor`
4. ✅ Implementar `FirestoreQueryExecutor` (construcción y ejecución de queries básicas)
5. ✅ Completar `VisitShapedQuery` en `FirestoreShapedQueryCompilingExpressionVisitor`
6. ✅ Implementar `TranslateSelect` (proyección simple)

**Pruebas:**
```csharp
// Debe funcionar:
var todos = await context.Productos.ToListAsync();
var producto = await context.Productos.FindAsync("prod-001");
```

**Duración Estimada:** 3-5 días

---

### **FASE 2: Filtrado con Where**
**Objetivo:** Soportar `context.Productos.Where(p => p.Precio > 100).ToList()`

**Tareas:**
1. ✅ Implementar `FirestoreExpressionTranslatingExpressionVisitor`
2. ✅ Completar `TranslateWhere` en `FirestoreQueryableMethodTranslatingExpressionVisitor`
3. ✅ Implementar traducción de operadores binarios (==, !=, <, >, <=, >=)
4. ✅ Implementar traducción de `Contains` (para IN y array-contains)
5. ✅ Agregar conversiones de tipos (decimal → double, enum → string)
6. ✅ Manejar múltiples Where encadenados

**Pruebas:**
```csharp
// Debe funcionar:
var productos = await context.Productos
    .Where(p => p.Precio > 100)
    .Where(p => p.Categoria == CategoriaProducto.Electronica)
    .ToListAsync();

var productos2 = await context.Productos
    .Where(p => ids.Contains(p.Id))
    .ToListAsync();
```

**Duración Estimada:** 3-5 días

---

### **FASE 3: Ordenamiento y Paginación**
**Objetivo:** Soportar `OrderBy`, `Take`, y `Skip`

**Tareas:**
1. ✅ Completar `TranslateOrderBy` y `TranslateOrderByDescending`
2. ✅ Completar `TranslateThenBy`
3. ✅ Completar `TranslateTake`
4. ✅ Implementar `TranslateSkip` (usando cursores)
5. ✅ Validar restricciones de Firestore (OrderBy con filtros de rango)

**Pruebas:**
```csharp
// Debe funcionar:
var productos = await context.Productos
    .OrderBy(p => p.Categoria)
    .ThenBy(p => p.Nombre)
    .Take(10)
    .ToListAsync();

var productos2 = await context.Productos
    .Skip(20)
    .Take(10)
    .ToListAsync();
```

**Duración Estimada:** 2-4 días

---

### **FASE 4: Operaciones de Proyección y Agregación**
**Objetivo:** Soportar `FirstOrDefault`, `Count`, `Any`

**Tareas:**
1. ✅ Completar `TranslateFirstOrDefault` (con y sin predicado)
2. ✅ Completar `TranslateSingleOrDefault`
3. ✅ Completar `TranslateCount` (evaluación en memoria)
4. ✅ Completar `TranslateAny` (optimizado con Limit(1))
5. ✅ Implementar `TranslateMax`, `TranslateMin`, `TranslateAverage` (en memoria)

**Pruebas:**
```csharp
// Debe funcionar:
var producto = await context.Productos
    .FirstOrDefaultAsync(p => p.Precio > 100);

var count = await context.Productos
    .Where(p => p.Categoria == CategoriaProducto.Electronica)
    .CountAsync();

var hayProductos = await context.Productos
    .AnyAsync(p => p.Precio > 1000);
```

**Duración Estimada:** 2-3 días

---

### **FASE 5: Manejo de Relaciones y Complex Types**
**Objetivo:** Deserializar entidades con referencias y Value Objects correctamente

**Tareas:**
1. ✅ Deserializar Complex Properties (Value Objects) en `FirestoreDocumentDeserializer`
2. ✅ Deserializar GeoPoint
3. ✅ Deserializar referencias (DocumentReference → marcar para carga lazy)
4. ✅ Implementar carga explícita de referencias (opcional: `Include`)
5. ✅ Deserializar colecciones de Complex Types

**Pruebas:**
```csharp
// Debe funcionar:
var productos = await context.Productos.ToListAsync();
// productos[0].DireccionAlmacen debe estar deserializado
// productos[0].Ubicacion (GeoPoint) debe estar deserializado
```

**Duración Estimada:** 3-4 días

---

### **FASE 6: Optimizaciones y Casos Avanzados**
**Objetivo:** Manejar limitaciones de Firestore y optimizar rendimiento

**Tareas:**
1. ⚠️ Implementar detección de queries no soportadas (OR compuesto, múltiples rangos)
2. ⚠️ Implementar evaluación parcial en memoria para queries complejas
3. ⚠️ Implementar caché de metadata de entidades
4. ⚠️ Agregar logging detallado de queries generadas
5. ⚠️ Implementar validación de restricciones de Firestore en compile-time
6. ⚠️ Optimizar deserialización con expression compilation

**Pruebas:**
```csharp
// Debe lanzar excepción clara:
var productos = await context.Productos
    .Where(p => p.Precio > 10 || p.Stock < 100) // OR compuesto
    .ToListAsync();
// InvalidOperationException: "Firestore does not support OR queries..."

// Debe evaluar parcialmente en memoria:
var productos2 = await context.Productos
    .Where(p => p.Precio > 10)
    .Where(p => p.Stock < 100) // Segundo rango - en memoria
    .ToListAsync();
```

**Duración Estimada:** 4-6 días

---

## ⚠️ Limitaciones Conocidas

### Limitaciones de Firestore (No superables)

1. **OR Compuesto:**
   - ❌ `Where(p => p.A == 1 || p.B == 2)` no es soportado directamente
   - **Solución:** Requiere ejecutar múltiples queries y merge en memoria

2. **Múltiples Filtros de Rango:**
   - ❌ `Where(p => p.Precio > 10 && p.Stock < 100)` no es válido
   - **Solución:** Un filtro en Firestore, el otro en memoria

3. **OrderBy con Filtros de Rango:**
   - ⚠️ Si usas `Where(p => p.Precio > 10)`, el primer `OrderBy` debe ser por `Precio`
   - **Solución:** Validar en compile-time y lanzar excepción clara

4. **JOINs:**
   - ❌ Firestore no soporta JOINs
   - **Solución:** Navegaciones requieren queries separadas (eager/lazy loading)

5. **GROUP BY:**
   - ❌ Firestore no tiene GROUP BY nativo
   - **Solución:** Obtener datos y agrupar en memoria

### Limitaciones de Implementación (Superables en el futuro)

1. **Skip:**
   - ⚠️ Implementación actual requiere obtener todos los documentos hasta el cursor
   - **Mejora futura:** Usar pagination tokens persistentes

2. **Count:**
   - ⚠️ Requiere obtener todos los documentos para contar
   - **Mejora futura:** Usar Firestore aggregation queries (beta en v9)

3. **Include (Eager Loading):**
   - ❌ No implementado en fases iniciales
   - **Mejora futura:** Implementar carga explícita de referencias

---

## 🎯 Criterios de Éxito

### Fase 1
- ✅ `context.Productos.ToListAsync()` devuelve todas las entidades
- ✅ `context.Productos.FindAsync(id)` encuentra por ID
- ✅ Entidades deserializadas correctamente (propiedades simples)

### Fase 2
- ✅ `Where` con todos los operadores de comparación funciona
- ✅ Múltiples `Where` encadenados funcionan
- ✅ Conversiones automáticas (decimal→double, enum→string) aplicadas

### Fase 3
- ✅ `OrderBy`, `ThenBy` funcionan correctamente
- ✅ `Take` limita resultados
- ✅ `Skip` con paginación funciona

### Fase 4
- ✅ `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync` funcionan
- ✅ Operaciones de agregación en memoria funcionan

### Fase 5
- ✅ Complex Properties y GeoPoint deserializados correctamente
- ✅ Referencias marcadas para carga lazy

### Fase 6
- ✅ Queries no soportadas lanzan excepciones claras
- ✅ Evaluación parcial en memoria funciona para casos complejos
- ✅ Logging muestra queries generadas para debugging

---

## 📦 Archivos a Crear/Modificar

### Nuevos Archivos
1. ✅ `Query/FirestoreQueryExpression.cs` (clases de expresión)
2. `Query/FirestoreExpressionTranslatingExpressionVisitor.cs`
3. ✅ `Query/FirestoreQueryExecutor.cs`
4. ✅ `Storage/FirestoreDocumentDeserializer.cs`

### Archivos a Modificar
1. ✅ `Infrastructure/FirestoreServiceCollectionExtensions.cs`:
   - ✅ Implementar `CreateShapedQueryExpression` en `FirestoreQueryableMethodTranslatingExpressionVisitor`
   - ✅ Implementar `TranslateSelect` en `FirestoreQueryableMethodTranslatingExpressionVisitor`
   - ✅ Implementar `VisitShapedQuery` en `FirestoreShapedQueryCompilingExpressionVisitor`
   - Pendiente: Completar otros métodos Translate (Where, OrderBy, Take, etc.)

2. `Storage/FirestoreDatabase.cs`:
   - Agregar método `ExecuteQueryAsync<T>`

3. `Infrastructure/IFirestoreClientWrapper.cs`:
   - Ya tiene `ExecuteQueryAsync`, no requiere cambios

---

## 🔧 Consideraciones Técnicas

### Conversión de Tipos
Todas las conversiones deben ser bidireccionales:

| C# → Firestore (Escritura) | Firestore → C# (Lectura) |
|----------------------------|--------------------------|
| decimal → double | double → decimal |
| enum → string | string → enum |
| DateTime → Timestamp | Timestamp → DateTime |
| List<decimal> → double[] | double[] → List<decimal> |
| List<enum> → string[] | string[] → List<enum> |

### Manejo de Errores
- Lanzar excepciones claras para queries no soportadas
- Incluir en el mensaje la limitación de Firestore y posible workaround
- Logging de queries generadas para debugging

### Performance
- Minimizar queries a Firestore
- Usar Limit cuando sea posible
- Evaluar en memoria solo cuando sea necesario
- Considerar caché de metadata de entidades

---

## 📝 Notas Adicionales

### Testing
Cada fase debe incluir:
- Tests unitarios para componentes individuales
- Tests de integración con Firestore Emulator
- Tests de validación de limitaciones

### Documentación
Actualizar documentación con:
- Ejemplos de queries soportadas
- Lista de limitaciones
- Guía de workarounds para casos no soportados

### Compatibilidad
- Mantener compatibilidad con EF Core 8.0
- Seguir convenciones de EF Core para providers personalizados
- Usar el SDK oficial de Google.Cloud.Firestore

---

## ✅ Checklist de Implementación

### Fase 1: Fundamentos ✅
- [x] Crear `FirestoreQueryExpression.cs`
- [x] Crear `FirestoreDocumentDeserializer.cs`
- [x] Implementar `CreateShapedQueryExpression`
- [x] Crear `FirestoreQueryExecutor.cs`
- [x] Implementar `VisitShapedQuery`
- [x] Implementar `TranslateSelect`
- [ ] Tests: ToList, Find

### Fase 2: Filtrado
- [ ] Crear `FirestoreExpressionTranslatingExpressionVisitor.cs`
- [ ] Implementar `TranslateWhere`
- [ ] Implementar traducción de operadores binarios
- [ ] Implementar conversiones de tipos
- [ ] Tests: Where con diferentes operadores

### Fase 3: Ordenamiento
- [ ] Implementar `TranslateOrderBy`
- [ ] Implementar `TranslateThenBy`
- [ ] Implementar `TranslateTake`
- [ ] Implementar `TranslateSkip`
- [ ] Tests: OrderBy, Take, Skip

### Fase 4: Agregaciones
- [ ] Implementar `TranslateFirstOrDefault`
- [ ] Implementar `TranslateCount`
- [ ] Implementar `TranslateAny`
- [ ] Tests: First, Count, Any

### Fase 5: Relaciones
- [ ] Deserializar Complex Properties
- [ ] Deserializar GeoPoint
- [ ] Deserializar referencias
- [ ] Tests: Entidades con relaciones

### Fase 6: Optimizaciones
- [ ] Detección de queries no soportadas
- [ ] Evaluación parcial en memoria
- [ ] Logging de queries
- [ ] Tests: Casos edge

---

**Tiempo Total Estimado:** 17-27 días de desarrollo
**Prioridad:** Alta - Funcionalidad crítica para que el provider sea usable

---

## 🎓 Referencias

- [EF Core Query Pipeline](https://learn.microsoft.com/en-us/ef/core/providers/writing-a-provider?tabs=netcore-cli#query-pipeline)
- [Firestore Query Documentation](https://firebase.google.com/docs/firestore/query-data/queries)
- [Google.Cloud.Firestore API Reference](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest)
- [EF Core Provider Development](https://learn.microsoft.com/en-us/ef/core/providers/)

---

**Última actualización:** 26 de noviembre de 2025
**Estado del Plan:** ✅ Completo y listo para aprobación
