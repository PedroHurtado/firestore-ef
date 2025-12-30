# Análisis de ProjectionExtractionVisitor

## Resumen

Este documento analiza los gaps identificados en `ProjectionExtractionVisitor`, el visitor encargado de extraer información de proyección de expresiones LINQ para el provider de EF Core para Firestore.

## Estado actual

El visitor maneja correctamente:

- **SingleField**: `e => e.Name`
- **AnonymousType**: `e => new { e.Id, e.Name }`
- **DtoClass**: `e => new Dto { Id = e.Id }`
- **Record**: `e => new Record(e.Id, e.Name)`
- **ComplexType**: `e => e.Direccion`
- **Nested field**: `e => e.Direccion.Ciudad`
- **Subcollections básicas**: `e => new { e.Nombre, e.Pedidos }`
- **Subcollection con operaciones**: `Where`, `OrderBy/ThenBy`, `Take`, `Select`
- **Agregaciones en subcollections**: `Count`, `Sum`, `Average`, `Min`, `Max`

---

## Gaps identificados

### 1. Expresiones calculadas en proyecciones

No existe manejo para `BinaryExpression`, lo que impide proyecciones con operaciones aritméticas o concatenaciones.

**Ejemplos no soportados:**

```csharp
// Concatenación de strings
e => new { FullName = e.FirstName + " " + e.LastName }

// Operaciones aritméticas
e => new { Total = e.Price * e.Quantity }
e => new { Discount = e.Price * 0.1m }

// Comparaciones que generan bool
e => new { IsExpensive = e.Price > 100 }
e => new { IsAdult = e.Age >= 18 }
```

**Impacto:** Estas proyecciones fallarán silenciosamente, el campo no se incluirá en el resultado.

**Solución propuesta:** Añadir case para `BinaryExpression` en `ProcessProjectionArgument`. Decidir si:
- Evaluar en cliente (post-fetch)
- Lanzar `NotSupportedException` con mensaje claro
- Intentar traducir a expresiones Firestore (limitado)

---

### 2. Constantes y capturas de variables (closures)

No hay manejo para valores constantes ni variables capturadas del scope externo.

**Ejemplos no soportados:**

```csharp
// Constantes literales
e => new { e.Name, Version = 1 }
e => new { e.Name, Active = true }

// Expresiones de fecha/hora
e => new { e.Name, Today = DateTime.Now }
e => new { e.Name, Timestamp = DateTimeOffset.UtcNow }

// Closures - variables capturadas
var prefix = "Sr.";
e => new { Title = prefix + e.Name }

var threshold = GetThreshold();
e => new { e.Name, Threshold = threshold }
```

**Impacto:** Los campos con constantes o closures no aparecerán en la proyección resultante.

**Solución propuesta:** 
- `ConstantExpression`: Evaluar directamente y almacenar como campo computado
- Closures: Compilar y evaluar la expresión para obtener el valor

---

### 3. Llamadas a métodos sobre propiedades

No se procesan `MethodCallExpression` que operan sobre propiedades de la entidad (solo se detectan operaciones LINQ sobre navegaciones).

**Ejemplos no soportados:**

```csharp
// Métodos de string
e => new { Upper = e.Name.ToUpper() }
e => new { Lower = e.Name.ToLower() }
e => new { Trimmed = e.Name.Trim() }
e => new { Sub = e.Name.Substring(0, 5) }

// Propiedades de tipos
e => new { Length = e.Name.Length }
e => new { Year = e.BirthDate.Year }
e => new { Month = e.BirthDate.Month }
e => new { Day = e.CreatedAt.DayOfWeek }

// Métodos de conversión
e => new { AsString = e.Id.ToString() }
```

**Impacto:** Se intentará interpretar como operación sobre subcollection y fallará o se ignorará.

**Solución propuesta:** En `ProcessProjectionArgument`, antes de verificar si es navegación, detectar si es un método sobre propiedad escalar y marcarlo para evaluación en cliente.

---

