using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for Select with subcollections.
/// Fase 4: Subcollections en Select
/// Ciclo 11: Select con subcollection completa
/// Ciclo 12: Select con subcollection proyectada
/// Ciclo 13: Select con subcollection filtrada
/// Ciclo 14: Select con subcollection filtrada + ordenada
/// Ciclo 15: Select con subcollection filtrada + ordenada + limitada
/// Ciclo 16: Select con múltiples subcollections
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SelectSubcollectionTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectSubcollectionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 11: Select con subcollection completa

    [Fact]
    public async Task Select_WithSubcollection_ReturnsRootFieldsAndSubcollection()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsub"),
            Nombre = "Cliente Select Sub",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-001",
                    Total = 100m,
                    Estado = EstadoPedido.Confirmado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-002",
                    Total = 200m,
                    Estado = EstadoPedido.Pendiente
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                c.Pedidos
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Select Sub");
        result.Pedidos.Should().HaveCount(2);
        result.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-001");
        result.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-002");
    }

    #endregion

    #region Ciclo 12: Select con subcollection proyectada

    [Fact]
    public async Task Select_WithProjectedSubcollection_ReturnsOnlySelectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-proj-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsubp"),
            Nombre = "Cliente Proyección",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "PRJ-001",
                    Total = 150.50m,
                    Estado = EstadoPedido.Enviado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "PRJ-002",
                    Total = 299.99m,
                    Estado = EstadoPedido.Entregado
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                Totales = c.Pedidos.Select(p => p.Total)
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Proyección");
        result.Totales.Should().HaveCount(2);
        result.Totales.Should().Contain(150.50m);
        result.Totales.Should().Contain(299.99m);
    }

    #endregion

    #region Ciclo 13: Select con subcollection filtrada

    [Fact]
    public async Task Select_WithFilteredSubcollection_ReturnsOnlyMatchingItems()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-filt-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsubf"),
            Nombre = "Cliente Filtrado",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "FLT-001",
                    Total = 50m,
                    Estado = EstadoPedido.Pendiente
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "FLT-002",
                    Total = 150m,
                    Estado = EstadoPedido.Confirmado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "FLT-003",
                    Total = 250m,
                    Estado = EstadoPedido.Confirmado
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                PedidosConfirmados = c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado)
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Filtrado");
        result.PedidosConfirmados.Should().HaveCount(2);
        result.PedidosConfirmados.Should().AllSatisfy(p => p.Estado.Should().Be(EstadoPedido.Confirmado));
    }

    #endregion

    #region Ciclo 14: Select con subcollection filtrada + ordenada

    [Fact]
    public async Task Select_WithFilteredAndOrderedSubcollection_ReturnsOrderedResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-ord-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsubo"),
            Nombre = "Cliente Ordenado",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-C",
                    Total = 300m,
                    Estado = EstadoPedido.Enviado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-A",
                    Total = 100m,
                    Estado = EstadoPedido.Enviado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-B",
                    Total = 200m,
                    Estado = EstadoPedido.Enviado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "ORD-X",
                    Total = 50m,
                    Estado = EstadoPedido.Cancelado
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                PedidosEnviados = c.Pedidos
                    .Where(p => p.Estado == EstadoPedido.Enviado)
                    .OrderBy(p => p.Total)
                    .ToList() // Required by EF Core for IOrderedEnumerable in projections
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Ordenado");
        result.PedidosEnviados.Should().HaveCount(3);
        result.PedidosEnviados[0].Total.Should().Be(100m);
        result.PedidosEnviados[1].Total.Should().Be(200m);
        result.PedidosEnviados[2].Total.Should().Be(300m);
    }

    #endregion

    #region Ciclo 15: Select con subcollection filtrada + ordenada + limitada

    [Fact]
    public async Task Select_WithFilteredOrderedLimitedSubcollection_ReturnsTopN()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-lim-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsubl"),
            Nombre = "Cliente Limitado",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "LIM-001",
                    Total = 500m,
                    Estado = EstadoPedido.Entregado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "LIM-002",
                    Total = 300m,
                    Estado = EstadoPedido.Entregado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "LIM-003",
                    Total = 100m,
                    Estado = EstadoPedido.Entregado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "LIM-004",
                    Total = 400m,
                    Estado = EstadoPedido.Entregado
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "LIM-005",
                    Total = 200m,
                    Estado = EstadoPedido.Cancelado
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Top 2 pedidos entregados ordenados por total descendente
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                TopPedidos = c.Pedidos
                    .Where(p => p.Estado == EstadoPedido.Entregado)
                    .OrderByDescending(p => p.Total)
                    .Take(2)
                    .ToList() // Required by EF Core for ordered collections in projections
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Limitado");
        result.TopPedidos.Should().HaveCount(2);
        result.TopPedidos[0].Total.Should().Be(500m);
        result.TopPedidos[1].Total.Should().Be(400m);
    }

    #endregion

    #region Ciclo 16: Select con múltiples subcollections (cliente con pedidos y líneas)

    [Fact(Skip = "Ciclo 16: Requiere soporte para navegaciones anidadas (p.Lineas.Count())")]
    public async Task Select_WithNestedSubcollections_ReturnsAllLevels()
    {
        // Arrange
        using var context = _fixture.CreateContext<TestDbContext>();
        var uniqueEmail = $"select-sub-nest-{Guid.NewGuid():N}@test.com";
        var cliente = new Cliente
        {
            Id = FirestoreTestFixture.GenerateId("selectsubn"),
            Nombre = "Cliente Anidado",
            Email = uniqueEmail,
            Pedidos =
            [
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "NEST-001",
                    Total = 350m,
                    Estado = EstadoPedido.Confirmado,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("linea"),
                            Cantidad = 2,
                            PrecioUnitario = 100m
                        },
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("linea"),
                            Cantidad = 3,
                            PrecioUnitario = 50m
                        }
                    ]
                },
                new Pedido
                {
                    Id = FirestoreTestFixture.GenerateId("pedido"),
                    NumeroOrden = "NEST-002",
                    Total = 150m,
                    Estado = EstadoPedido.Pendiente,
                    Lineas =
                    [
                        new LineaPedido
                        {
                            Id = FirestoreTestFixture.GenerateId("linea"),
                            Cantidad = 1,
                            PrecioUnitario = 150m
                        }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<TestDbContext>();
        var results = await readContext.Clientes
            .Where(c => c.Email == uniqueEmail)
            .Select(c => new
            {
                c.Nombre,
                Pedidos = c.Pedidos.Select(p => new
                {
                    p.NumeroOrden,
                    p.Total,
                    CantidadLineas = p.Lineas.Count()
                })
            })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Nombre.Should().Be("Cliente Anidado");
        result.Pedidos.Should().HaveCount(2);

        var pedidosList = result.Pedidos.OrderBy(p => p.NumeroOrden).ToList();
        pedidosList[0].NumeroOrden.Should().Be("NEST-001");
        pedidosList[0].Total.Should().Be(350m);
        pedidosList[0].CantidadLineas.Should().Be(2);

        pedidosList[1].NumeroOrden.Should().Be("NEST-002");
        pedidosList[1].Total.Should().Be(150m);
        pedidosList[1].CantidadLineas.Should().Be(1);
    }

    #endregion
}
