# Debugging Guide: Include Tree Detection

## Problema actual

Estás detectando solo el **primer Include** pero no el árbol completo de **ThenInclude**.

---

## Test rápido para debugging

### 1. Mejora el logging en `IncludeDetectorVisitor`

Reemplaza tu método `ProcessInclude` con esta versión mejorada:

```csharp
private void ProcessInclude(IncludeExpression includeExpression)
{
    if (includeExpression.Navigation is not IReadOnlyNavigation navigation)
    {
        Console.WriteLine("⚠ IncludeExpression with null Navigation, skipping");
        return;
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine($"✓ DETECTED IncludeExpression:");
    Console.WriteLine($"  Navigation: {navigation.Name}");
    Console.WriteLine($"  DeclaringType: {navigation.DeclaringEntityType.ClrType.Name}");
    Console.WriteLine($"  TargetType: {navigation.TargetEntityType.ClrType.Name}");
    Console.WriteLine($"  IsCollection: {navigation.IsCollection}");
    Console.WriteLine($"  IsSubCollection: {navigation.IsSubCollection()}");
    
    // Información sobre el EntityExpression
    Console.WriteLine($"  EntityExpression type: {includeExpression.EntityExpression?.GetType().Name ?? "null"}");
    
    // Información sobre el NavigationExpression
    Console.WriteLine($"  NavigationExpression type: {includeExpression.NavigationExpression?.GetType().Name ?? "null"}");
    
    if (includeExpression.NavigationExpression is IncludeExpression nested)
    {
        Console.WriteLine($"  → Has nested IncludeExpression (ThenInclude): {nested.Navigation.Name}");
    }
    
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    // ✅ Agregar a la lista
    _queryExpression.PendingIncludes.Add(navigation);
    Console.WriteLine($"✅ Added to PendingIncludes (total: {_queryExpression.PendingIncludes.Count})");
    
    // 🔑 CLAVE: Visitar NavigationExpression recursivamente
    if (includeExpression.NavigationExpression != null)
    {
        Console.WriteLine($"🔍 Visitando NavigationExpression para buscar ThenInclude...\n");
        Visit(includeExpression.NavigationExpression);
    }
    else
    {
        Console.WriteLine($"⚠ No NavigationExpression, es el último nivel\n");
    }
}
```

### 2. Agrega un resumen al final

En `VisitShapedQuery`, después del `includeDetector.Visit()`:

```csharp
// Log de debug: ver qué navegaciones se capturaron
Console.WriteLine($"\n╔═══════════════════════════════════════════╗");
Console.WriteLine($"║  RESUMEN DE INCLUDES DETECTADOS           ║");
Console.WriteLine($"╚═══════════════════════════════════════════╝");
Console.WriteLine($"Total PendingIncludes: {firestoreQueryExpression.PendingIncludes.Count}");

if (firestoreQueryExpression.PendingIncludes.Count == 0)
{
    Console.WriteLine("⚠ ⚠ ⚠  NO SE DETECTÓ NINGÚN INCLUDE  ⚠ ⚠ ⚠");
}
else
{
    // Agrupar por DeclaringType para visualizar el árbol
    var grouped = firestoreQueryExpression.PendingIncludes
        .GroupBy(n => n.DeclaringEntityType.ClrType.Name)
        .OrderBy(g => g.Key);
    
    foreach (var group in grouped)
    {
        Console.WriteLine($"\n  📁 {group.Key}:");
        foreach (var nav in group)
        {
            var arrow = nav.IsCollection ? "└─[Collection]" : "└─[Reference]";
            Console.WriteLine($"    {arrow} {nav.Name} → {nav.TargetEntityType.ClrType.Name}");
        }
    }
}
Console.WriteLine($"\n═════════════════════════════════════════════\n");
```

---

## Output esperado para tu query

Para esta query:

```csharp
var clienteConPedidos = await context.Clientes
    .Include(c => c.Pedidos)
        .ThenInclude(p => p.Lineas)
            .ThenInclude(l => l.Producto)
    .FirstOrDefaultAsync(c => c.Id == "cli-002");
```

**Deberías ver:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ DETECTED IncludeExpression:
  Navigation: Pedidos
  DeclaringType: Cliente
  TargetType: Pedido
  IsCollection: True
  IsSubCollection: True
  EntityExpression type: StructuralTypeShaperExpression
  NavigationExpression type: IncludeExpression
  → Has nested IncludeExpression (ThenInclude): Lineas
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Added to PendingIncludes (total: 1)
🔍 Visitando NavigationExpression para buscar ThenInclude...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ DETECTED IncludeExpression:
  Navigation: Lineas
  DeclaringType: Pedido
  TargetType: Linea
  IsCollection: True
  IsSubCollection: True
  EntityExpression type: StructuralTypeShaperExpression
  NavigationExpression type: IncludeExpression
  → Has nested IncludeExpression (ThenInclude): Producto
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Added to PendingIncludes (total: 2)
🔍 Visitando NavigationExpression para buscar ThenInclude...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ DETECTED IncludeExpression:
  Navigation: Producto
  DeclaringType: Linea
  TargetType: Producto
  IsCollection: False
  IsSubCollection: False
  EntityExpression type: StructuralTypeShaperExpression
  NavigationExpression type: StructuralTypeShaperExpression
  → NO nested IncludeExpression
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Added to PendingIncludes (total: 3)
⚠ No NavigationExpression, es el último nivel

