using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.SubCollections;

/// <summary>
/// Tests de integración para diferentes tipos de colección en navegaciones.
/// Ciclo 4: List{T} (baseline - ya funciona)
/// Ciclo 5: ICollection{T}
/// Ciclo 6: HashSet{T}
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class CollectionTypesTests
{
    private readonly FirestoreTestFixture _fixture;

    public CollectionTypesTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 4: List<T> (baseline - ya funciona)

    /// <summary>
    /// Ciclo 4: Verifica que List{T} funciona correctamente con .Include()
    /// Este es el baseline - ya debería funcionar.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithListProperty_ShouldWork()
    {
        // Arrange - Crear cliente con pedidos usando List<T>
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-list");

        var cliente = new ClienteConList
        {
            Id = clienteId,
            Nombre = "Cliente Con List Test",
            Email = "list@test.com",
            Pedidos =
            [
                new PedidoList
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-LIST-001",
                    Total = 100.00m
                },
                new PedidoList
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-LIST-002",
                    Total = 200.00m
                }
            ]
        };

        context.ClientesConList.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConList
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().NotBeNull();
        clienteLeido.Pedidos.Should().BeOfType<List<PedidoList>>();
        clienteLeido.Pedidos.Should().HaveCount(2);
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-LIST-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-LIST-002");
    }

    #endregion

    #region Ciclo 5: ICollection<T>

    /// <summary>
    /// Ciclo 5: Verifica que ICollection{T} funciona correctamente con .Include()
    /// La propiedad está declarada como ICollection{T}, debe materializarse como List{T}.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithICollectionProperty_ShouldWork()
    {
        // Arrange - Crear cliente con pedidos usando ICollection<T>
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-icol");

        var cliente = new ClienteConICollection
        {
            Id = clienteId,
            Nombre = "Cliente Con ICollection Test",
            Email = "icollection@test.com",
            Pedidos = new List<PedidoICollection>
            {
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-001",
                    Total = 150.00m
                },
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-002",
                    Total = 250.00m
                },
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-003",
                    Total = 350.00m
                }
            }
        };

        context.ClientesConICollection.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConICollection
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().NotBeNull();
        // ICollection<T> debe poder contener los elementos (no necesariamente ser List<T>)
        clienteLeido.Pedidos.Should().HaveCount(3);
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-ICOL-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-ICOL-002");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-ICOL-003");

        // Verificar que los totales se cargaron correctamente
        var totales = clienteLeido.Pedidos.Select(p => p.Total).OrderBy(t => t).ToList();
        totales.Should().BeEquivalentTo([150.00m, 250.00m, 350.00m]);
    }

    /// <summary>
    /// Ciclo 5: Verifica que ICollection{T} funciona con Filtered Include.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithICollectionProperty_FilteredInclude_ShouldWork()
    {
        // Arrange - Crear cliente con pedidos variados
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-icol-filt");

        var cliente = new ClienteConICollection
        {
            Id = clienteId,
            Nombre = "Cliente ICollection Filtered Test",
            Email = "icollection-filtered@test.com",
            Pedidos = new List<PedidoICollection>
            {
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-F-001",
                    Total = 50.00m  // < 100, NO debe incluirse
                },
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-F-002",
                    Total = 150.00m  // >= 100, debe incluirse
                },
                new PedidoICollection
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-ICOL-F-003",
                    Total = 200.00m  // >= 100, debe incluirse
                }
            }
        };

        context.ClientesConICollection.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Filtered Include (solo pedidos >= 100)
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConICollection
            .Include(c => c.Pedidos.Where(p => p.Total >= 100))
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().NotBeNull();
        clienteLeido.Pedidos.Should().HaveCount(2);
        clienteLeido.Pedidos.Should().NotContain(p => p.NumeroOrden == "ORD-ICOL-F-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-ICOL-F-002");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-ICOL-F-003");
    }

    #endregion

    #region Ciclo 6: HashSet<T>

    /// <summary>
    /// Ciclo 6: Verifica que HashSet{T} funciona correctamente con .Include()
    /// La propiedad está declarada como HashSet{T}, debe materializarse como HashSet{T}.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithHashSetProperty_ShouldWork()
    {
        // Arrange - Crear cliente con pedidos usando HashSet<T>
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-hash");

        var cliente = new ClienteConHashSet
        {
            Id = clienteId,
            Nombre = "Cliente Con HashSet Test",
            Email = "hashset@test.com",
            Pedidos =
            [
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-001",
                    Total = 175.00m
                },
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-002",
                    Total = 275.00m
                }
            ]
        };

        context.ClientesConHashSet.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConHashSet
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().NotBeNull();
        clienteLeido.Pedidos.Should().BeOfType<HashSet<PedidoHashSet>>();
        clienteLeido.Pedidos.Should().HaveCount(2);
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-HASH-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-HASH-002");
    }

    /// <summary>
    /// Ciclo 6: Verifica que HashSet{T} funciona con Filtered Include.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithHashSetProperty_FilteredInclude_ShouldWork()
    {
        // Arrange - Crear cliente con pedidos variados
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-hash-filt");

        var cliente = new ClienteConHashSet
        {
            Id = clienteId,
            Nombre = "Cliente HashSet Filtered Test",
            Email = "hashset-filtered@test.com",
            Pedidos =
            [
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-F-001",
                    Total = 80.00m  // < 100, NO debe incluirse
                },
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-F-002",
                    Total = 120.00m  // >= 100, debe incluirse
                },
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-F-003",
                    Total = 180.00m  // >= 100, debe incluirse
                },
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-HASH-F-004",
                    Total = 90.00m  // < 100, NO debe incluirse
                }
            ]
        };

        context.ClientesConHashSet.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Filtered Include (solo pedidos >= 100)
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConHashSet
            .Include(c => c.Pedidos.Where(p => p.Total >= 100))
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        clienteLeido.Should().NotBeNull();
        clienteLeido!.Pedidos.Should().NotBeNull();
        clienteLeido.Pedidos.Should().BeOfType<HashSet<PedidoHashSet>>();
        clienteLeido.Pedidos.Should().HaveCount(2);
        clienteLeido.Pedidos.Should().NotContain(p => p.NumeroOrden == "ORD-HASH-F-001");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-HASH-F-002");
        clienteLeido.Pedidos.Should().Contain(p => p.NumeroOrden == "ORD-HASH-F-003");
        clienteLeido.Pedidos.Should().NotContain(p => p.NumeroOrden == "ORD-HASH-F-004");
    }

    /// <summary>
    /// Ciclo 6: Verifica que HashSet{T} mantiene la unicidad de elementos.
    /// </summary>
    [Fact]
    public async Task Include_SubCollection_WithHashSetProperty_ShouldMaintainUniqueness()
    {
        // Arrange - Crear cliente con pedidos
        using var context = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteId = FirestoreTestFixture.GenerateId("cli-hash-unique");
        var pedidoId = FirestoreTestFixture.GenerateId("ped");

        var cliente = new ClienteConHashSet
        {
            Id = clienteId,
            Nombre = "Cliente HashSet Unique Test",
            Email = "hashset-unique@test.com",
            Pedidos =
            [
                new PedidoHashSet
                {
                    Id = pedidoId,
                    NumeroOrden = "ORD-UNIQUE-001",
                    Total = 100.00m
                },
                new PedidoHashSet
                {
                    Id = FirestoreTestFixture.GenerateId("ped"),
                    NumeroOrden = "ORD-UNIQUE-002",
                    Total = 200.00m
                }
            ]
        };

        context.ClientesConHashSet.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con Include
        using var readContext = _fixture.CreateContext<CollectionTypesDbContext>();
        var clienteLeido = await readContext.ClientesConHashSet
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert - Verificar que es un HashSet válido
        clienteLeido.Should().NotBeNull();
        var hashSet = clienteLeido!.Pedidos as HashSet<PedidoHashSet>;
        hashSet.Should().NotBeNull();

        // Intentar agregar un elemento duplicado (mismo Id) no debería aumentar el count
        var duplicado = new PedidoHashSet
        {
            Id = pedidoId,  // Mismo Id que el primer pedido
            NumeroOrden = "ORD-DUPLICATE",
            Total = 999.00m
        };

        var countAntes = hashSet!.Count;
        hashSet.Add(duplicado);  // HashSet debería rechazarlo o reemplazarlo
        hashSet.Count.Should().Be(countAntes);  // Count no debería cambiar
    }

    #endregion
}
