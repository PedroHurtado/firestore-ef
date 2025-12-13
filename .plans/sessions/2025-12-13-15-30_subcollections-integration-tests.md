# Plan: Tests de Integración para SubCollections

**Fecha:** 2025-12-13 15:30

## Objetivo

Crear tests de integración que cubran las operaciones CRUD y consultas con SubCollections en el provider Firestore EF Core.

## Contexto

El provider ya soporta SubCollections como se demuestra en `firestore-test/Program.cs`:
- SubCollections de 1 nivel: `Cliente -> Pedidos`
- SubCollections anidadas de 2 niveles: `Cliente -> Pedidos -> Lineas`
- Include/ThenInclude para cargar subcollections

## Estructura de Tests Propuesta

```
tests/Fudie.Firestore.IntegrationTest/
├── SubCollections/
│   └── SubCollectionTests.cs    <- NUEVO
├── Helpers/
│   ├── TestEntities.cs          <- MODIFICAR (agregar LineaPedido)
│   └── TestDbContext.cs         <- MODIFICAR (agregar configuración anidada)
```

## Modificaciones Necesarias

### 1. TestEntities.cs - Agregar LineaPedido

```csharp
/// <summary>
/// Entidad subcollection anidada de Pedido.
/// Path: /clientes/{clienteId}/pedidos/{pedidoId}/lineas/{lineaId}
/// </summary>
public class LineaPedido
{
    public string? Id { get; set; }
    public string? ProductoId { get; set; }  // Referencia a Producto
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    // Navegación
    public Producto? Producto { get; set; }
}
```

Modificar `Pedido` para incluir `Lineas`:
```csharp
public class Pedido
{
    // ... propiedades existentes ...
    public List<LineaPedido> Lineas { get; set; } = [];
}
```

### 2. TestDbContext.cs - Configurar SubCollections Anidadas

```csharp
// Configuración de Cliente con subcollections anidadas
modelBuilder.Entity<Cliente>(entity =>
{
    entity.SubCollection(c => c.Pedidos)
          .SubCollection(p => p.Lineas);
});
```

## Tests a Implementar

### SubCollectionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_ClienteConPedidos_ShouldPersistHierarchy` | Crear cliente con pedidos en un SaveChanges |
| `Query_ClienteWithIncludePedidos_ShouldLoadSubCollection` | Leer cliente con Include de pedidos |
| `Add_ClienteConPedidosYLineas_ShouldPersistNestedHierarchy` | Crear estructura anidada de 2 niveles |
| `Query_ClienteWithThenInclude_ShouldLoadNestedSubCollections` | Include().ThenInclude() para 2 niveles |
| `Update_PedidoEnSubCollection_ShouldPersistChanges` | Actualizar entidad en subcollection |
| `Delete_PedidoFromSubCollection_ShouldRemoveFromFirestore` | Eliminar entidad de subcollection |

### Detalle de Tests

#### Test 1: Add_ClienteConPedidos_ShouldPersistHierarchy
```csharp
// Arrange
var cliente = new Cliente
{
    Id = GenerateId("cli"),
    Nombre = "Test Cliente",
    Email = "test@test.com",
    Pedidos = [
        new Pedido { Id = GenerateId("ped"), NumeroOrden = "ORD-001", Total = 100m },
        new Pedido { Id = GenerateId("ped"), NumeroOrden = "ORD-002", Total = 200m }
    ]
};

// Act
context.Clientes.Add(cliente);
await context.SaveChangesAsync();

// Assert - Verificar paths en Firestore
// Path: /clientes/{clienteId}/pedidos/{pedidoId}
```

#### Test 2: Query_ClienteWithIncludePedidos_ShouldLoadSubCollection
```csharp
// Arrange - Crear datos
// ...

// Act
var clienteConPedidos = await context.Clientes
    .Include(c => c.Pedidos)
    .FirstOrDefaultAsync(c => c.Id == clienteId);

// Assert
clienteConPedidos.Pedidos.Should().HaveCount(2);
```

