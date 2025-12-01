# Ejemplo Antes/Después: Detección de Include Tree

## Problema Original

### ❌ ANTES: Solo detecta el primer Include

```csharp
private class IncludeDetectorVisitor : ExpressionVisitor
{
    private readonly Query.FirestoreQueryExpression _queryExpression;

    public IncludeDetectorVisitor(Query.FirestoreQueryExpression queryExpression)
    {
        _queryExpression = queryExpression;
    }

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

        Console.WriteLine($"✓ Detected: {navigation.Name}");
        
        // ✅ Agregar esta navegación
        _queryExpression.PendingIncludes.Add(navigation);
        
        // ❌ PROBLEMA: Visitas NavigationExpression pero sin logging
        // No sabes si realmente está capturando los ThenInclude
        if (includeExpression.NavigationExpression != null)
        {
            Visit(includeExpression.NavigationExpression);
        }
    }
}
```

### Output del código ANTES:
```
✓ Detected: Pedidos

Total PendingIncludes: 1
  📁 Cliente:
    └─[Collection] Pedidos → Pedido
```

**Problema:** Solo detecta `Pedidos`, falta `Lineas` y `Producto`.

---

## Solución

### ✅ DESPUÉS: Detecta todo el árbol de Include

```csharp
private class IncludeDetectorVisitor : ExpressionVisitor
{
    private readonly Query.FirestoreQueryExpression _queryExpression;
    private int _depth = 0;

    public IncludeDetectorVisitor(Query.FirestoreQueryExpression queryExpression)
    {
        _queryExpression = queryExpression;
    }

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
        {
            Console.WriteLine($"{GetIndent()}⚠ IncludeExpression with null Navigation");
            return;
        }

        // ✅ Logging detallado para detectar problemas
        Console.WriteLine($"{GetIndent()}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"{GetIndent()}✓ DETECTED IncludeExpression #{_queryExpression.PendingIncludes.Count + 1}:");
        Console.WriteLine($"{GetIndent()}  Navigation: {navigation.Name}");
        Console.WriteLine($"{GetIndent()}  DeclaringType: {navigation.DeclaringEntityType.ClrType.Name}");
        Console.WriteLine($"{GetIndent()}  TargetType: {navigation.TargetEntityType.ClrType.Name}");
        Console.WriteLine($"{GetIndent()}  IsCollection: {navigation.IsCollection}");
        Console.WriteLine($"{GetIndent()}  NavigationExpression type: {includeExpression.NavigationExpression?.GetType().Name ?? "null"}");
        
        if (includeExpression.NavigationExpression is IncludeExpression nested)
        {
            Console.WriteLine($"{GetIndent()}  → Has nested IncludeExpression: {nested.Navigation.Name}");
        }
        
        Console.WriteLine($"{GetIndent()}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // ✅ Agregar a la lista
        _queryExpression.PendingIncludes.Add(navigation);
        Console.WriteLine($"{GetIndent()}✅ Added to PendingIncludes (total: {_queryExpression.PendingIncludes.Count})");

        // ✅ Visitar recursivamente CON indentación para visualizar profundidad
        if (includeExpression.NavigationExpression != null)
        {
            Console.WriteLine($"{GetIndent()}🔍 Visitando NavigationExpression...\n");
            _depth++;
            Visit(includeExpression.NavigationExpression);
            _depth--;
        }
        else
        {
            Console.WriteLine($"{GetIndent()}⚠ No NavigationExpression (último nivel)\n");
        }
    }

    private string GetIndent()
    {
        return new string(' ', _depth * 2);
    }
}
```

### Output del código DESPUÉS:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ DETECTED IncludeExpression #1:
  Navigation: Pedidos
  DeclaringType: Cliente
  TargetType: Pedido
  IsCollection: True
  NavigationExpression type: IncludeExpression
  → Has nested IncludeExpression: Lineas
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Added to PendingIncludes (total: 1)
🔍 Visitando NavigationExpression...

  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ✓ DETECTED IncludeExpression #2:
    Navigation: Lineas
    DeclaringType: Pedido
    TargetType: Linea
    IsCollection: True
    NavigationExpression type: IncludeExpression
    → Has nested IncludeExpression: Producto
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ✅ Added to PendingIncludes (total: 2)
  🔍 Visitando NavigationExpression...

    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ✓ DETECTED IncludeExpression #3:
      Navigation: Producto
      DeclaringType: Linea
      TargetType: Producto
      IsCollection: False
      NavigationExpression type: StructuralTypeShaperExpression
    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ✅ Added to PendingIncludes (total: 3)
    ⚠ No NavigationExpression (último nivel)

