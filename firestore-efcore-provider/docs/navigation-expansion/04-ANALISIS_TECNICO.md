# Análisis: Navigation Expansion en EF Core y EFCore.InMemory

## Resumen Ejecutivo

Después de analizar el código fuente de **EF Core** y **EFCore.InMemory**, he descubierto algo **fundamental**:

> ⚠️ **DESCUBRIMIENTO CLAVE**: EFCore.InMemory **NO implementa ni usa** `INavigationExpansionExtensibilityHelper`.

### ¿Por qué?

Porque **la expansión de navegación (`Include`/`ThenInclude`) es manejada COMPLETAMENTE por el núcleo de EF Core**, no por los proveedores individuales.

---

## ¿Cómo funciona el proceso de Include/ThenInclude?

### 1. **Fase de Expansión de Navegación** (EF Core Núcleo)

Cuando escribes una query como:

```csharp
var clienteConPedidos = await context.Clientes
    .Include(c => c.Pedidos)
        .ThenInclude(p => p.Lineas)
            .ThenInclude(l => l.Producto)
    .FirstOrDefaultAsync(c => c.Id == "cli-002");
```

**EF Core realiza estos pasos ANTES de que tu proveedor vea la query:**

1. **`NavigationExpandingExpressionVisitor`** procesa el árbol de expresiones
2. Convierte cada `Include` y `ThenInclude` en un **`IncludeExpression`** interno
3. Crea un árbol jerárquico de navegaciones llamado **`NavigationTree`**
4. Construye el **`ShaperExpression`** que contiene toda esta información

### 2. **Lo que recibe tu proveedor**

Cuando la query llega a tu `QueryableMethodTranslatingExpressionVisitor`, **ya está procesada**. Recibes un `ShapedQueryExpression` que contiene:

```
ShapedQueryExpression {
    QueryExpression: FirestoreQueryExpression,
    ShaperExpression: IncludeExpression {
        Navigation: "Pedidos",
        NavigationExpression: IncludeExpression {
            Navigation: "Lineas",
            NavigationExpression: IncludeExpression {
                Navigation: "Producto",
                NavigationExpression: null
            }
        }
    }
}
```

**Esta estructura anidada ES el árbol de ThenInclude.**

---

## ¿Qué hace EFCore.InMemory?

### Estructura del proveedor InMemory

```
InMemoryQueryableMethodTranslatingExpressionVisitor
  └─ Traduce LINQ a expresiones InMemory
  └─ NO procesa Include directamente
        ⬇
ShapedQueryExpression (con ShaperExpression que contiene IncludeExpression)
        ⬇
InMemoryShapedQueryCompilingExpressionVisitor
  └─ VisitShapedQuery()
  └─ Compila el ShaperExpression
  └─ Durante la ejecución, el shaper recibe cada entidad y APLICA los includes
```

### Código relevante de InMemory

En `InMemoryShapedQueryCompilingExpressionVisitor.cs`:

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    var inMemoryQueryExpression = (InMemoryQueryExpression)shapedQueryExpression.QueryExpression;
    inMemoryQueryExpression.ApplyProjection();

    // CLAVE: El ShaperExpression ya contiene los IncludeExpression
    // Solo necesita procesarlo
    var shaperExpression = new ShaperExpressionProcessingExpressionVisitor(
            this, inMemoryQueryExpression, 
            QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll)
        .ProcessShaper(shapedQueryExpression.ShaperExpression);
    
    var innerEnumerable = Visit(inMemoryQueryExpression.ServerQueryExpression);

    return New(
        typeof(QueryingEnumerable<>).MakeGenericType(shaperExpression.ReturnType).GetConstructors()[0],
        QueryCompilationContext.QueryContextParameter,
        innerEnumerable,
        Constant(shaperExpression.Compile()), // Shaper compilado
        ...
    );
}
```

**InMemory NO hace nada especial con Include**. Solo compila el `ShaperExpression` que EF Core ya preparó.

---

## ¿Qué está mal en tu implementación actual?

### Tu código actual (líneas 661-712)

```csharp
private class IncludeDetectorVisitor : ExpressionVisitor
{
    protected override Expression VisitExtension(Expression node)
    {
        if (node is IncludeExpression includeExpression)
        {
            ProcessInclude(includeExpression);
        }
        return base.VisitExtension(node);
    }