╔═══════════════════════════════════════════╗
║  RESUMEN DE INCLUDES DETECTADOS           ║
╚═══════════════════════════════════════════╝
Total PendingIncludes: 3

  📁 Cliente:
    └─[Collection] Pedidos → Pedido

  📁 Linea:
    └─[Reference] Producto → Producto

  📁 Pedido:
    └─[Collection] Lineas → Linea

═════════════════════════════════════════════
```

---

## Si solo ves 1 Include detectado

### Posible causa 1: No estás visitando recursivamente

Verifica que tu `VisitExtension` llame a `base.VisitExtension()`:

```csharp
protected override Expression VisitExtension(Expression node)
{
    if (node is IncludeExpression includeExpression)
    {
        ProcessInclude(includeExpression);
    }
    
    // ✅ IMPORTANTE: llamar a base para continuar visitando
    return base.VisitExtension(node);
}
```

### Posible causa 2: No estás visitando NavigationExpression

Verifica que en `ProcessInclude`:

```csharp
if (includeExpression.NavigationExpression != null)
{
    Visit(includeExpression.NavigationExpression); // ✅ Esto debe estar
}
```

### Posible causa 3: El ShaperExpression no contiene los Include

Agrega logging ANTES de crear el visitor:

```csharp
protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
{
    Console.WriteLine("\n🔍 ShaperExpression type: " + shapedQueryExpression.ShaperExpression.GetType().Name);
    Console.WriteLine("🔍 ShaperExpression content:");
    Console.WriteLine(shapedQueryExpression.ShaperExpression.ToString());
    Console.WriteLine();
    
    var includeDetector = new IncludeDetectorVisitor(firestoreQueryExpression);
    includeDetector.Visit(shapedQueryExpression.ShaperExpression);
    // ...
}
```

Esto te dirá si el `ShaperExpression` realmente contiene los `IncludeExpression`.

---

## Verifica tu modelo de Firestore

### Configuración correcta

```csharp
modelBuilder.Entity<Cliente>(entity =>
{
    entity.HasKey(c => c.Id);
    
    // ✅ Configurar como subcollection
    entity.SubCollection(c => c.Pedidos)
          .SubCollection(p => p.Lineas);  // ← Esto configura Pedido.Lineas
});

modelBuilder.Entity<Linea>(entity =>
{
    entity.HasKey(l => l.Id);
    
    // ✅ Configurar referencia a Producto
    entity.HasOne(l => l.Producto)
          .WithMany()  // o .WithMany(p => p.Lineas) si Producto tiene colección
          .HasForeignKey(l => l.ProductoId);
});
```

**IMPORTANTE:** La configuración `.SubCollection(p => p.Lineas)` debe marcar la navegación para que:

```csharp
// Este método devuelva true
navigation.IsSubCollection() == true
```

Verifica que tu extensión `IsSubCollection()` esté correctamente implementada:

```csharp
public static class NavigationExtensions
{
    public static bool IsSubCollection(this IReadOnlyNavigation navigation)
    {
        // Verificar si tiene la anotación de subcollection
        return navigation.FindAnnotation("Firestore:SubCollection")?.Value as bool? == true;
    }
}
```

---

## Checklist completo

- [ ] `IncludeDetectorVisitor.VisitExtension` llama a `base.VisitExtension()`
- [ ] `ProcessInclude` llama a `Visit(includeExpression.NavigationExpression)`
- [ ] `ShaperExpression` contiene `IncludeExpression` (verificar con ToString())
- [ ] El modelo está configurado con `.SubCollection()`
- [ ] La extensión `IsSubCollection()` funciona correctamente
- [ ] No hay excepciones silenciosas en el visitor

---

## Debugging avanzado: Dump del árbol de expresiones

Si quieres ver la estructura COMPLETA del `ShaperExpression`:

```csharp
private void DumpExpressionTree(Expression expression, int indent = 0)
{
    var prefix = new string(' ', indent * 2);
    Console.WriteLine($"{prefix}{expression.GetType().Name}:");
    
    if (expression is IncludeExpression inc)
    {
        Console.WriteLine($"{prefix}  Navigation: {inc.Navigation.Name}");
        Console.WriteLine($"{prefix}  EntityExpression:");
        DumpExpressionTree(inc.EntityExpression, indent + 2);
        if (inc.NavigationExpression != null)
        {
            Console.WriteLine($"{prefix}  NavigationExpression:");
            DumpExpressionTree(inc.NavigationExpression, indent + 2);
        }
    }
    else if (expression is BinaryExpression bin)
    {
        Console.WriteLine($"{prefix}  Left:");
        DumpExpressionTree(bin.Left, indent + 2);
        Console.WriteLine($"{prefix}  Right:");
        DumpExpressionTree(bin.Right, indent + 2);
    }
    // Agregar más tipos según necesites
}
```

Llama esto desde `VisitShapedQuery`:

```csharp
Console.WriteLine("\n📊 DUMP DEL SHAPER EXPRESSION TREE:");
DumpExpressionTree(shapedQueryExpression.ShaperExpression);
Console.WriteLine();
```

---

## Siguiente paso

1. **Agrega el logging mejorado** a `ProcessInclude`
2. **Ejecuta tu query** de prueba
3. **Comparte el output** que ves en la consola
4. **Compara** con el output esperado de arriba

Si solo ves 1 Include en lugar de 3, sabremos exactamente dónde está el problema.
