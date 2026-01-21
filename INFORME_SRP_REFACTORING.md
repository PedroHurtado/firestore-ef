# Informe SRP: SnapshotShaper y Materializer

## 1. Catálogo de Escenarios

### A. Proyecciones Simples
| LINQ | Tipo |
|------|------|
| `e => e.Name` | `string` |
| `e => new { e.Id, e.Name }` | Anónimo |
| `e => new Dto { Id = e.Id }` | DTO |
| `e => new Record(e.Id)` | Record |

### B. ComplexTypes
| LINQ | Tipo |
|------|------|
| `p => p.Direccion` | `Direccion` |
| `p => p.Direccion.Ciudad` | `string` |
| `p => new { p.Direccion.Coordenadas.Altitud }` | Anónimo |

### C. FK References
| LINQ | Tipo |
|------|------|
| `l => new { AutorNombre = l.Autor.Nombre }` | Anónimo |
| `l => new { l.Autor }` | Anónimo con entidad |

### D. Subcollections
| LINQ | Tipo |
|------|------|
| `c => new { c.Pedidos }` | Lista de entidades |
| `c => new { Totales = c.Pedidos.Select(p => p.Total) }` | Lista escalar |
| `c => new { Items = c.Pedidos.Select(p => new { p.NumeroOrden }) }` | Lista de anónimos |
| `c => new { Total = c.Pedidos.Sum(p => p.Total) }` | Escalar |

---

## 2. El Problema

El Handler extrae datos del AST y los pasa al Materializer:

```csharp
var projectedFields = resolved.Projection?.Fields;
var subcollections = resolved.Projection?.Subcollections;
var materializedItems = _materializer.Materialize(shapedResult, context.ResultType, projectedFields, subcollections);
```

El Materializer recibe el AST directamente → viola SRP.

---

## 3. La Solución: Cada Key-Value Sabe Cómo Comportarse

### Concepto

El diccionario actual es `Dictionary<string, object?>` donde la clave es un string y el valor es un objeto sin información de tipo.

**El Materializer no sabe:**
- Si el valor es escalar o complejo
- Si es una subcollection escalar o de objetos
- Qué tipo CLR debe tener

**Esta información está en el AST** (`FirestoreProjectedField`):
- `FieldPath` - dónde buscar
- `ResultName` - nombre de la propiedad destino
- `FieldType` - tipo CLR

### Nueva Estructura

```csharp
/// <summary>
/// Un valor con su metadata de materialización.
/// </summary>
public record ShapedValue(
    object? Value,           // El valor extraído de Firestore
    Type TargetType,         // Tipo CLR destino (del AST: FieldType)
    ValueKind Kind           // Cómo materializar
);

public enum ValueKind
{
    Scalar,          // Valor simple: string, int, decimal, enum, DateTime
    ComplexType,     // Objeto anidado: Direccion, Coordenadas
    Entity,          // Entidad relacionada (FK): Autor
    ScalarList,      // Lista de valores: List<decimal>
    ObjectList       // Lista de objetos: List<Pedido> o List<anónimo>
}

/// <summary>
/// Resultado del shaping con metadata para materialización.
/// </summary>
public class ShapedResult
{
    public List<ShapedItem> Items { get; init; }
    public bool HasProjection { get; init; }
}

/// <summary>
/// Un item con sus valores etiquetados.
/// </summary>
public class ShapedItem
{
    /// <summary>
    /// Valores indexados por ResultName.
    /// Cada valor incluye su tipo y cómo materializarlo.
    /// </summary>
    public Dictionary<string, ShapedValue> Values { get; init; }
}
```

---

## 4. Cómo el Shaper Construye ShapedValue

### Desde FirestoreProjectedField

```csharp
// El AST tiene:
FirestoreProjectedField {
    FieldPath = "Direccion.Ciudad",
    ResultName = "Ciudad",
    FieldType = typeof(string)
}

// El Shaper construye:
new ShapedValue(
    Value: "Madrid",
    TargetType: typeof(string),
    Kind: ValueKind.Scalar
)
```

### Desde ResolvedSubcollectionProjection

```csharp
// AST para subcollection escalar:
ResolvedSubcollectionProjection {
    ResultName = "Totales",
    Fields = [{ FieldPath = "Total", ResultName = "Total", FieldType = typeof(decimal) }]
}

// El Shaper detecta: 1 campo de tipo simple → escalar
// Construye:
new ShapedValue(
    Value: new List<object> { 150.50, 299.99 },  // Valores extraídos
    TargetType: typeof(IEnumerable<decimal>),
    Kind: ValueKind.ScalarList
)
```

### Clasificación de ValueKind

```csharp
private static ValueKind DetermineValueKind(Type type, object? value)
{
    if (IsSimpleType(type))
        return ValueKind.Scalar;

    if (IsCollectionType(type))
    {
        var elementType = GetElementType(type);
        return IsSimpleType(elementType) ? ValueKind.ScalarList : ValueKind.ObjectList;
    }

    if (IsEntity(type))
        return ValueKind.Entity;

    return ValueKind.ComplexType;
}
```

---

## 5. Cómo el Materializer Usa ShapedValue

### Método Principal