    private void ProcessInclude(IncludeExpression includeExpression)
    {
        _queryExpression.PendingIncludes.Add(includeExpression.Navigation);
        
        // 🚨 PROBLEMA: Solo visitas NavigationExpression
        // Pero no el EntityExpression
        if (includeExpression.NavigationExpression != null)
        {
            Visit(includeExpression.NavigationExpression);
        }
    }
}
```

### El problema

La estructura real de un `IncludeExpression` es:

```csharp
public class IncludeExpression : Expression
{
    public Expression EntityExpression { get; }       // La entidad base
    public Expression NavigationExpression { get; }   // La navegación A CARGAR
    public INavigation Navigation { get; }            // Metadata de la navegación
}
```

**Armado real para tu query:**

```
IncludeExpression(Pedidos) {
    EntityExpression: StructuralTypeShaperExpression (Cliente),
    NavigationExpression: IncludeExpression(Lineas) {
        EntityExpression: StructuralTypeShaperExpression (Pedido),
        NavigationExpression: IncludeExpression(Producto) {
            EntityExpression: StructuralTypeShaperExpression (Linea),
            NavigationExpression: StructuralTypeShaperExpression (Producto)
        }
    },
    Navigation: "Pedidos"
}
```

**El árbol COMPLETO de ThenInclude está en `NavigationExpression`, no en `EntityExpression`**.

Pero necesitas visitar AMBOS **de forma recursiva** hasta llegar al final.

---

## La solución correcta

### Implementación mejorada

```csharp
private class IncludeDetectorVisitor : ExpressionVisitor
{
    private readonly FirestoreQueryExpression _queryExpression;
    private readonly Stack<IReadOnlyNavigation> _navigationPath = new();

    protected override Expression VisitExtension(Expression node)
    {
        if (node is IncludeExpression includeExpression)
        {
            ProcessInclude(includeExpression);
        }
        return base.VisitExtension(node);
    }

    private void ProcessInclude(IncludeExpression includeExpression)
    {
        if (includeExpression.Navigation is not IReadOnlyNavigation navigation)
            return;

        Console.WriteLine($"✓ Detected: {navigation.DeclaringEntityType.ClrType.Name}.{navigation.Name}");
        
        // Agregar esta navegación
        _queryExpression.PendingIncludes.Add(navigation);
        
        // 🔑 CLAVE: Marcar en el stack que estamos dentro de esta navegación
        _navigationPath.Push(navigation);
        
        // 🔑 CLAVE: Visitar RECURSIVAMENTE NavigationExpression
        // Esto capturará los ThenInclude anidados
        if (includeExpression.NavigationExpression != null)
        {
            Visit(includeExpression.NavigationExpression);
        }
        
        _navigationPath.Pop();
    }
}
```

### Estructura de datos correcta

En lugar de una lista plana:

```csharp
public List<IReadOnlyNavigation> PendingIncludes { get; set; } = new();
```

Deberías usar una **estructura jerárquica**:

```csharp
public class NavigationNode
{
    public IReadOnlyNavigation Navigation { get; set; }
    public List<NavigationNode> Children { get; set; } = new();
}

public List<NavigationNode> NavigationTree { get; set; } = new();
```

De esta forma puedes representar:

```
Cliente
  └─ Pedidos (subcollection)
       └─ Lineas (subcollection)
            └─ Producto (referencia)
```

---

## Comparación con EFCore.InMemory

| Aspecto | EFCore.InMemory | Tu Firestore Provider |
|---------|----------------|----------------------|
| **Preparación de Include** | No necesita, EF Core lo hace | Necesitas detectar el árbol |
| **Ejecución de Include** | Los datos ya están en memoria, solo aplica el shaper | Debes hacer llamadas a Firestore para subcollections |
| **Timing** | Todo en tiempo de shaping | Necesitas cargar durante deserialización |
| **Complejidad** | Baja, datos disponibles | Alta, necesitas queries adicionales |

### Por qué InMemory no necesita `INavigationExpansionExtensibilityHelper`

```csharp
// InMemory: Los datos YA están cargados
var blogs = inMemoryStore.GetTable<Blog>();
// El shaper simplemente recorre las propiedades de navegación
// y las asigna desde memoria

// Firestore: Los datos NO están cargados
var cliente = await GetDocumentAsync("clientes/cli-002");
// Necesitas EJECUTAR QUERIES ADICIONALES para subcollections:
await cliente.Reference.Collection("Pedidos").GetSnapshotAsync();
```

---

## Recomendaciones para tu implementación

### 1. Mejora el `IncludeDetectorVisitor`

Asegúrate de visitar **todo el árbol de `NavigationExpression`**:

```csharp
private void ProcessInclude(IncludeExpression includeExpression)
{
    if (includeExpression.Navigation is not IReadOnlyNavigation navigation)
        return;

    _queryExpression.PendingIncludes.Add(navigation);
    
    // ✅ Visitar recursivamente TODAS las navegaciones anidadas
    if (includeExpression.NavigationExpression != null)
    {
        Visit(includeExpression.NavigationExpression);
    }
    
    // ✅ OPCIONAL: También visitar EntityExpression si necesitas más contexto
    // (normalmente no es necesario para ThenInclude)
    Visit(includeExpression.EntityExpression);
}
```

### 2. Construye un árbol jerárquico

Cambia `PendingIncludes` de una lista plana a un árbol:

```csharp
public class IncludeNode
{
    public IReadOnlyNavigation Navigation { get; set; }
    public IncludeNode? Parent { get; set; }
    public List<IncludeNode> Children { get; set; } = new();
    
