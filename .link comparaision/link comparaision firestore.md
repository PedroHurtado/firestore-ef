# Comparativa Firestore vs LINQ

## 1. Operadores de Comparación

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Igualdad | `.WhereEqualTo("Field", value)` | `.Where(x => x.Field == value)` | ✅ |
| Desigualdad | `.WhereNotEqualTo("Field", value)` | `.Where(x => x.Field != value)` | ✅ |
| Mayor que | `.WhereGreaterThan("Field", value)` | `.Where(x => x.Field > value)` | ✅ |
| Mayor o igual | `.WhereGreaterThanOrEqualTo("Field", value)` | `.Where(x => x.Field >= value)` | ✅ |
| Menor que | `.WhereLessThan("Field", value)` | `.Where(x => x.Field < value)` | ✅ |
| Menor o igual | `.WhereLessThanOrEqualTo("Field", value)` | `.Where(x => x.Field <= value)` | ✅ |

---

## 2. Operadores de Arrays

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Array contiene | `.WhereArrayContains("Tags", "csharp")` | `.Where(x => x.Tags.Contains("csharp"))` | ✅ |
| Array contiene alguno | `.WhereArrayContainsAny("Tags", new[] {"a", "b"})` | `.Where(x => x.Tags.Any(t => list.Contains(t)))` | ✅ |
| | | `.Where(x => x.Tags.Intersect(list).Any())` | ✅ |

---

## 3. Operadores IN / NOT-IN

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| In (valor en lista) | `.WhereIn("Country", new[] {"USA", "Japan"})` | `.Where(x => list.Contains(x.Country))` | ✅ |
| | | `.Where(x => new[] {"USA", "Japan"}.Contains(x.Country))` | ✅ |
| Not In | `.WhereNotIn("Status", new[] {"deleted", "archived"})` | `.Where(x => !list.Contains(x.Status))` | ✅ |

---

## 4. Operadores Lógicos (AND / OR)

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| AND implícito | `.WhereEqualTo("A", 1).WhereEqualTo("B", 2)` | `.Where(x => x.A == 1 && x.B == 2)` | ✅ |
| AND explícito | `Filter.And(filter1, filter2)` | `.Where(x => x.A == 1 && x.B == 2)` | ✅ |
| OR | `Filter.Or(filter1, filter2)` | `.Where(x => x.A == 1 \|\| x.B == 2)` | ✅ |
| Combinado | `Filter.And(f1, Filter.Or(f2, f3))` | `.Where(x => x.A == 1 && (x.B == 2 \|\| x.C == 3))` | ✅ |

---

## 5. Ordenamiento

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Ascendente | `.OrderBy("Field")` | `.OrderBy(x => x.Field)` | ✅ |
| Descendente | `.OrderByDescending("Field")` | `.OrderByDescending(x => x.Field)` | ✅ |
| Múltiple ASC | `.OrderBy("A").OrderBy("B")` | `.OrderBy(x => x.A).ThenBy(x => x.B)` | ✅ |
| Múltiple DESC | `.OrderByDescending("A").OrderByDescending("B")` | `.OrderByDescending(x => x.A).ThenByDescending(x => x.B)` | ✅ |
| Mixto | `.OrderBy("A").OrderByDescending("B")` | `.OrderBy(x => x.A).ThenByDescending(x => x.B)` | ✅ |

---

## 6. Límites y Paginación

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Primeros N | `.Limit(10)` | `.Take(10)` | ✅ |
| Últimos N | `.LimitToLast(10)` | `.TakeLast(10)` | ✅ |
| Saltar N | `.Offset(10)` | `.Skip(10)` | ⚠️ Ineficiente* |
| Skip + Take | `.Offset(10).Limit(5)` | `.Skip(10).Take(5)` | ⚠️ Ineficiente* |
| Primero | `.Limit(1)` | `.First()` / `.FirstOrDefault()` | ✅ |
| Único | `.Limit(2)` + validación | `.Single()` / `.SingleOrDefault()` | ✅ |

> *Firestore cobra por todos los documentos leídos incluso con Offset. Se recomienda usar cursores.