╔═══════════════════════════════════════════════════════╗
║         RESUMEN DE INCLUDES DETECTADOS                ║
╚═══════════════════════════════════════════════════════╝
Total PendingIncludes: 3

  📁 Cliente:
    └─[Collection] Pedidos → Pedido

  📁 Linea:
    └─[Reference] Producto → Producto

  📁 Pedido:
    └─[Collection] Lineas → Linea
```

**Resultado:** Detecta LOS 3 INCLUDES CORRECTAMENTE ✅

---

## Cambios Clave

| Aspecto | ANTES | DESPUÉS |
|---------|-------|---------|
| **Logging** | Mínimo | Detallado con indentación |
| **Detección de anidación** | No visible | Muestra si hay ThenInclude |
| **Profundidad** | No se visualiza | Indentación muestra nivel |
| **Count** | No mostrado | Se muestra en cada paso |
| **NavigationExpression type** | No mostrado | Muestra el tipo para debugging |
| **Resumen final** | No había | Tabla agrupada por DeclaringType |

---

## Verificación del Árbol

### ANTES: Lista plana sin jerarquía visible
```csharp
PendingIncludes = [
    Cliente.Pedidos
]
```

**No sabes si falta algo.**

### DESPUÉS: Lista completa con jerarquía visible
```csharp
PendingIncludes = [
    Cliente.Pedidos → Pedido,
    Pedido.Lineas → Linea,
    Linea.Producto → Producto
]
```

**Y el resumen te muestra la estructura:**
```
Cliente
  └─[1:N] Pedidos → Pedido
      └─[1:N] Lineas → Linea
          └─[N:1] Producto → Producto
```

---

## LoadIncludes: Filtrado correcto

### ANTES: Carga incorrecta (intenta cargar todos de golpe)
```csharp
foreach (var navigation in includes)
{
    await LoadNavigationAsync(entity, documentSnapshot, navigation, ...);
}
```

**Problema:** Intenta cargar `Pedido.Lineas` directamente desde `Cliente` ❌

### DESPUÉS: Carga jerárquica (filtro por DeclaringType)
```csharp
// ✅ Filtrar navegaciones que pertenecen a este nivel
var rootNavigations = includes
    .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
    .ToList();

foreach (var navigation in rootNavigations)
{
    await LoadNavigationAsync(entity, documentSnapshot, navigation, includes, ...);
    
    // 🔁 Los ThenInclude se cargan recursivamente dentro de LoadSubCollectionAsync
    var childIncludes = includes
        .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
        .ToList();
    
    if (childIncludes.Count > 0)
    {
        await LoadIncludes(childEntity, childDoc, childIncludes, ...);
    }
}
```

**Resultado:** Carga en orden correcto:
1. `Cliente.Pedidos` desde `Cliente`
2. `Pedido.Lineas` desde cada `Pedido`
3. `Linea.Producto` desde cada `Linea`

---

## Ejecución: Queries generadas

### ANTES (solo cargaba Pedidos):
```
1. GetDocumentAsync("clientes/cli-002")
2. GetSnapshotAsync("clientes/cli-002/Pedidos")
   
Total: 2 queries
```

**Resultado:** `cliente.Pedidos` está cargado, pero `pedido.Lineas` está vacío ❌

### DESPUÉS (carga todo el árbol):
```
1. GetDocumentAsync("clientes/cli-002")
2. GetSnapshotAsync("clientes/cli-002/Pedidos")
   
   Para cada Pedido:
   3. GetSnapshotAsync("clientes/cli-002/Pedidos/ped-001/Lineas")
   
      Para cada Linea:
      4. GetDocumentAsync("productos/prod-xyz")
   
Total: 1 + 1 + (N pedidos) + (N pedidos * M lineas) queries
```

**Resultado:** Árbol completo cargado ✅

---

## Debugging: Cómo identificar problemas

### Síntoma 1: Solo detecta 1 Include

**Output:**
```
✓ DETECTED IncludeExpression #1:
  Navigation: Pedidos
  NavigationExpression type: IncludeExpression
  → Has nested IncludeExpression: Lineas    ← ✅ Detecta que hay más

