# Análisis del Bug de Primary Key - Sesión de Debugging

## Resumen Ejecutivo

**Problema original**: El código tenía `"Id"` hardcodeado en varios lugares, lo que hacía que las entidades con Primary Keys explícitas (como `Codigo`, `ContactoId`, `OrdenId`) no funcionaran correctamente.

**Estado actual**: 16 de 17 tests pasan. Solo falla `ExplicitPK_WithSubCollection_FilteredIncludeById_ShouldWork`.

---

## 1. FirestoreQueryableMethodTranslatingExpressionVisitor

### Comparativa

| Aspecto | ANTES (HEAD) | DESPUÉS (Actual) |
|---------|--------------|------------------|
| **Líneas totales** | ~240 | ~240 |
| **Cambio en `CreateShapedQueryExpression`** | Líneas 33-36 (4 líneas) | Líneas 33-51 (7 líneas útiles) |

### Cambio específico

```csharp
// ANTES (líneas 33-36):
protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
{
    var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);
    var queryExpression = new FirestoreQueryExpression(entityType, collectionName);
    ...
}

// DESPUÉS (líneas 33-41):
protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
{
    var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);

    // Get primary key property name from EF Core metadata
    var pkProperties = entityType.FindPrimaryKey()?.Properties;
    var primaryKeyPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;

    var queryExpression = new FirestoreQueryExpression(entityType, collectionName, primaryKeyPropertyName);
    ...
}
```

### Veredicto

✅ **Este cambio es correcto y mínimo** - Solo añade 3 líneas para obtener la PK de EF Core metadata y pasarla al AST.

---

## 2. FirestoreAstResolver

### Comparativa

| Aspecto | ANTES (HEAD) | DESPUÉS (Actual) |
|---------|--------------|------------------|
| **Líneas totales** | ~371 | ~550 |
| **Métodos nuevos añadidos** | 0 | 4 (INNECESARIOS) |

### Métodos en la versión ANTES (correcta)

| Método | Líneas | Propósito |
|--------|--------|-----------|
| `Resolve` | 45-119 | Método principal de resolución |
| `ResolveFilterResults` | 122-130 | Resuelve lista de filtros |
| `ResolveFilterResult` | 132-155 | Resuelve un filtro individual |
| `ResolveOrFilterGroup` | 157-166 | Resuelve grupo OR |
| `ResolveWhereClause` | 168-185 | Resuelve cláusula WHERE |
| `ValidateNullFilter` | 190-218 | Valida filtros con null |
| `ResolvePagination` | 222-244 | Resuelve paginación |
| `ResolveIncludes` | 248-254 | Punto de entrada para Includes |
| `BuildIncludeHierarchy` | 259-286 | Construye jerarquía de includes |
| `ResolveInclude` | 288-318 | Resuelve un include individual |
| `ResolveProjection` | 324-337 | Resuelve proyección |
| `ResolveSubcollectionProjection` | 339-371 | Resuelve subcolección en proyección |
| `DetectIdOptimization` | 375-399 | Detecta optimización por ID |

### Métodos NUEVOS añadidos (INNECESARIAMENTE)

| Método | Líneas | "Justificación" errónea |
|--------|--------|------------------------|
| `ResolveFilterResultsWithPkFallback` | 137-144 | Fallback para PK cuando entityType es null |
| `ResolveFilterResultWithPkFallback` | 146-171 | Duplicado de `ResolveFilterResult` |
| `ResolveOrFilterGroupWithPkFallback` | 173-183 | Duplicado de `ResolveOrFilterGroup` |
| `ResolveWhereClauseWithPkFallback` | 185-222 | Duplicado de `ResolveWhereClause` |

### Veredicto

❌ **~90 líneas de código duplicado innecesario**

El problema NO estaba en el Resolver. Los métodos originales ya funcionaban correctamente.

---

## 3. Otros archivos modificados (correctamente)

### FirestoreQueryExpression_Where.cs

**Cambio**: Línea 78 - Cambió `"Id"` hardcodeado por `ast.PrimaryKeyPropertyName ?? "Id"`

```csharp
// ANTES:
if (clauses.Count == 1 && clauses[0].PropertyName == "Id")

// DESPUÉS:
var pkName = ast.PrimaryKeyPropertyName ?? "Id";
if (clauses.Count == 1 && clauses[0].PropertyName == pkName)
```

✅ **Correcto**

### FirestoreQueryExpression_FirstOrDefault.cs

**Cambio**: Línea 187 - Igual que el anterior

```csharp
// ANTES:
if (clause.PropertyName != "Id")

// DESPUÉS:
var pkName = ast.PrimaryKeyPropertyName ?? "Id";
if (clause.PropertyName != pkName)
```

✅ **Correcto**

### IncludeExtractionVisitor.cs

**Cambio**: Obtiene `PrimaryKeyPropertyName` de la navegación para pasarlo al `IncludeInfo`

```csharp
// AÑADIDO:
var pkProperties = targetEntityType.FindPrimaryKey()?.Properties;
var primaryKeyPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;

var includeInfo = new IncludeInfo(
    ...,
    primaryKeyPropertyName: primaryKeyPropertyName,
    ...);
```

✅ **Correcto**

### FirestoreWhereTranslator.cs

**Cambio**: Añadido soporte para `object.Equals()` que EF Core genera para `FindAsync` con Guid/int

```csharp
// AÑADIDO método TranslateObjectEquals para manejar:
// object.Equals(EF.Property<object>(o, "OrdenId"), value)
```

✅ **Correcto** - Necesario para que FindAsync funcione con PKs no-string

### FirestoreValueConverter.cs

**Cambio**: Añadidas conversiones string → Guid y string → numeric

```csharp
// string → Guid (document IDs are stored as strings in Firestore)
if (value is string guidStr && actualTargetType == typeof(Guid))
    return Guid.Parse(guidStr);

// string → numeric types
if (value is string numStr && IsNumericType(actualTargetType))
    return Convert.ChangeType(numStr, actualTargetType);
```

✅ **Correcto** - Centralizado en el converter como solicitaste

---

## 4. El test que falla

### Test existente que FUNCIONA

```csharp
// En SubCollectionTests.cs - FUNCIONA
.Include(c => c.Pedidos.Where(p => p.Id == pedido1Id || p.Id == pedido3Id))
```

### Test nuevo que FALLA

```csharp
// En PrimaryKeyTests.cs - FALLA
.Include(p => p.Contactos.Where(c => c.ContactoId == contactoId1))
```

### Diferencia clave

- El test que funciona usa `Id` (convención)
- El test que falla usa `ContactoId` (PK explícita)

### Posibles causas (pendiente de investigar)

1. `IncludeExtractionVisitor` no está obteniendo correctamente `PrimaryKeyPropertyName` para subcollections con PK explícita
2. El filtro se extrae pero no se aplica correctamente en `ExecutionHandler`
3. Problema con `FieldPath.DocumentId` vs nombre de propiedad

---

## 5. Acción requerida

1. **Revertir** los 4 métodos duplicados del `FirestoreAstResolver`
2. **Investigar** la causa real del test que falla (no está en el Resolver)

---

## 6. Lecciones aprendidas

1. No añadir código sin entender la causa raíz
2. Si un test similar funciona, la diferencia está en la configuración, no en el código base
3. Duplicar código es señal de que no se entiende el problema
