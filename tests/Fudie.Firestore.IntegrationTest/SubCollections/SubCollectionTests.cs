using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.SubCollections;

/// <summary>
/// Tests de integración para operaciones con SubCollections.
/// Cubre subcollections de 1 nivel y anidadas (2 niveles).
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SubCollectionTests
{
    private readonly FirestoreTestFixture _fixture;

    public SubCollectionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Tests de 1 nivel (Cliente -> Pedidos)

    [Fact]
    public async Task Add_ClienteConPedidos_ShouldPersistHierarchy()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedido1Id = FirestoreTestFixture.GenerateId("ped");
        var pedido2Id = FirestoreTestFixture.GenerateId("ped");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente SubCollection Test",
            Email = "subcollection@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = pedido1Id,
                    NumeroOrden = "ORD-SC-001",
                    Total = 150.00m,
                    Estado = EstadoPedido.Pendiente
                },
                new Pedido
                {
                    Id = pedido2Id,
                    NumeroOrden = "ORD-SC-002",
                    Total = 250.00m,
                    Estado = EstadoPedido.Confirmado
                }
            ]
        };

        // Act
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Verificar que se guardó correctamente
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteLeido = await readContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        clienteLeido.Should().NotBeNull();
        clienteLeido!.Nombre.Should().Be("Cliente SubCollection Test");
        clienteLeido.Pedidos.Should().HaveCount(2);
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-SC-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-SC-002");
    }

    [Fact]
    public async Task Query_ClienteWithIncludePedidos_ShouldLoadSubCollection()
    {
        // Arrange - Crear cliente con pedidos
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Include Test",
            Email = "include@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-INC-001",
                    Total = 100.00m
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-INC-002",
                    Total = 200.00m
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-INC-003",
                    Total = 300.00m
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteConPedidos = await readContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteConPedidos.Should().NotBeNull();
        clienteConPedidos!.Pedidos.Should().NotBeNull();
        clienteConPedidos.Pedidos.Should().HaveCount(3);

        // Verificar que los datos de los pedidos se cargaron correctamente
        var totales = clienteConPedidos.Pedidos.Select(p => p.Total).OrderBy(t => t).ToList();
        totales.Should().BeEquivalentTo([100.00m, 200.00m, 300.00m]);
    }

    #endregion

    #region Tests de 2 niveles (Cliente -> Pedidos -> Lineas)

    [Fact]
    public async Task Add_ClienteConPedidosYLineas_ShouldPersistNestedHierarchy()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Nested Test",
            Email = "nested@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-NEST-001",
                    Total = 200.00m,
                    Estado = EstadoPedido.Pendiente,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 2,
                            PrecioUnitario = 50.00m
                        },
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 1,
                            PrecioUnitario = 100.00m
                        }
                    ]
                }
            ]
        };

        // Act
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Verificar jerarquía completa
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteLeido = await readContext.Clientes
            .Include(c => c.Pedidos)
                .ThenInclude(p => p.Lineas)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().HaveCount(1);
        clienteLeido.Pedidos[0].Lineas.Should().HaveCount(2);
        clienteLeido.Pedidos[0].Lineas.Should().Contain(l => l.Cantidad == 2 && l.PrecioUnitario == 50.00m);
        clienteLeido.Pedidos[0].Lineas.Should().Contain(l => l.Cantidad == 1 && l.PrecioUnitario == 100.00m);
    }

    [Fact]
    public async Task Query_ClienteWithThenInclude_ShouldLoadNestedSubCollections()
    {
        // Arrange - Crear estructura completa
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente ThenInclude Test",
            Email = "theninclude@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-TI-001",
                    Total = 350.00m,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 3,
                            PrecioUnitario = 50.00m
                        },
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 2,
                            PrecioUnitario = 100.00m
                        }
                    ]
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-TI-002",
                    Total = 75.00m,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 1,
                            PrecioUnitario = 75.00m
                        }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con ThenInclude
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteCompleto = await readContext.Clientes
            .Include(c => c.Pedidos)
                .ThenInclude(p => p.Lineas)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteCompleto.Should().NotBeNull();
        clienteCompleto!.Pedidos.Should().HaveCount(2);

        var pedido1 = clienteCompleto.Pedidos.First(p => p.NumeroOrden == "ORD-TI-001");
        pedido1.Lineas.Should().HaveCount(2);

        var pedido2 = clienteCompleto.Pedidos.First(p => p.NumeroOrden == "ORD-TI-002");
        pedido2.Lineas.Should().HaveCount(1);

        // Verificar totales de líneas
        var totalLineasPedido1 = pedido1.Lineas.Sum(l => l.Cantidad * l.PrecioUnitario);
        totalLineasPedido1.Should().Be(350.00m); // (3*50) + (2*100)

        var totalLineasPedido2 = pedido2.Lineas.Sum(l => l.Cantidad * l.PrecioUnitario);
        totalLineasPedido2.Should().Be(75.00m); // 1*75
    }

    #endregion

    #region Tests de Update/Delete en SubCollections

    [Fact]
    public async Task Update_PedidoEnSubCollection_ShouldPersistChanges()
    {
        // Arrange - Crear cliente con pedidos
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedidoId = FirestoreTestFixture.GenerateId("ped");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Update Test",
            Email = "update@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = pedidoId,
                    NumeroOrden = "ORD-UPD-001",
                    Total = 100.00m,
                    Estado = EstadoPedido.Pendiente
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer y actualizar el pedido
        // Nota: El provider requiere que el padre esté en el ChangeTracker
        // para poder construir el path de la subcollection
        using var updateContext = _fixture.CreateContext<TestDbContext>();
        var clienteParaActualizar = await updateContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        var pedidoParaActualizar = clienteParaActualizar!.Pedidos.First(p => p.Id == pedidoId);
        pedidoParaActualizar.Total = 250.00m;
        pedidoParaActualizar.Estado = EstadoPedido.Confirmado;

        // Marcar el padre como modificado para que el provider pueda construir el path
        updateContext.Entry(clienteParaActualizar).State = EntityState.Modified;

        await updateContext.SaveChangesAsync();

        // Assert - Verificar cambios
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteLeido = await readContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        var pedidoActualizado = clienteLeido!.Pedidos.First(p => p.Id == pedidoId);
        pedidoActualizado.Total.Should().Be(250.00m);
        pedidoActualizado.Estado.Should().Be(EstadoPedido.Confirmado);
    }

    [Fact]
    public async Task Delete_PedidoFromSubCollection_ShouldRemoveFromFirestore()
    {
        // Arrange - Crear cliente con múltiples pedidos
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedidoAEliminarId = FirestoreTestFixture.GenerateId("ped");
        var pedidoAManterId = FirestoreTestFixture.GenerateId("ped");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Delete Test",
            Email = "delete@test.com",
            Pedidos =
            [
                new Pedido
                {
                    Id = pedidoAEliminarId,
                    NumeroOrden = "ORD-DEL-001",
                    Total = 100.00m
                },
                new Pedido
                {
                    Id = pedidoAManterId,
                    NumeroOrden = "ORD-KEEP-001",
                    Total = 200.00m
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Eliminar un pedido
        // Nota: El provider requiere que el padre esté en el ChangeTracker
        using var deleteContext = _fixture.CreateContext<TestDbContext>();
        var clienteParaEliminar = await deleteContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        var pedidoAEliminar = clienteParaEliminar!.Pedidos.First(p => p.Id == pedidoAEliminarId);

        // Marcar el padre como modificado para que el provider pueda construir el path
        deleteContext.Entry(clienteParaEliminar).State = EntityState.Modified;
        deleteContext.Pedidos.Remove(pedidoAEliminar);

        await deleteContext.SaveChangesAsync();

        // Assert - Verificar que solo queda un pedido
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteLeido = await readContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        clienteLeido!.Pedidos.Should().HaveCount(1);
        clienteLeido.Pedidos.Should().Contain(p => p.Id == pedidoAManterId);
        clienteLeido.Pedidos.Should().NotContain(p => p.Id == pedidoAEliminarId);
    }

    #endregion
}
