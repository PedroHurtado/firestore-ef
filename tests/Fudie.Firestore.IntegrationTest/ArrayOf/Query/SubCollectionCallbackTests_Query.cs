using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para SubCollection con callback - DESERIALIZACIÓN con LINQ.
/// Verifica la nueva API:
/// - SubCollection(e => e.X, c => c.Reference(...))
/// - SubCollection(e => e.X, c => c.ArrayOf(...))
/// Patrón: Guardar con EF Core → Leer con LINQ (Include para SubCollections y References) → Verificar estructura.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SubCollectionCallbackTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public SubCollectionCallbackTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region SubCollection con Reference callback

    [Fact]
    public async Task SubCollection_WithReferenceCallback_ShouldDeserializeWithInclude()
    {
        // Arrange
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var vendedorId = FirestoreTestFixture.GenerateId("vend");
        var pedidoId = FirestoreTestFixture.GenerateId("ped");
        using var context = _fixture.CreateContext<SubCollectionWithReferenceDbContext>();

        // Crear vendedor primero
        var vendedor = new Vendedor
        {
            Id = vendedorId,
            Nombre = "Juan Pérez",
            Zona = "Norte"
        };
        context.Vendedores.Add(vendedor);

        // Crear cliente con pedido que referencia al vendedor
        var cliente = new ClienteConVendedor
        {
            Id = clienteId,
            Nombre = "Empresa ABC",
            Email = "contacto@abc.com",
            Pedidos =
            [
                new PedidoConVendedor
                {
                    Id = pedidoId,
                    Numero = "PED-001",
                    Fecha = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    Total = 1500.00m,
                    Vendedor = vendedor
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para SubCollection y Reference anidada
        using var readContext = _fixture.CreateContext<SubCollectionWithReferenceDbContext>();
        var result = await readContext.Clientes
            .Include(c => c.Pedidos)
            .ThenInclude(p => p.Vendedor)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Empresa ABC");
        result.Pedidos.Should().HaveCount(1);
        result.Pedidos[0].Numero.Should().Be("PED-001");
        result.Pedidos[0].Total.Should().Be(1500.00m);

        // Verificar que el vendedor se deserializó correctamente
        result.Pedidos[0].Vendedor.Should().NotBeNull();
        result.Pedidos[0].Vendedor!.Id.Should().Be(vendedorId);
        result.Pedidos[0].Vendedor!.Nombre.Should().Be("Juan Pérez");
        result.Pedidos[0].Vendedor!.Zona.Should().Be("Norte");
    }

    #endregion

    #region SubCollection con ArrayOf callback

    [Fact]
    public async Task SubCollection_WithArrayOfCallback_ShouldDeserializeEmbeddedArray()
    {
        // Arrange
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedidoId = FirestoreTestFixture.GenerateId("ped");
        using var context = _fixture.CreateContext<SubCollectionWithArrayOfDbContext>();

        var cliente = new ClienteConLineas
        {
            Id = clienteId,
            Nombre = "Empresa XYZ",
            Email = "contacto@xyz.com",
            Pedidos =
            [
                new PedidoConLineas
                {
                    Id = pedidoId,
                    Numero = "PED-002",
                    Fecha = new DateTime(2024, 2, 20, 0, 0, 0, DateTimeKind.Utc),
                    Total = 350.00m,
                    Lineas =
                    [
                        new LineaDetalle { ProductoNombre = "Laptop", Cantidad = 1, PrecioUnitario = 200.00m },
                        new LineaDetalle { ProductoNombre = "Mouse", Cantidad = 2, PrecioUnitario = 25.00m },
                        new LineaDetalle { ProductoNombre = "Teclado", Cantidad = 1, PrecioUnitario = 100.00m }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para SubCollection
        using var readContext = _fixture.CreateContext<SubCollectionWithArrayOfDbContext>();
        var result = await readContext.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Empresa XYZ");
        result.Pedidos.Should().HaveCount(1);
        result.Pedidos[0].Numero.Should().Be("PED-002");
        result.Pedidos[0].Total.Should().Be(350.00m);

        // Verificar que las líneas se deserializaron correctamente
        result.Pedidos[0].Lineas.Should().HaveCount(3);
        result.Pedidos[0].Lineas[0].ProductoNombre.Should().Be("Laptop");
        result.Pedidos[0].Lineas[0].Cantidad.Should().Be(1);
        result.Pedidos[0].Lineas[0].PrecioUnitario.Should().Be(200.00m);
        result.Pedidos[0].Lineas[1].ProductoNombre.Should().Be("Mouse");
        result.Pedidos[0].Lineas[2].ProductoNombre.Should().Be("Teclado");
    }

    #endregion

    #region SubCollection con todos los tipos de arrays

    [Fact]
    public async Task SubCollection_WithAllArrayTypes_ShouldDeserializeCorrectly()
    {
        // Arrange
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var ordenId = FirestoreTestFixture.GenerateId("ord");
        var producto1Id = FirestoreTestFixture.GenerateId("prod");
        var producto2Id = FirestoreTestFixture.GenerateId("prod");
        var producto3Id = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<SubCollectionWithAllArraysDbContext>();

        // Crear productos primero
        var producto1 = new ProductoRef { Id = producto1Id, Nombre = "Laptop Pro", Sku = "LAP-001", PrecioBase = 1200.00m };
        var producto2 = new ProductoRef { Id = producto2Id, Nombre = "Mouse Wireless", Sku = "MOU-001", PrecioBase = 45.00m };
        var producto3 = new ProductoRef { Id = producto3Id, Nombre = "Teclado Mecánico", Sku = "TEC-001", PrecioBase = 150.00m };
        context.Productos.AddRange(producto1, producto2, producto3);

        // Crear cliente con orden completa
        var cliente = new ClienteCompleto
        {
            Id = clienteId,
            Nombre = "Corporación TechMax",
            Email = "compras@techmax.com",
            Telefono = "+1-555-0100",
            Ordenes =
            [
                new OrdenCompleta
                {
                    Id = ordenId,
                    NumeroOrden = "ORD-2024-001",
                    FechaCreacion = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc),
                    Total = 1500.00m,

                    // Array de References
                    Productos = [producto1, producto2, producto3],

                    // Array de GeoPoints (ruta de entrega)
                    RutaEntrega =
                    [
                        new PuntoEntrega { Latitude = 40.7128, Longitude = -74.0060 },  // NYC
                        new PuntoEntrega { Latitude = 40.7580, Longitude = -73.9855 },  // Times Square
                        new PuntoEntrega { Latitude = 40.7484, Longitude = -73.9857 }   // Empire State
                    ],

                    // Array de ValueObjects
                    Descuentos =
                    [
                        new DescuentoAplicado { Codigo = "WELCOME10", Descripcion = "Descuento de bienvenida", Porcentaje = 10.0m },
                        new DescuentoAplicado { Codigo = "BULK5", Descripcion = "Descuento por volumen", Porcentaje = 5.0m }
                    ]
                }
            ]
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include para SubCollection y References
        using var readContext = _fixture.CreateContext<SubCollectionWithAllArraysDbContext>();
        var result = await readContext.Clientes
            .Include(c => c.Ordenes)
            .ThenInclude(o => o.Productos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Assert - Verificar cliente
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Corporación TechMax");
        result.Email.Should().Be("compras@techmax.com");
        result.Telefono.Should().Be("+1-555-0100");

        // Assert - Verificar orden
        result.Ordenes.Should().HaveCount(1);
        result.Ordenes[0].NumeroOrden.Should().Be("ORD-2024-001");
        result.Ordenes[0].Total.Should().Be(1500.00m);

        // Assert - Array de References (productos)
        result.Ordenes[0].Productos.Should().HaveCount(3);
        result.Ordenes[0].Productos.Select(p => p.Id).Should().BeEquivalentTo([producto1Id, producto2Id, producto3Id]);
        var laptopProd = result.Ordenes[0].Productos.First(p => p.Id == producto1Id);
        laptopProd.Nombre.Should().Be("Laptop Pro");
        laptopProd.Sku.Should().Be("LAP-001");
        laptopProd.PrecioBase.Should().Be(1200.00m);

        // Assert - Array de GeoPoints (ruta de entrega)
        result.Ordenes[0].RutaEntrega.Should().HaveCount(3);
        result.Ordenes[0].RutaEntrega[0].Latitude.Should().BeApproximately(40.7128, 0.0001);
        result.Ordenes[0].RutaEntrega[0].Longitude.Should().BeApproximately(-74.0060, 0.0001);
        result.Ordenes[0].RutaEntrega[1].Latitude.Should().BeApproximately(40.7580, 0.0001);
        result.Ordenes[0].RutaEntrega[2].Latitude.Should().BeApproximately(40.7484, 0.0001);

        // Assert - Array de ValueObjects (descuentos)
        result.Ordenes[0].Descuentos.Should().HaveCount(2);
        result.Ordenes[0].Descuentos[0].Codigo.Should().Be("WELCOME10");
        result.Ordenes[0].Descuentos[0].Descripcion.Should().Be("Descuento de bienvenida");
        result.Ordenes[0].Descuentos[0].Porcentaje.Should().Be(10.0m);
        result.Ordenes[0].Descuentos[1].Codigo.Should().Be("BULK5");
    }

    #endregion
}