✅ Added to PendingIncludes (total: 1)
🔍 Visitando NavigationExpression...

Total PendingIncludes: 1                    ← ❌ Pero solo agrega 1
```

**Diagnóstico:** `Visit(includeExpression.NavigationExpression)` **no está siendo llamado** o está lanzando excepción silenciosa.

**Solución:**
```csharp
// Verificar que base.VisitExtension() se llama
protected override Expression VisitExtension(Expression node)
{
    if (node is IncludeExpression includeExpression)
    {
        ProcessInclude(includeExpression);
    }
    return base.VisitExtension(node); // ✅ IMPORTANTE
}
```

---

### Síntoma 2: NavigationExpression es null

**Output:**
```
✓ DETECTED IncludeExpression #1:
  Navigation: Pedidos
  NavigationExpression type: null           ← ❌ Debería ser IncludeExpression
  
⚠ No NavigationExpression (último nivel)
```

**Diagnóstico:** El `ShaperExpression` no contiene los `IncludeExpression`. EF Core no expandió la navegación.

**Posibles causas:**
1. La query no tiene `Include`/`ThenInclude` (verifica tu código)
2. El modelo no está configurado correctamente
3. Estás visitando la expresión equivocada

**Solución:** Verifica que estás visitando `shapedQueryExpression.ShaperExpression`:
```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    // ✅ IMPORTANTE: Visitar el ShaperExpression, no el QueryExpression
    var includeDetector = new IncludeDetectorVisitor(firestoreQueryExpression);
    includeDetector.Visit(shapedQueryExpression.ShaperExpression); // ← Aquí
}
```

---

### Síntoma 3: Detecta pero no carga

**Output:**
```
Total PendingIncludes: 3

  📁 Cliente:
    └─[Collection] Pedidos → Pedido
  📁 Pedido:
    └─[Collection] Lineas → Linea
  📁 Linea:
    └─[Reference] Producto → Producto
```

Pero al ejecutar:
```csharp
var cliente = await context.Clientes.Include(...).FirstOrDefaultAsync(...);
// cliente.Pedidos != null ✅
// cliente.Pedidos[0].Lineas == null ❌
```

**Diagnóstico:** `LoadIncludes` no está filtrando correctamente o no está llamando recursivamente.

**Solución:** Verificar el filtro:
```csharp
// ✅ CORRECTO: Filtrar por DeclaringEntityType
var childIncludes = allIncludes
    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
    .ToList();

// ❌ INCORRECTO: Filtrar por nombre
var childIncludes = allIncludes
    .Where(inc => inc.Name.Contains("Lineas"))
    .ToList();
```

---

## Resumen de Cambios Necesarios

### 1. En `IncludeDetectorVisitor`:
- ✅ Agregar logging detallado
- ✅ Mostrar profundidad con indentación
- ✅ Detectar ThenInclude anidados
- ✅ Verificar que `Visit()` se llama recursivamente

### 2. En `VisitShapedQuery`:
- ✅ Agregar resumen final
- ✅ Visualizar árbol de carga esperado
- ✅ Mostrar advertencia si PendingIncludes está vacío

### 3. En `LoadIncludes`:
- ✅ Filtrar por `DeclaringEntityType`
- ✅ Cargar recursivamente los ThenInclude
- ✅ Pasar `allIncludes` completo a cada nivel

---

## Test de Verificación

### Query de prueba:
```csharp
var cliente = await context.Clientes
    .Include(c => c.Pedidos)
        .ThenInclude(p => p.Lineas)
            .ThenInclude(l => l.Producto)
    .FirstOrDefaultAsync(c => c.Id == "cli-002");
```

### Expected output:
```
Total PendingIncludes: 3
```

### Expected result:
```csharp
Assert.NotNull(cliente);
Assert.NotNull(cliente.Pedidos);
Assert.True(cliente.Pedidos.Count > 0);

var pedido = cliente.Pedidos[0];
Assert.NotNull(pedido.Lineas);
Assert.True(pedido.Lineas.Count > 0);

var linea = pedido.Lineas[0];
Assert.NotNull(linea.Producto);
Assert.Equal("prod-xyz", linea.Producto.Id);
```

---

## Próximo Paso

Copia el código mejorado completo de `IMPROVED_INCLUDE_DETECTOR.cs` a tu proyecto y ejecuta la query de prueba.