    // Para debugging
    public string GetFullPath()
    {
        if (Parent == null)
            return Navigation.Name;
        return $"{Parent.GetFullPath()}.{Navigation.Name}";
    }
}
```

### 3. Carga recursiva en `LoadIncludes`

Tu método actual (líneas 776-798) **ya tiene la estructura correcta**:

```csharp
private static async Task LoadIncludes<T>(
    T entity,
    DocumentSnapshot documentSnapshot,
    List<IReadOnlyNavigation> includes,
    ...)
{
    // ✅ Filtrar navegaciones de nivel raíz
    var rootNavigations = includes
        .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
        .ToList();

    foreach (var navigation in rootNavigations)
    {
        await LoadNavigationAsync(entity, documentSnapshot, navigation, includes, ...);
    }
}
```

Pero mejora la recursión en `LoadSubCollectionAsync` (líneas 827-895):

```csharp
// 🔑 Buscar ThenInclude para esta navegación
var childIncludes = allIncludes
    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
    .ToList();

foreach (var doc in snapshot.Documents)
{
    var childEntity = deserializeMethod.Invoke(deserializer, new object[] { doc });
    
    // 🔁 CARGA RECURSIVA de ThenInclude
    if (childIncludes.Count > 0)
    {
        await LoadIncludes(childEntity, doc, childIncludes, ...);
    }
    
    list.Add(childEntity);
}
```

### 4. Debugging: Imprime el árbol completo

```csharp
private void PrintNavigationTree(List<IReadOnlyNavigation> navigations)
{
    Console.WriteLine("\n📊 Navigation Tree:");
    
    var grouped = navigations
        .GroupBy(n => n.DeclaringEntityType)
        .OrderBy(g => g.Key.ClrType.Name);
    
    foreach (var group in grouped)
    {
        Console.WriteLine($"  {group.Key.ClrType.Name}:");
        foreach (var nav in group)
        {
            Console.WriteLine($"    └─ {nav.Name} -> {nav.TargetEntityType.ClrType.Name}");
        }
    }
    Console.WriteLine();
}
```

---

## Conclusión

### ❌ Lo que NO necesitas hacer:

- ❌ Implementar `INavigationExpansionExtensibilityHelper` (es solo para casos muy avanzados)
- ❌ Procesar `Include` en `VisitMethodCall` (EF Core ya lo convirtió a `IncludeExpression`)
- ❌ Crear tu propio árbol de navegación desde cero

### ✅ Lo que SÍ necesitas hacer:

- ✅ **Visitar recursivamente** el `IncludeExpression.NavigationExpression`
- ✅ **Capturar TODAS** las navegaciones en el árbol (no solo la primera)
- ✅ **Cargar recursivamente** las subcollections durante la deserialización
- ✅ **Mantener la jerarquía** de navegaciones para saber qué cargar dentro de qué

---

## Siguiente paso

Tu implementación actual **casi funciona**. El problema es que:

1. **`IncludeDetectorVisitor`** SÍ está visitando `NavigationExpression` correctamente (línea 708)
2. Pero quizás **no se están agregando todas** las navegaciones a `PendingIncludes`

**Prueba de depuración:**

Agrega más logging en `ProcessInclude`:

```csharp
private void ProcessInclude(IncludeExpression includeExpression)
{
    if (includeExpression.Navigation is not IReadOnlyNavigation navigation)
        return;

    Console.WriteLine($"✓ Processing: {navigation.Name}");
    Console.WriteLine($"  DeclaringType: {navigation.DeclaringEntityType.ClrType.Name}");
    Console.WriteLine($"  Has NavigationExpression: {includeExpression.NavigationExpression != null}");
    
    _queryExpression.PendingIncludes.Add(navigation);
    
    if (includeExpression.NavigationExpression != null)
    {
        Console.WriteLine($"  → Diving into nested navigation...");
        Visit(includeExpression.NavigationExpression);
    }
}
```

Esto te dirá exactamente qué navegaciones está detectando y cuáles no.

---

## Referencias

- [EF Core Source: NavigationExpandingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore/Query/Internal/NavigationExpandingExpressionVisitor.cs)
- [EF Core Source: InMemoryShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.InMemory/Query/Internal/InMemoryShapedQueryCompilingExpressionVisitor.cs)
- [INavigationExpansionExtensibilityHelper.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore/Query/INavigationExpansionExtensibilityHelper.cs)