---

## 7. Cursores (Paginación Eficiente)

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Empezar en | `.StartAt(value)` | `.Where(x => x.Field >= value)` | ⚠️ Aproximado |
| Empezar después | `.StartAfter(value)` | `.Where(x => x.Field > value)` | ⚠️ Aproximado |
| Terminar en | `.EndAt(value)` | `.Where(x => x.Field <= value)` | ⚠️ Aproximado |
| Terminar antes | `.EndBefore(value)` | `.Where(x => x.Field < value)` | ⚠️ Aproximado |
| Por Snapshot | `.StartAfter(documentSnapshot)` | N/A (requiere extensión custom) | 🔧 Custom |

> Los cursores de Firestore son más potentes porque trabajan con múltiples campos ordenados simultáneamente.

---

## 8. Agregaciones

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Contar | `.Count().GetSnapshotAsync()` | `.Count()` / `.CountAsync()` | ✅ |
| | | `.LongCount()` | ✅ |
| Sumar | `.Aggregate(AggregateField.Sum("Price"))` | `.Sum(x => x.Price)` | ✅ |
| Promedio | `.Aggregate(AggregateField.Average("Price"))` | `.Average(x => x.Price)` | ✅ |
| Mínimo | ❌ No soportado nativamente | `.Min(x => x.Price)` | ❌ Client-side |
| Máximo | ❌ No soportado nativamente | `.Max(x => x.Price)` | ❌ Client-side |
| Any | `.Limit(1)` + verificar resultado | `.Any()` / `.Any(x => x.Active)` | ✅ |
| All | ❌ No soportado | `.All(x => x.Active)` | ❌ Client-side |

---

## 9. Proyecciones (Select)

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Select campos | `.Select("Name", "Age")` | `.Select(x => new { x.Name, x.Age })` | ✅ |
| Select a DTO | `.Select("Name", "Age")` + mapeo | `.Select(x => new PersonDto { Name = x.Name })` | ✅ |
| Select campo único | `.Select("Name")` | `.Select(x => x.Name)` | ✅ |

---

## 10. Consultas de Existencia / Null

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Campo es null | `.WhereEqualTo("Field", null)` | `.Where(x => x.Field == null)` | ✅ |
| Campo no es null | `.WhereNotEqualTo("Field", null)` | `.Where(x => x.Field != null)` | ✅ |
| Campo existe | Implícito en filtros | `.Where(x => EF.Property<object>(x, "Field") != null)` | ⚠️ |

---

## 11. Strings (Limitado en Firestore)

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Igualdad exacta | `.WhereEqualTo("Name", "John")` | `.Where(x => x.Name == "John")` | ✅ |
| Prefijo (StartsWith) | `.WhereGreaterThanOrEqualTo("Name", "Jo").WhereLessThan("Name", "Jp")` | `.Where(x => x.Name.StartsWith("Jo"))` | ⚠️ Workaround |
| Contains | ❌ No soportado | `.Where(x => x.Name.Contains("oh"))` | ❌ Client-side |
| EndsWith | ❌ No soportado | `.Where(x => x.Name.EndsWith("hn"))` | ❌ Client-side |
| ToLower/ToUpper | ❌ No soportado | `.Where(x => x.Name.ToLower() == "john")` | ❌ Client-side |
| Like/Regex | ❌ No soportado | `EF.Functions.Like(x.Name, "%pattern%")` | ❌ No soportable |

---

## 12. Collection Group Queries

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Query en todas las subcolecciones | `db.CollectionGroup("reviews")` | Requiere configuración en `OnModelCreating` | 🔧 Custom |

```csharp
// Posible API en tu provider
modelBuilder.Entity<Review>()
    .ToCollection("reviews")
    .AsCollectionGroup(); // Marca como collection group

// LINQ
context.Reviews.Where(r => r.Rating > 4); // Busca en TODAS las subcolecciones "reviews"
```

---

## 13. Subcollections / Navegación

