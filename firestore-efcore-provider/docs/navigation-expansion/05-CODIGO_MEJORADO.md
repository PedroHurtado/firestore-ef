```cs

// ============================================================================
// VERSIÓN MEJORADA DE IncludeDetectorVisitor
// Con logging completo para debugging
// ============================================================================

/// <summary>
/// Visitor que detecta IncludeExpression en el árbol del shaper.
/// Construye una lista plana de navegaciones para soportar Include/ThenInclude.
/// </summary>
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
        if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
        {
            ProcessInclude(includeExpression);
        }

        return base.VisitExtension(node);
    }

    private void ProcessInclude(Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
    {
        if (includeExpression.Navigation is not IReadOnlyNavigation navigation)
        {
            Console.WriteLine($"{GetIndent()}⚠ IncludeExpression with null Navigation, skipping");
            return;
        }

        Console.WriteLine($"{GetIndent()}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"{GetIndent()}✓ DETECTED IncludeExpression #{_queryExpression.PendingIncludes.Count + 1}:");
        Console.WriteLine($"{GetIndent()}  Navigation: {navigation.Name}");
        Console.WriteLine($"{GetIndent()}  DeclaringType: {navigation.DeclaringEntityType.ClrType.Name}");
        Console.WriteLine($"{GetIndent()}  TargetType: {navigation.TargetEntityType.ClrType.Name}");
        Console.WriteLine($"{GetIndent()}  IsCollection: {navigation.IsCollection}");

        // Verificar si es subcollection
        var isSubCollection = navigation.IsSubCollection();
        Console.WriteLine($"{GetIndent()}  IsSubCollection: {isSubCollection}");

        // Información sobre el EntityExpression
        var entityExprType = includeExpression.EntityExpression?.GetType().Name ?? "null";
        Console.WriteLine($"{GetIndent()}  EntityExpression type: {entityExprType}");

        // Información sobre el NavigationExpression
        var navExprType = includeExpression.NavigationExpression?.GetType().Name ?? "null";
        Console.WriteLine($"{GetIndent()}  NavigationExpression type: {navExprType}");

        // Detectar si hay ThenInclude anidado
        if (includeExpression.NavigationExpression is Microsoft.EntityFrameworkCore.Query.IncludeExpression nested)
        {
            Console.WriteLine($"{GetIndent()}  → Has nested IncludeExpression (ThenInclude): {nested.Navigation.Name}");
        }

        Console.WriteLine($"{GetIndent()}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // ✅ Agregar a la lista de includes pendientes
        _queryExpression.PendingIncludes.Add(navigation);
        Console.WriteLine($"{GetIndent()}✅ Added to PendingIncludes (total: {_queryExpression.PendingIncludes.Count})");

        // 🔑 CLAVE: Visitar NavigationExpression recursivamente para capturar ThenInclude
        if (includeExpression.NavigationExpression != null)
        {
            Console.WriteLine($"{GetIndent()}🔍 Visitando NavigationExpression para buscar ThenInclude...\n");
            _depth++;
            Visit(includeExpression.NavigationExpression);
            _depth--;
        }
        else
        {
            Console.WriteLine($"{GetIndent()}⚠ No NavigationExpression, es el último nivel\n");
        }
    }

    private string GetIndent()
    {
        return new string(' ', _depth * 2);
    }
}

// ============================================================================
// RESUMEN FINAL (agregar al final de VisitShapedQuery)
// ============================================================================

protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        // Obtener el FirestoreQueryExpression
        var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

        // ✅ DEBUGGING: Ver el tipo de ShaperExpression
        Console.WriteLine("\n🔍 ═══════════════════════════════════════════════════════");
        Console.WriteLine($"🔍 ShaperExpression type: {shapedQueryExpression.ShaperExpression.GetType().Name}");
        Console.WriteLine("🔍 ═══════════════════════════════════════════════════════\n");

        // Procesar el shaper original para detectar y extraer Include expressions
        var includeDetector = new IncludeDetectorVisitor(firestoreQueryExpression);
        includeDetector.Visit(shapedQueryExpression.ShaperExpression);

        // ✅ DEBUGGING: Resumen final
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║         RESUMEN DE INCLUDES DETECTADOS                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
        Console.WriteLine($"Total PendingIncludes: {firestoreQueryExpression.PendingIncludes.Count}\n");

        if (firestoreQueryExpression.PendingIncludes.Count == 0)
        {
            Console.WriteLine("⚠ ⚠ ⚠  NO SE DETECTÓ NINGÚN INCLUDE  ⚠ ⚠ ⚠");
            Console.WriteLine("Verifica:");
            Console.WriteLine("  1. Que estés usando .Include() en la query");
            Console.WriteLine("  2. Que el ShaperExpression contenga IncludeExpression");
            Console.WriteLine("  3. Que el modelo esté configurado correctamente\n");
        }
        else
        {
            // Agrupar por DeclaringType para visualizar el árbol
            var grouped = firestoreQueryExpression.PendingIncludes
                .GroupBy(n => n.DeclaringEntityType.ClrType.Name)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                Console.WriteLine($"  📁 {group.Key}:");
                foreach (var nav in group)
                {
                    var typeIndicator = nav.IsCollection ? "[Collection]" : "[Reference]";
                    var isSubColl = nav.IsSubCollection() ? "✓ SubCollection" : "⚠ NOT SubCollection";
                    Console.WriteLine($"    └─{typeIndicator} {nav.Name} → {nav.TargetEntityType.ClrType.Name} ({isSubColl})");
                }
            }

            Console.WriteLine($"\n  📊 Árbol de carga esperado:");
            PrintLoadingTree(firestoreQueryExpression.PendingIncludes);
        }

        Console.WriteLine($"\n═══════════════════════════════════════════════════════\n");

        // ... resto del código (crear shaper, etc.)
    }

// ============================================================================
// HELPER: Visualizar el árbol de carga
// ============================================================================

private void PrintLoadingTree(List<IReadOnlyNavigation> navigations)
{
    // Encontrar entidades raíz (las que no son target de ninguna otra)
    var allTargetTypes = new HashSet<IReadOnlyEntityType>(
        navigations.Select(n => n.TargetEntityType));

    var rootTypes = navigations
        .Select(n => n.DeclaringEntityType)
        .Distinct()
        .Where(t => !allTargetTypes.Contains(t))
        .ToList();

    foreach (var rootType in rootTypes)
    {
        Console.WriteLine($"  {rootType.ClrType.Name}");
        PrintNavigationChildren(rootType, navigations, indent: "    ");
    }
}

private void PrintNavigationChildren(
    IReadOnlyEntityType entityType,
    List<IReadOnlyNavigation> allNavigations,
    string indent)
{
    var children = allNavigations
        .Where(n => n.DeclaringEntityType == entityType)
        .ToList();

    foreach (var child in children)
    {
        var indicator = child.IsCollection ? "└─[1:N]" : "└─[N:1]";
        Console.WriteLine($"{indent}{indicator} {child.Name} → {child.TargetEntityType.ClrType.Name}");

        // Recursión para hijos anidados
        PrintNavigationChildren(child.TargetEntityType, allNavigations, indent + "    ");
    }
}
´´´
// ============================================================================
// OUTPUT ESPERADO PARA:
// .Include(c => c.Pedidos).ThenInclude(p => p.Lineas).ThenInclude(l => l.Producto)
// ============================================================================

/*
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ DETECTED IncludeExpression #1:
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
  ✓ DETECTED IncludeExpression #2:
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
    ✓ DETECTED IncludeExpression #3:
      Navigation: Producto
      DeclaringType: Linea
      TargetType: Producto
      IsCollection: False
      IsSubCollection: False
      EntityExpression type: StructuralTypeShaperExpression
      NavigationExpression type: StructuralTypeShaperExpression
    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ✅ Added to PendingIncludes (total: 3)
    ⚠ No NavigationExpression, es el último nivel

╔═══════════════════════════════════════════════════════╗
║         RESUMEN DE INCLUDES DETECTADOS                ║
╚═══════════════════════════════════════════════════════╝
Total PendingIncludes: 3

  📁 Cliente:
    └─[Collection] Pedidos → Pedido (✓ SubCollection)

  📁 Linea:
    └─[Reference] Producto → Producto (⚠ NOT SubCollection)

  📁 Pedido:
    └─[Collection] Lineas → Linea (✓ SubCollection)

  📊 Árbol de carga esperado:
  Cliente
    └─[1:N] Pedidos → Pedido
        └─[1:N] Lineas → Linea
            └─[N:1] Producto → Producto

═══════════════════════════════════════════════════════
*/