```csharp
public List<object> Materialize(ShapedResult shaped, Type targetType)
{
    var results = new List<object>();

    foreach (var item in shaped.Items)
    {
        var instance = MaterializeItem(item, targetType);
        results.Add(instance);
    }

    return results;
}
```

### MaterializeItem

```csharp
private object MaterializeItem(ShapedItem item, Type targetType)
{
    var strategy = GetOrCreateStrategy(targetType);
    var args = new object?[strategy.ConstructorParams.Count];

    foreach (var param in strategy.ConstructorParams)
    {
        if (item.Values.TryGetValue(param.Name, out var shapedValue))
        {
            args[param.Index] = MaterializeValue(shapedValue);
        }
    }

    return strategy.Constructor.Invoke(args);
}
```

### MaterializeValue - Usa ValueKind

```csharp
private object? MaterializeValue(ShapedValue shaped)
{
    if (shaped.Value == null)
        return GetDefaultValue(shaped.TargetType);

    return shaped.Kind switch
    {
        ValueKind.Scalar => _converter.FromFirestore(shaped.Value, shaped.TargetType),

        ValueKind.ComplexType => MaterializeComplexType(shaped.Value, shaped.TargetType),

        ValueKind.Entity => MaterializeEntity(shaped.Value, shaped.TargetType),

        ValueKind.ScalarList => MaterializeScalarList(shaped.Value, shaped.TargetType),

        ValueKind.ObjectList => MaterializeObjectList(shaped.Value, shaped.TargetType),

        _ => throw new InvalidOperationException($"Unknown ValueKind: {shaped.Kind}")
    };
}
```

---

## 6. Flujo Completo

### Ejemplo: `c => new { c.Nombre, Totales = c.Pedidos.Select(p => p.Total) }`

**1. AST (ResolvedProjectionDefinition):**
```
Fields: [
    { FieldPath: "Nombre", ResultName: "Nombre", FieldType: string }
]
Subcollections: [
    { ResultName: "Totales", Fields: [{ FieldPath: "Total", FieldType: decimal }] }
]
```

**2. SnapshotShaper produce:**
```csharp
ShapedItem {
    Values = {
        ["Nombre"] = ShapedValue("Cliente X", typeof(string), ValueKind.Scalar),
        ["Totales"] = ShapedValue([150.50, 299.99], typeof(IEnumerable<decimal>), ValueKind.ScalarList)
    }
}
```

**3. Materializer procesa:**
- Para "Nombre": `ValueKind.Scalar` → `_converter.FromFirestore("Cliente X", typeof(string))`
- Para "Totales": `ValueKind.ScalarList` → `MaterializeScalarList([150.50, 299.99], typeof(IEnumerable<decimal>))`

---

## 7. Cambios en Código

### SnapshotShaper.cs

```csharp
// Nuevo: Construir ShapedValue desde FirestoreProjectedField
private static ShapedValue CreateShapedValue(object? value, FirestoreProjectedField field)
{
    var kind = DetermineValueKind(field.FieldType, value);
    return new ShapedValue(value, field.FieldType, kind);
}

// En ShapeNode, usar ShapedValue en lugar de object?
private static ShapedItem ShapeNode(...)
{
    var values = new Dictionary<string, ShapedValue>();

    foreach (var field in projectedFields)
    {
        var rawValue = GetValue(rawDict, field.FieldPath);
        values[field.ResultName] = CreateShapedValue(rawValue, field);
    }

    return new ShapedItem { Values = values };
}
```

### Materializer.cs

```csharp
// Nuevo firma
public List<object> Materialize(ShapedResult shaped, Type targetType)

// Eliminar: projectedFields, subcollections parámetros
// Eliminar: _subcollectionFieldMappings
// Eliminar: BuildSubcollectionFieldMappings

// Nuevo: MaterializeValue recibe ShapedValue con Kind
private object? MaterializeValue(ShapedValue shaped)
```

### IMaterializer.cs

```csharp
public interface IMaterializer
{
    List<object> Materialize(ShapedResult shaped, Type targetType);
}
```

---

## 8. Resumen

### Responsabilidades

| Componente | Hace | No Hace |
|------------|------|---------|
| **SnapshotShaper** | Lee AST, extrae valores, construye `ShapedValue` con `Kind` | Materializar objetos |
| **Materializer** | Lee `ShapedValue.Kind`, materializa según tipo | Interpretar AST |

### Flujo de Datos

```
AST (FirestoreProjectedField)
    │
    ▼
SnapshotShaper.Shape()
    │
    ▼
ShapedResult {
    Items: [
        ShapedItem {
            Values: {
                "Nombre": ShapedValue("X", string, Scalar),
                "Totales": ShapedValue([150, 299], IEnumerable<decimal>, ScalarList)
            }
        }
    ]
}
    │
    ▼
Materializer.Materialize()
    │
    ▼
List<object> [instancia del tipo anónimo]
```

### Beneficios

1. **SRP:** Cada componente tiene una responsabilidad clara
2. **Información completa:** `ShapedValue` tiene todo lo necesario para materializar
3. **Sin duplicación:** La clasificación se hace una vez en el Shaper
4. **Testeable:** Se puede crear `ShapedResult` manualmente
5. **Extensible:** Agregar nuevo `ValueKind` no rompe código existente
