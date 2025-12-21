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

    [Fact]
    public async Task Query_ClienteWithFilteredIncludeAndThenInclude_ShouldLoadFilteredNestedSubCollections()
    {
        // Arrange - Crear estructura con datos variados para filtrar
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Filtered Include Test",
            Email = "filtered-include@test.com",
            Pedidos =
            [
                // Pedido 1: Confirmado con líneas de diferentes cantidades
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-FI-001",
                    Total = 500.00m,
                    Estado = EstadoPedido.Confirmado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 5, // Cantidad >= 3, debe incluirse
                            PrecioUnitario = 50.00m
                        },
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 2, // Cantidad < 3, NO debe incluirse
                            PrecioUnitario = 100.00m
                        },
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 3, // Cantidad >= 3, debe incluirse
                            PrecioUnitario = 75.00m
                        }
                    ]
                },
                // Pedido 2: Pendiente, NO debe incluirse por el filtro de estado
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-FI-002",
                    Total = 100.00m,
                    Estado = EstadoPedido.Pendiente,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 10,
                            PrecioUnitario = 10.00m
                        }
                    ]
                },
                // Pedido 3: Confirmado con líneas que no pasan el filtro
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-FI-003",
                    Total = 150.00m,
                    Estado = EstadoPedido.Confirmado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 1, // Cantidad < 3, NO debe incluirse
                            PrecioUnitario = 150.00m
                        }
                    ]
                },
                // Pedido 4: Enviado (debe incluirse) con líneas variadas
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-FI-004",
                    Total = 400.00m,
                    Estado = EstadoPedido.Enviado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("lin"),
                            Cantidad = 4, // Cantidad >= 3, debe incluirse
                            PrecioUnitario = 100.00m
                        }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include filtrado y ThenInclude filtrado
        // Filtro Include: Solo pedidos Confirmados o Enviados
        // Filtro ThenInclude: Solo líneas con Cantidad >= 3
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteFiltrado = await readContext.Clientes
            .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado || p.Estado == EstadoPedido.Enviado))
                .ThenInclude(p => p.Lineas.Where(l => l.Cantidad >= 3))
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteFiltrado.Should().NotBeNull();

        // Solo deben cargarse pedidos Confirmados o Enviados (3 de 4)
        clienteFiltrado!.Pedidos.Should().HaveCount(3);
        clienteFiltrado.Pedidos.Should().NotContain(p => p.NumeroOrden == "ORD-FI-002"); // Pendiente excluido

        // Verificar pedido 1 (Confirmado): debe tener solo 2 líneas (Cantidad >= 3)
        var pedido1 = clienteFiltrado.Pedidos.First(p => p.NumeroOrden == "ORD-FI-001");
        pedido1.Estado.Should().Be(EstadoPedido.Confirmado);
        pedido1.Lineas.Should().HaveCount(2);
        pedido1.Lineas.Should().AllSatisfy(l => l.Cantidad.Should().BeGreaterThanOrEqualTo(3));
        pedido1.Lineas.Should().Contain(l => l.Cantidad == 5);
        pedido1.Lineas.Should().Contain(l => l.Cantidad == 3);

        // Verificar pedido 3 (Confirmado): debe tener 0 líneas (ninguna pasa el filtro)
        var pedido3 = clienteFiltrado.Pedidos.First(p => p.NumeroOrden == "ORD-FI-003");
        pedido3.Estado.Should().Be(EstadoPedido.Confirmado);
        pedido3.Lineas.Should().BeEmpty();

        // Verificar pedido 4 (Enviado): debe tener 1 línea
        var pedido4 = clienteFiltrado.Pedidos.First(p => p.NumeroOrden == "ORD-FI-004");
        pedido4.Estado.Should().Be(EstadoPedido.Enviado);
        pedido4.Lineas.Should().HaveCount(1);
        pedido4.Lineas[0].Cantidad.Should().Be(4);
    }

    [Fact]
    public async Task Query_ClienteWithFilteredIncludeById_ShouldLoadOnlyMatchingEntities()
    {
        // Arrange - Crear estructura con IDs conocidos para filtrar
        using var context = _fixture.CreateContext<TestDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli");

        var pedido1Id = FirestoreTestFixture.GenerateId("ped");
        var pedido2Id = FirestoreTestFixture.GenerateId("ped");
        var pedido3Id = FirestoreTestFixture.GenerateId("ped");

        var linea1Id = FirestoreTestFixture.GenerateId("lin");
        var linea2Id = FirestoreTestFixture.GenerateId("lin");
        var linea3Id = FirestoreTestFixture.GenerateId("lin");
        var linea4Id = FirestoreTestFixture.GenerateId("lin");

        var cliente = new Cliente
        {
            Id = clienteId,
            Nombre = "Cliente Filter By Id Test",
            Email = "filter-by-id@test.com",
            Pedidos =
            [
                // Pedido 1: Debe incluirse (filtraremos por su ID)
                new Pedido
                {
                    Id = pedido1Id,
                    NumeroOrden = "ORD-ID-001",
                    Total = 300.00m,
                    Estado = EstadoPedido.Confirmado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = linea1Id, // Debe incluirse
                            Cantidad = 2,
                            PrecioUnitario = 100.00m
                        },
                        new LineaPedido
                        {
                            Id = linea2Id, // NO debe incluirse
                            Cantidad = 1,
                            PrecioUnitario = 100.00m
                        }
                    ]
                },
                // Pedido 2: NO debe incluirse (no está en el filtro)
                new Pedido
                {
                    Id = pedido2Id,
                    NumeroOrden = "ORD-ID-002",
                    Total = 150.00m,
                    Estado = EstadoPedido.Pendiente,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = linea3Id,
                            Cantidad = 3,
                            PrecioUnitario = 50.00m
                        }
                    ]
                },
                // Pedido 3: Debe incluirse (filtraremos por su ID)
                new Pedido
                {
                    Id = pedido3Id,
                    NumeroOrden = "ORD-ID-003",
                    Total = 200.00m,
                    Estado = EstadoPedido.Enviado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = linea4Id, // Debe incluirse
                            Cantidad = 4,
                            PrecioUnitario = 50.00m
                        }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include filtrado por ID de pedido y ThenInclude filtrado por ID de línea
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var clienteFiltrado = await readContext.Clientes
            .Include(c => c.Pedidos.Where(p => p.Id == pedido1Id || p.Id == pedido3Id))
                .ThenInclude(p => p.Lineas.Where(l => l.Id == linea1Id || l.Id == linea4Id))
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteFiltrado.Should().NotBeNull();

        // Solo deben cargarse pedidos 1 y 3 (filtrados por ID)
        clienteFiltrado!.Pedidos.Should().HaveCount(2);
        clienteFiltrado.Pedidos.Should().Contain(p => p.Id == pedido1Id);
        clienteFiltrado.Pedidos.Should().Contain(p => p.Id == pedido3Id);
        clienteFiltrado.Pedidos.Should().NotContain(p => p.Id == pedido2Id);

        // Verificar pedido 1: solo linea1Id debe estar
        var pedido1 = clienteFiltrado.Pedidos.First(p => p.Id == pedido1Id);
        pedido1.NumeroOrden.Should().Be("ORD-ID-001");
        pedido1.Lineas.Should().HaveCount(1);
        pedido1.Lineas[0].Id.Should().Be(linea1Id);
        pedido1.Lineas[0].Cantidad.Should().Be(2);

        // Verificar pedido 3: solo linea4Id debe estar
        var pedido3 = clienteFiltrado.Pedidos.First(p => p.Id == pedido3Id);
        pedido3.NumeroOrden.Should().Be("ORD-ID-003");
        pedido3.Lineas.Should().HaveCount(1);
        pedido3.Lineas[0].Id.Should().Be(linea4Id);
        pedido3.Lineas[0].Cantidad.Should().Be(4);
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
        using var updateContext = _fixture.CreateContext<TestDbContext>();
        var clienteParaActualizar = await updateContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        var pedidoParaActualizar = clienteParaActualizar!.Pedidos.First(p => p.Id == pedidoId);
        pedidoParaActualizar.Total = 250.00m;
        pedidoParaActualizar.Estado = EstadoPedido.Confirmado;

        // El provider ahora busca automáticamente el padre en el ChangeTracker
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
        using var deleteContext = _fixture.CreateContext<TestDbContext>();
        var clienteParaEliminar = await deleteContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        var pedidoAEliminar = clienteParaEliminar!.Pedidos.First(p => p.Id == pedidoAEliminarId);

        // Usar Remove() directamente ya que Pedido no tiene DbSet (es SubCollection)
        deleteContext.Remove(pedidoAEliminar);

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
