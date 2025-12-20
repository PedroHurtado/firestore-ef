# Plan TDD: Auto-registro de SubCollections - EF Core Firestore Provider

**Fecha:** 2025-12-20

---

## Objetivo

Modificar `SubCollection<T>()` para que auto-registre las entidades hijas en el modelo, eliminando la necesidad de definir `DbSet<T>` para entidades que no son agregados raíz.

---

## Progreso

| Paso | Acción | Estado | Commit |
|------|--------|--------|--------|
| 1 | Modificar `SubCollection<T>()` para auto-registrar | ⬚ | |
| 2 | Quitar `DbSet<Pedido>` y `DbSet<LineaPedido>` de TestDbContext | ⬚ | |
| 3 | Correr tests de integración existentes | ⬚ | |

---

## Regla de Oro

| Tipo | ¿Necesita DbSet? | ¿Por qué? |
|------|------------------|-----------|
| **Agregado raíz** | ✅ Sí | Acceso independiente |
| **SubCollection** | ❌ No | Vive dentro del agregado |
| **Target de Reference** | ✅ Sí | Es agregado independiente |
| **ComplexType** | ❌ No | Embebido en documento |

---

## Implementación

### Paso 1: Modificar SubCollectionBuilderExtensions

**Antes:**
```csharp
var targetEntityType = entityType.Model.FindEntityType(typeof(TRelatedEntity)) 
    ?? throw new InvalidOperationException(
        $"Entity type '{typeof(TRelatedEntity).Name}' must be added to the model...");
```

**Después:**
```csharp
var mutableModel = (IMutableModel)entityType.Model;

var targetEntityType = mutableModel.FindEntityType(typeof(TRelatedEntity)) 
    ?? mutableModel.AddEntityType(typeof(TRelatedEntity));
```

### Paso 2: Limpiar TestDbContext

**Antes:**
```csharp
public class TestDbContext : DbContext
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ProductoCompleto> ProductosCompletos => Set<ProductoCompleto>();

    // ❌ Estos sobran
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<LineaPedido> LineasPedido => Set<LineaPedido>();
}
```

**Después:**
```csharp
public class TestDbContext : DbContext
{
    // Solo agregados raíz
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ProductoCompleto> ProductosCompletos => Set<ProductoCompleto>();
}
```

### Paso 3: Correr tests

```bash
dotnet test --filter "FullyQualifiedName~SubCollectionTests"
```

Los tests existentes ya cubren:
- SubCollection de 1 nivel (Cliente → Pedidos)
- SubCollection anidada de 2 niveles (Cliente → Pedidos → Lineas)
- Include y ThenInclude
- Filtered Include
- Update en SubCollection
- Delete en SubCollection

---

## Tests existentes que validan el cambio

| Test | Qué valida |
|------|------------|
| `Add_ClienteConPedidos_ShouldPersistHierarchy` | Insert con SubCollection |
| `Add_ClienteConPedidosYLineas_ShouldPersistNestedHierarchy` | Insert con 2 niveles |
| `Query_ClienteWithIncludePedidos_ShouldLoadSubCollection` | Include funciona |
| `Query_ClienteWithThenInclude_ShouldLoadNestedSubCollections` | ThenInclude funciona |
| `Query_ClienteWithFilteredIncludeAndThenInclude_...` | Filtered Include funciona |
| `Update_PedidoEnSubCollection_ShouldPersistChanges` | Update funciona |
| `Delete_PedidoFromSubCollection_ShouldRemoveFromFirestore` | Delete funciona |

---

## Si los tests fallan

Posibles ajustes:

1. **Convenciones no aplicadas**: Verificar que `AddEntityType()` dispara las convenciones automáticamente
2. **Navegación no creada**: Asegurar que `HasMany()` se llama correctamente
3. **Referencias rotas**: Los tests de Delete usan `context.Pedidos.Remove()` - habrá que cambiar a `context.Remove(pedido)`

---

## Resultado esperado

Después del cambio, el DbContext queda alineado con DDD:

```csharp
public class TestDbContext : DbContext
{
    // ✅ Solo agregados raíz expuestos
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ProductoCompleto> ProductosCompletos => Set<ProductoCompleto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SubCollections se auto-registran
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.SubCollection(c => c.Pedidos)
                  .SubCollection(p => p.Lineas);
        });
    }
}
```
