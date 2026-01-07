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