### 4. Expresiones condicionales

No hay soporte para operadores ternarios, null-coalescing ni null-propagation.

**Ejemplos no soportados:**

```csharp
// Operador ternario
e => new { Status = e.IsActive ? "Active" : "Inactive" }
e => new { Display = e.Name != null ? e.Name : "N/A" }

// Null-coalescing
e => new { Name = e.Name ?? "Unknown" }
e => new { Value = e.OptionalValue ?? 0 }

// Null-propagation (genera ConditionalExpression internamente)
e => e.Address?.City
e => new { City = e.Address?.City ?? "Unknown" }
```

**Impacto:** Estas expresiones no generan campos en la proyección.

**Solución propuesta:** Añadir cases para:
- `ConditionalExpression` (ternario y null-propagation)
- `BinaryExpression` con `NodeType == Coalesce`

---

### 5. Operaciones de subcollection faltantes

El visitor maneja correctamente:
- `Where`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`
- `Take`, `Select`
- **Agregaciones:** `Count`, `Sum`, `Average`, `Min`, `Max`

Operaciones faltantes que Firestore sí soporta:

#### 5.1 Skip (paginación con offset)

```csharp
// Solo hay Take, falta Skip para paginación completa
e => new { Pedidos = e.Pedidos.Skip(10).Take(5) }
```

> **Nota:** Firestore soporta `offset()` aunque no es recomendado para datasets grandes. Evaluar si implementar o forzar paginación por cursor.

#### 5.2 FirstOrDefault / SingleOrDefault

```csharp
// Muy común para obtener un solo elemento relacionado
e => new { UltimoPedido = e.Pedidos.OrderByDescending(p => p.Fecha).FirstOrDefault() }
e => new { Principal = e.Direcciones.SingleOrDefault(d => d.EsPrincipal) }
```

> **Implementación:** Se traduce a `.Take(1)` o `.Limit(1)` en Firestore.

#### 5.3 Any / All (predicados de existencia)

```csharp
// Verificar existencia
e => new { TienePedidos = e.Pedidos.Any() }
e => new { TienePendientes = e.Pedidos.Any(p => !p.Pagado) }

// Verificar condición universal
e => new { TodosPagados = e.Pedidos.All(p => p.Pagado) }
```

> **Implementación:** 
> - `Any()` → `Count` aggregation > 0, o `Limit(1)` y verificar si hay resultado
> - `Any(predicate)` → `Where(predicate).Limit(1)` 
> - `All(predicate)` → `Where(!predicate).Limit(1)` y verificar que NO hay resultado

#### 5.4 Operaciones NO soportadas por Firestore

Las siguientes operaciones **no deben implementarse** ya que Firestore no las soporta nativamente:

- `SelectMany` — No hay flatten de colecciones
- `Distinct` — No hay deduplicación server-side
- `GroupBy` — No hay agrupación server-side
- `Join` / `GroupJoin` — No hay joins entre colecciones

Si el usuario intenta usar estas operaciones, el visitor debería lanzar `NotSupportedException` con un mensaje claro indicando la limitación de Firestore.

**Solución propuesta:** Añadir cases en `ProcessSubcollectionMethodCall` para `Skip`, `FirstOrDefault`, `SingleOrDefault`, `Any`, `All`. Para operaciones no soportadas, lanzar excepción explicativa.

---

### 6. Problema arquitectónico: fallo silencioso

El método `ProcessProjectionArgument` no tiene un case `default`, lo que causa que expresiones no reconocidas se ignoren sin ningún error.

**Código actual:**

```csharp
private void ProcessProjectionArgument(...)
{
    switch (argument)
    {
        case MemberExpression memberExpr when IsCollectionNavigation(memberExpr):
            // ...
            break;

        case MemberExpression memberExpr:
            // ...
            break;

        case MethodCallExpression methodCallExpr:
            // ...
            break;

        case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Convert:
            // ...
            break;
        
        // ⚠️ NO HAY DEFAULT - todo lo demás se ignora
    }
}
```

**Impacto:** 
- Bugs difíciles de diagnosticar
- Proyecciones incompletas sin advertencia
- El desarrollador no sabe que su proyección no está soportada

**Solución propuesta:**

```csharp
default:
    throw new NotSupportedException(
        $"Projection expression type '{argument.NodeType}' ({argument.GetType().Name}) " +
        $"is not supported in Firestore projections. Expression: {argument}");