| Operación | Firestore SDK (.NET) | LINQ Equivalent | Soportado |
|-----------|---------------------|-----------------|-----------|
| Acceder subcolección | `doc.Collection("orders")` | `.Include(x => x.Orders)` | ✅ |
| Filtrar en subcolección | `doc.Collection("orders").WhereEqualTo(...)` | `.Include(x => x.Orders.Where(o => o.Status == "Pending"))` | 🔧 Filtered Include |
| Navegar profundo | `doc.Collection("orders").Document(id).Collection("items")` | `.ThenInclude(o => o.Items)` | ✅ |

---

## 14. Operaciones No Soportadas en Firestore

| LINQ Operation | Razón | Alternativa |
|----------------|-------|-------------|
| `.GroupBy()` | Firestore no soporta agrupaciones | Client-side o rediseñar datos |
| `.Join()` | NoSQL no tiene JOINs nativos | Cargar por separado y unir en memoria |
| `.Distinct()` | No soportado | Client-side |
| `.Union()` / `.Intersect()` / `.Except()` | No soportado | Múltiples queries + merge en cliente |
| `.Reverse()` | No soportado directamente | Cambiar OrderBy direction |
| Subqueries | No soportado | Múltiples queries |
| `.Contains()` en string | No hay full-text search | Solución externa (Algolia, Elasticsearch) o Vector Search |

---

## 15. Vector Search (Extensión)

| Operación | Firestore SDK (.NET) | LINQ Equivalent (Custom) | Soportado |
|-----------|---------------------|--------------------------|-----------|
| KNN Search | `.FindNearest(vectorField, queryVector, limit, distance)` | `.FindNearest(x => x.Embedding, vector, 10, DistanceMeasure.Cosine)` | 🔧 Custom Extension |

```csharp
// Posible API personalizada
context.Products
    .Where(p => p.Category == "Electronics")
    .FindNearest(p => p.Embedding, queryVector, limit: 10, DistanceMeasure.Euclidean);
```

---

## 16. Restricciones Importantes de Firestore

### Límites de Operadores

| Restricción | Límite |
|-------------|--------|
| Valores en `in` / `array-contains-any` | Máximo 30 |
| Valores en `not-in` | Máximo 10 |
| Disyunciones (OR) | Máximo 30 en forma normal disyuntiva |
| Condiciones de rango por query | Solo 1 campo |
| `array-contains` por disyunción | Solo 1 |

### Combinaciones No Permitidas

| Combinación | Permitido |
|-------------|-----------|
| `not-in` + `!=` | ❌ |
| `not-in` + `in` | ❌ |
| `not-in` + `array-contains-any` | ❌ |
| `not-in` + `or` | ❌ |
| `array-contains` + `array-contains-any` (misma disyunción) | ❌ |
| Rango en campo A + OrderBy en campo B (sin índice compuesto) | ❌ |

### Índices

| Tipo | Creación |
|------|----------|
| Single-field | Automático |
| Composite | Manual (console o CLI) |
| Vector | Manual (gcloud CLI) |
| Collection Group | Manual con scope específico |

---

## Resumen de Soporte

| Categoría | ✅ Completo | ⚠️ Parcial | ❌ No Soportado |
|-----------|-------------|------------|-----------------|
| Comparaciones básicas | 6/6 | - | - |
| Operadores lógicos | AND, OR | - | - |
| Arrays | 2/2 | - | - |
| IN/NOT-IN | 2/2 | - | - |
| Ordenamiento | 5/5 | - | - |
| Límites | Take, First, Single | Skip (ineficiente) | - |
| Agregaciones | Count, Sum, Average, Any | - | Min, Max, All |
| Strings | Equality | StartsWith | Contains, EndsWith, Like |
| Proyecciones | Select | - | - |
| Navegación | Include, ThenInclude | Filtered Include | - |
| Joins/Groups | - | - | GroupBy, Join, Distinct |

---

## Leyenda

| Símbolo | Significado |
|---------|-------------|
| ✅ | Soportado completamente - traducción directa |
| ⚠️ | Soportado parcialmente - requiere workaround o tiene limitaciones |
| ❌ | No soportado - requiere evaluación client-side o no es posible |
| 🔧 | Requiere implementación custom / extensión del provider |
