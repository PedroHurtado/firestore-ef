# Refactor: Select en el AST

## Problema Actual

El AST (`FirestoreQueryExpression`) maneja WHERE y ORDER correctamente con datos estructurados, pero SELECT guarda la lambda sin procesar:

```csharp
// WHERE - Bien diseñado (datos estructurados)
public List<FirestoreWhereClause> Filters { get; set; }

// ORDER BY - Bien diseñado (datos estructurados)
public List<FirestoreOrderByClause> OrderByClauses { get; set; }

// SELECT - Mal diseñado (lambda sin procesar)
public LambdaExpression? ProjectionSelector { get; set; }  // ← PROBLEMA
```

Esto causó que el shaper cargara toda la entidad en memoria y filtrara después.

---

## Solución: Estructurar SELECT como WHERE/ORDER

### Nuevas clases a crear

#### 1. `FirestoreProjectionDefinition.cs`

```csharp
public class FirestoreProjectionDefinition
{
    public ProjectionResultType ResultType { get; set; }
    public Type ClrType { get; set; }
    public List<FirestoreProjectedField> Fields { get; set; }
    public List<FirestoreSubcollectionProjection> Subcollections { get; set; }
}

public enum ProjectionResultType
{
    Entity,           // e => e
    SingleField,      // e => e.Name
    AnonymousType,    // e => new { e.Id, e.Name }
    DtoClass,         // e => new Dto { Id = e.Id }
    Record            // e => new Record(e.Id, e.Name)
}
```

#### 2. `FirestoreProjectedField.cs`

```csharp
public class FirestoreProjectedField
{
    public string FieldPath { get; set; }      // "Name", "Direccion.Ciudad"
    public string ResultName { get; set; }      // Alias en el resultado
    public Type FieldType { get; set; }
    public int ConstructorParameterIndex { get; set; } = -1;  // Para records
}
```

#### 3. `FirestoreSubcollectionProjection.cs`

```csharp
public class FirestoreSubcollectionProjection
{
    public string NavigationName { get; set; }
    public string ResultName { get; set; }
    public string CollectionName { get; set; }
    public IEntityType EntityType { get; set; }

    // REUTILIZA tipos existentes
    public List<FirestoreWhereClause> Filters { get; set; }
    public List<FirestoreOrderByClause> OrderByClauses { get; set; }
    public int? Limit { get; set; }

    public List<FirestoreProjectedField>? Fields { get; set; }

    // Agregaciones en subcollection
    public FirestoreAggregationType? Aggregation { get; set; }
    public string? AggregationPropertyName { get; set; }

    // Anidamiento
    public List<FirestoreSubcollectionProjection> NestedSubcollections { get; set; }
}
```

---

## Los 8 casos de Select

| # | Patrón | ResultType | Fields | Subcollections |
|---|--------|------------|--------|----------------|
| 1 | `e => e` | Entity | null | null |
| 2 | `e => e.Name` | SingleField | [Name] | null |
| 3 | `e => new { e.Id, e.Name }` | AnonymousType | [Id, Name] | null |
| 4 | `e => new Dto { ... }` | DtoClass | [...] | null |
| 5 | `e => new Record(...)` | Record | [...] con índices | null |
| 6 | `e => e.Direccion` | SingleField | [Direccion.*] | null |
| 7 | `e => e.Pedidos` | Entity | null | [Pedidos] |
| 8 | `e => new { ..., Items = e.Pedidos.Where().Take() }` | AnonymousType | [...] | [Pedidos+filtros] |

---

## Tareas

### Fase 1: Crear estructuras del AST
- [x] Crear `FirestoreProjectionDefinition.cs` (804dff9)
- [x] Crear `FirestoreProjectedField.cs` (804dff9)
- [x] Crear `FirestoreSubcollectionProjection.cs` (804dff9)
- [x] Tests unitarios para las estructuras (804dff9)

### Fase 2: Modificar AST
- [x] Agregar `Projection` a `FirestoreQueryExpression` (d3948df)
- [x] Eliminar `ProjectionSelector`, `ProjectionType`, `HasSubcollectionProjection` (d3948df)
- [x] Actualizar método `Update()` y `WithProjection()` (d3948df)
- [x] Eliminar código legacy: `FirestoreProjectionQueryingEnumerable` (d3948df)
- [x] Limpiar Visitor y Shaper de código de proyección obsoleto (d3948df)
- [x] Extraer tests a archivos separados (d3948df)

### Fase 3: Modificar Visitor
- [ ] Crear `FirestoreProjectionTranslator` (similar a `FirestoreWhereTranslator`)
- [ ] Modificar `TranslateSelect` para usar el translator
- [ ] Tests unitarios para el translator

### Fase 4: Modificar Executor
- [ ] Implementar lectura de campos específicos (no entidad completa)
- [ ] Implementar queries de subcollection con filtros en Firestore
- [ ] Implementar ensamblado del resultado de proyección

### Fase 5: Limpiar código legacy
- [ ] Eliminar `ProjectionSelectorCleaner` del shaper
- [ ] Eliminar código comentado de proyecciones
- [ ] Eliminar `FirestoreProjectionQueryingEnumerable` si ya no se usa

### Fase 6: Tests de integración
- [ ] Habilitar tests de `SelectTests.cs`
- [ ] Habilitar tests de `SelectComplexTypeTests.cs`
- [ ] Habilitar tests de `SelectWhereTests.cs`
- [ ] Habilitar tests de `SelectSubcollectionTests.cs`

---

## Deuda técnica actual

- **23 tests de proyección en Skip** (commit efee08f)
  - `SelectTests.cs` - 4 tests
  - `SelectComplexTypeTests.cs` - 4 tests
  - `SelectWhereTests.cs` - 8 tests
  - `SelectSubcollectionTests.cs` - 7 tests

Estos tests estaban correctos. Se pusieron en Skip porque la implementación era incorrecta (cargaba todo en memoria). No se deben quitar los Skip hasta tener la implementación correcta.

## Commits realizados

| Commit | Descripción |
|--------|-------------|
| efee08f | Skip tests de proyección por implementación incorrecta (deuda técnica) |
| 804dff9 | feat: add projection AST structures for Select clause |
| d3948df | refactor: add Projection property to AST and remove legacy projection code |

---

## Notas

- Los filtros de subcollection deben ejecutarse en Firestore, NO en memoria
- Reutilizar `FirestoreWhereClause` y `FirestoreOrderByClause` para subcollections
- El Visitor traduce, el Executor ejecuta (separación de responsabilidades)
