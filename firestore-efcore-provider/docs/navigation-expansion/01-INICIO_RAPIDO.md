# TL;DR: INavigationExpansionExtensibilityHelper en EFCore.InMemory

## Tu pregunta:
> ¿Cómo usa EFCore.InMemory el contrato `INavigationExpansionExtensibilityHelper`?

## Respuesta corta:
**NO LO USA.**

---

## ¿Por qué no?

Porque **la expansión de navegación (`Include`/`ThenInclude`) la hace el núcleo de EF Core ANTES** de que cualquier proveedor (InMemory, SQL Server, Firestore) vea la query.

---

## ¿Qué recibe tu proveedor entonces?

Un `ShapedQueryExpression` con:
- `QueryExpression`: Tu query traducida (FirestoreQueryExpression, InMemoryQueryExpression, etc.)
- `ShaperExpression`: **Árbol completo de `IncludeExpression`** ya construido por EF Core

---

## ¿Qué hace InMemory?

Simplemente **compila el shaper** y lo aplica a los datos en memoria.

No necesita hacer nada especial porque **todos los datos ya están cargados**.

---

## ¿Qué debe hacer tu proveedor de Firestore?

### 1. En `ShapedQueryCompilingExpressionVisitor`:

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ✅ Visitar el ShaperExpression para extraer Include
    var includeDetector = new IncludeDetectorVisitor(firestoreQueryExpression);
    includeDetector.Visit(shapedQueryExpression.ShaperExpression);
    
    // Ahora tienes todas las navegaciones en PendingIncludes
}
```

### 2. En `IncludeDetectorVisitor`:

```csharp
protected override Expression VisitExtension(Expression node)
{
    if (node is IncludeExpression includeExpression)
    {
        // ✅ Agregar navegación
        _queryExpression.PendingIncludes.Add(includeExpression.Navigation);
        
        // ✅✅✅ CLAVE: Visitar recursivamente para capturar ThenInclude
        if (includeExpression.NavigationExpression != null)
        {
            Visit(includeExpression.NavigationExpression);
        }
    }
    return base.VisitExtension(node);
}
```

### 3. En `LoadIncludes`:

```csharp
// ✅ Cargar recursivamente
var childIncludes = allIncludes
    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
    .ToList();

if (childIncludes.Count > 0)
{
    await LoadIncludes(childEntity, childDoc, childIncludes, ...);
}
```

---

## ¿Qué estaba mal en tu código original?

Probablemente solo detectabas el primer `Include` porque:
- ❌ No estabas visitando `NavigationExpression` recursivamente
- ❌ O no estabas agregando todas las navegaciones a `PendingIncludes`

---

## Solución:

**Aplica el código de `IMPROVED_INCLUDE_DETECTOR.cs`** y ejecuta tu query.

Deberías ver:
```
Total PendingIncludes: 3

  📁 Cliente:
    └─[Collection] Pedidos → Pedido

  📁 Linea:
    └─[Reference] Producto → Producto

  📁 Pedido:
    └─[Collection] Lineas → Linea
```

En lugar de:
```
Total PendingIncludes: 1  ← ❌ Problema

  📁 Cliente:
    └─[Collection] Pedidos → Pedido
```

---

## Documentación completa:

Lee `INVESTIGACION_NAVIGATION_EXPANSION.md` para toda la investigación.

---

## Conclusión:

No necesitas `INavigationExpansionExtensibilityHelper`.  
Solo necesitas **visitar recursivamente** el `IncludeExpression` que EF Core ya construyó.