#### Test 3: Add_ClienteConPedidosYLineas_ShouldPersistNestedHierarchy
```csharp
// Arrange
var cliente = new Cliente
{
    Id = GenerateId("cli"),
    Nombre = "Cliente Completo",
    Email = "completo@test.com",
    Pedidos = [
        new Pedido
        {
            Id = GenerateId("ped"),
            NumeroOrden = "ORD-003",
            Total = 150m,
            Lineas = [
                new LineaPedido { Id = GenerateId("lin"), Cantidad = 2, PrecioUnitario = 50m },
                new LineaPedido { Id = GenerateId("lin"), Cantidad = 1, PrecioUnitario = 50m }
            ]
        }
    ]
};

// Act & Assert
```

#### Test 4: Query_ClienteWithThenInclude_ShouldLoadNestedSubCollections
```csharp
// Act
var cliente = await context.Clientes
    .Include(c => c.Pedidos)
        .ThenInclude(p => p.Lineas)
    .FirstOrDefaultAsync(c => c.Id == clienteId);

// Assert
cliente.Pedidos[0].Lineas.Should().HaveCount(2);
```

## Orden de Implementación

### Fase 1: Preparación de Entidades ✅ COMPLETADA
1. [x] Modificar `TestEntities.cs` - Agregar `LineaPedido`
2. [x] Modificar `TestEntities.cs` - Agregar `Lineas` a `Pedido`
3. [x] Modificar `TestDbContext.cs` - Configurar subcollections anidadas
4. [x] **Build para verificar compilación**

### Fase 2: Tests de SubCollection de 1 Nivel ✅ COMPLETADA
5. [x] Crear `SubCollections/SubCollectionTests.cs`
6. [x] Implementar `Add_ClienteConPedidos_ShouldPersistHierarchy`
7. [x] Implementar `Query_ClienteWithIncludePedidos_ShouldLoadSubCollection`
8. [x] **Ejecutar tests y verificar**

### Fase 3: Tests de SubCollections Anidadas (2 niveles) ✅ COMPLETADA
9. [x] Implementar `Add_ClienteConPedidosYLineas_ShouldPersistNestedHierarchy`
10. [x] Implementar `Query_ClienteWithThenInclude_ShouldLoadNestedSubCollections`
11. [x] **Ejecutar tests y verificar**

### Fase 4: Tests de Update/Delete en SubCollections ✅ COMPLETADA
12. [x] Implementar `Update_PedidoEnSubCollection_ShouldPersistChanges`
13. [x] Implementar `Delete_PedidoFromSubCollection_ShouldRemoveFromFirestore`
14. [x] **Ejecutar todos los tests**

### Fase 5: Commit ✅ COMPLETADA
15. [x] Commit con mensaje descriptivo
**Commit:** `95de045`

## Comandos de Verificación

```bash
# Build del proyecto de tests
dotnet build tests/Fudie.Firestore.IntegrationTest

# Ejecutar solo tests de SubCollections
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "SubCollectionTests"

# Ejecutar todos los tests de integración
dotnet test tests/Fudie.Firestore.IntegrationTest
```

## Dependencias

- Emulador de Firestore corriendo: `docker-compose up -d`
- Provider con soporte de SubCollections (ya implementado)
- Tracking de entidades (ya implementado en sesión anterior)

## Riesgos

| Riesgo | Mitigación |
|--------|------------|
| Emulador no corriendo | Verificar con `docker ps` antes de tests |
| Paths incorrectos en subcollections | Verificar logs del provider |
| Tracking de entidades en subcollections | Ya cubierto en Fase 4 del plan anterior |

## Notas de Implementación

### Limitación Encontrada: Update/Delete en SubCollections

Al implementar los tests de Update y Delete, se descubrió que el provider requiere que la entidad padre esté marcada como `Modified` en el ChangeTracker para poder construir el path de la subcollection.

**Workaround aplicado:**
```csharp
// Marcar el padre como modificado para que el provider pueda construir el path
updateContext.Entry(clienteParaActualizar).State = EntityState.Modified;
```

Esto podría ser una mejora futura para el provider: detectar automáticamente el padre cuando se modifica/elimina una entidad de subcollection.