```

Opcionalmente, implementar un modo de desarrollo/debug que loguee advertencias en lugar de fallar.

---

### 7. Distinción entre navigation y owned type

El método `IsCollectionNavigation` solo verifica si el tipo es una colección genérica, pero no distingue entre:

**Owned type embebido (array en el documento Firestore):**
```csharp
// Se almacena como array dentro del documento padre
public class Cliente
{
    public List<Direccion> Direcciones { get; set; }  // Owned
}
```

**Subcollection real (colección separada en Firestore):**
```csharp
// Se almacena como subcollection separada
public class Cliente
{
    public List<Pedido> Pedidos { get; set; }  // Navigation
}
```

**Código actual:**

```csharp
private static bool IsCollectionNavigation(MemberExpression memberExpr)
{
    var memberType = memberExpr.Type;

    if (memberType.IsGenericType)
    {
        var genericDef = memberType.GetGenericTypeDefinition();
        return genericDef == typeof(List<>)
            || genericDef == typeof(IList<>)
            || genericDef == typeof(ICollection<>)
            || genericDef == typeof(IEnumerable<>);
    }

    return false;
}
```

**Impacto:** 
- Owned types se tratan como subcollections, generando queries incorrectas
- No se pueden proyectar arrays embebidos correctamente

**Solución propuesta:** Inyectar `IModel` o el metadata del contexto para consultar si la propiedad es:
- Navigation property (subcollection)
- Owned entity (embebido)
- Complex type (embebido)

```csharp
private bool IsCollectionNavigation(MemberExpression memberExpr, IModel model)
{
    var entityType = model.FindEntityType(memberExpr.Expression.Type);
    var navigation = entityType?.FindNavigation(memberExpr.Member.Name);
    
    if (navigation != null)
    {
        return navigation.IsCollection && !navigation.TargetEntityType.IsOwned();
    }
    
    return false;
}
```

---

## Resumen de prioridades

| Prioridad | Gap | Impacto | Esfuerzo |
|-----------|-----|---------|----------|
| **Alta** | Fallo silencioso (default case) | Bugs ocultos | Bajo |
| **Alta** | Distinción navigation vs owned | Queries incorrectas | Medio |
| **Alta** | Rechazar operaciones no soportadas | Errores claros vs silenciosos | Bajo |
| **Media** | Constantes y closures | Proyecciones incompletas | Bajo |
| **Media** | Any/All en subcollections | Feature común no disponible | Medio |
| **Media** | FirstOrDefault/SingleOrDefault | Feature común no disponible | Bajo |
| **Baja** | Skip (paginación offset) | Paginación limitada | Bajo |
| **Baja** | Expresiones calculadas | Evaluación en cliente | Medio |
| **Baja** | Métodos sobre propiedades | Evaluación en cliente | Medio |
| **Baja** | Condicionales | Evaluación en cliente | Medio |

---

## Siguientes pasos recomendados

1. **Inmediato:** Añadir `default` case con `NotSupportedException` para detectar gaps
2. **Inmediato:** Añadir cases explícitos para `SelectMany`, `Distinct`, `GroupBy`, `Join` que lancen `NotSupportedException` indicando limitación de Firestore
3. **Corto plazo:** Inyectar modelo de EF para distinguir navigations de owned types
4. **Corto plazo:** Implementar soporte para constantes y closures
5. **Corto plazo:** Añadir `Any`, `All`, `FirstOrDefault`, `SingleOrDefault` a subcollections
6. **Medio plazo:** Evaluar qué expresiones calculadas pueden evaluarse en cliente post-fetch
