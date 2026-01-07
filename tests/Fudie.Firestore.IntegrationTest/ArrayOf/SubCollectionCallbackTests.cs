using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para SubCollection con callback.
/// Verifica la nueva API:
/// - SubCollection(e => e.X, c => c.Reference(...))
/// - SubCollection(e => e.X, c => c.ArrayOf(...))
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SubCollectionCallbackTests
{
    private readonly FirestoreTestFixture _fixture;

    public SubCollectionCallbackTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region SubCollection con Reference callback

    [Fact]
    public async Task SubCollection_WithReferenceCallback_ShouldPersistDocumentReference()
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

        // Act
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Verificar que el documento padre NO contiene el array de Pedidos
        var parentRawData = await GetDocumentRawData<ClienteConVendedor>(clienteId);
        parentRawData.Should().NotContainKey("Pedidos",
            "Las subcollections no deben guardarse como arrays en el documento padre");

        // Assert - Verificar que el vendedor se guardó como DocumentReference en la subcollection
        var rawData = await GetSubCollectionDocumentRawData<ClienteConVendedor, PedidoConVendedor>(
            clienteId, pedidoId);

        rawData.Should().ContainKey("Numero");
        rawData["Numero"].Should().Be("PED-001");

        // El vendedor debe estar como DocumentReference
        rawData.Should().ContainKey("Vendedor");
        rawData["Vendedor"].Should().BeOfType<DocumentReference>();

        var vendedorRef = (DocumentReference)rawData["Vendedor"];
        vendedorRef.Id.Should().Be(vendedorId);
    }

    #endregion

    #region SubCollection con ArrayOf callback

    [Fact]
    public async Task SubCollection_WithArrayOfCallback_ShouldPersistEmbeddedArray()
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

        // Act
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Verificar que el documento padre NO contiene el array de Pedidos
        var parentRawData = await GetDocumentRawData<ClienteConLineas>(clienteId);
        parentRawData.Should().NotContainKey("Pedidos",
            "Las subcollections no deben guardarse como arrays en el documento padre");

        // Assert - Verificar que las líneas se guardaron como array embebido
        var rawData = await GetSubCollectionDocumentRawData<ClienteConLineas, PedidoConLineas>(
            clienteId, pedidoId);

        rawData.Should().ContainKey("Numero");
        rawData["Numero"].Should().Be("PED-002");

        rawData.Should().ContainKey("Lineas");
        var lineas = ((IEnumerable<object>)rawData["Lineas"]).ToList();
        lineas.Should().HaveCount(3);

        var primeraLinea = lineas[0] as Dictionary<string, object>;
        primeraLinea.Should().NotBeNull();
        primeraLinea!["ProductoNombre"].Should().Be("Laptop");
        primeraLinea["Cantidad"].Should().Be(1L); // Firestore devuelve long
    }

    #endregion

    #region SubCollection con todos los tipos de arrays

    [Fact]
    public async Task SubCollection_WithAllArrayTypes_ShouldPersistCorrectly()
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

        // Act
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Verificar que el documento padre NO contiene el array de Ordenes
        var parentRawData = await GetDocumentRawData<ClienteCompleto>(clienteId);
        parentRawData.Should().NotContainKey("Ordenes",
            "Las subcollections no deben guardarse como arrays en el documento padre");
        parentRawData["Nombre"].Should().Be("Corporación TechMax");
        parentRawData["Email"].Should().Be("compras@techmax.com");

        // Assert - Obtener datos crudos de la subcollection
        var rawData = await GetSubCollectionDocumentRawData<ClienteCompleto, OrdenCompleta>(
            clienteId, ordenId);

        // Verificar campos básicos
        rawData["NumeroOrden"].Should().Be("ORD-2024-001");

        // Assert - Array de References (productos)
        rawData.Should().ContainKey("Productos");
        var productos = ((IEnumerable<object>)rawData["Productos"]).ToList();
        productos.Should().HaveCount(3);
        productos.Should().AllBeOfType<DocumentReference>();

        var productosRefs = productos.Cast<DocumentReference>().ToList();
        productosRefs.Select(r => r.Id).Should().BeEquivalentTo([producto1Id, producto2Id, producto3Id]);

        // Assert - Array de GeoPoints (ruta de entrega)
        rawData.Should().ContainKey("RutaEntrega");
        var rutaEntrega = ((IEnumerable<object>)rawData["RutaEntrega"]).ToList();
        rutaEntrega.Should().HaveCount(3);
        rutaEntrega.Should().AllBeOfType<GeoPoint>();

        var geoPoints = rutaEntrega.Cast<GeoPoint>().ToList();
        geoPoints[0].Latitude.Should().BeApproximately(40.7128, 0.0001);
        geoPoints[0].Longitude.Should().BeApproximately(-74.0060, 0.0001);
        geoPoints[1].Latitude.Should().BeApproximately(40.7580, 0.0001);
        geoPoints[2].Latitude.Should().BeApproximately(40.7484, 0.0001);

        // Assert - Array de ValueObjects (descuentos)
        rawData.Should().ContainKey("Descuentos");
        var descuentos = ((IEnumerable<object>)rawData["Descuentos"]).ToList();
        descuentos.Should().HaveCount(2);

        var primerDescuento = descuentos[0] as Dictionary<string, object>;
        primerDescuento.Should().NotBeNull();
        primerDescuento!["Codigo"].Should().Be("WELCOME10");
        primerDescuento["Descripcion"].Should().Be("Descuento de bienvenida");

        var segundoDescuento = descuentos[1] as Dictionary<string, object>;
        segundoDescuento.Should().NotBeNull();
        segundoDescuento!["Codigo"].Should().Be("BULK5");
    }

    #endregion

    #region Helpers

    private async Task<Dictionary<string, object>> GetDocumentRawData<T>(string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var collectionName = GetCollectionName<T>();

        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue(
            $"El documento {documentId} debe existir en {collectionName}");

        return docSnapshot.ToDictionary();
    }

    private async Task<Dictionary<string, object>> GetSubCollectionDocumentRawData<TParent, TChild>(
        string parentId, string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var parentCollection = GetCollectionName<TParent>();
        var childCollection = GetCollectionName<TChild>();

        var docSnapshot = await firestoreDb
            .Collection(parentCollection)
            .Document(parentId)
            .Collection(childCollection)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue(
            $"El documento {documentId} debe existir en {parentCollection}/{parentId}/{childCollection}");

        return docSnapshot.ToDictionary();
    }

#pragma warning disable EF1001
    private static string GetCollectionName<T>()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001

    #endregion
}
