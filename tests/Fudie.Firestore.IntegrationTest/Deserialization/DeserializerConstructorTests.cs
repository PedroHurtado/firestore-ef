using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Deserialization;

/// <summary>
/// Tests de integración para deserialización de entidades.
/// Verifica soporte de diferentes tipos de constructores.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class DeserializerConstructorTests
{
    private readonly FirestoreTestFixture _fixture;

    public DeserializerConstructorTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Ciclo 2: Constructor sin parámetros (baseline)

    [Fact]
    public async Task DeserializeEntity_WithParameterlessConstructor_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var producto = new Producto
        {
            Id = FirestoreTestFixture.GenerateId("deser"),
            Nombre = "Test Deserializer",
            Precio = 99.99m,
            Activo = true
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer de Firestore (deserialización)
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoLeido = await readContext.Productos.FindAsync(producto.Id);

        // Assert
        Assert.NotNull(productoLeido);
        Assert.Equal(producto.Id, productoLeido.Id);
        Assert.Equal("Test Deserializer", productoLeido.Nombre);
        Assert.Equal(99.99m, productoLeido.Precio);
        Assert.True(productoLeido.Activo);
    }

    [Fact]
    public async Task DeserializeEntities_WithParameterlessConstructor_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var uniqueTag = $"deser-list-{Guid.NewGuid():N}";
        var productos = new[]
        {
            new Producto { Id = FirestoreTestFixture.GenerateId("deser"), Nombre = uniqueTag, Precio = 10.00m },
            new Producto { Id = FirestoreTestFixture.GenerateId("deser"), Nombre = uniqueTag, Precio = 20.00m },
            new Producto { Id = FirestoreTestFixture.GenerateId("deser"), Nombre = uniqueTag, Precio = 30.00m }
        };

        context.Productos.AddRange(productos);
        await context.SaveChangesAsync();

        // Act - Leer múltiples entidades (deserialización de lista)
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productosLeidos = await readContext.Productos
            .Where(p => p.Nombre == uniqueTag)
            .ToListAsync();

        // Assert
        Assert.Equal(3, productosLeidos.Count);
        Assert.All(productosLeidos, p => Assert.Equal(uniqueTag, p.Nombre));
    }

    #endregion
}
