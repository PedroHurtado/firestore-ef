using Microsoft.EntityFrameworkCore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

namespace Fudie.Firestore.IntegrationTest.ArrayOf.Query;

/// <summary>
/// Tests de integración para ArrayOf con HashSet y records.
/// DESERIALIZACIÓN con LINQ.
/// Verifica que ICollection&lt;T&gt; funciona correctamente (no solo List&lt;T&gt;).
/// Todo AUTO-DETECTADO por conventions - sin configuración explícita.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class HashSetArrayOfTests_Query
{
    private readonly FirestoreTestFixture _fixture;

    public HashSetArrayOfTests_Query(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region HashSet<Record> Embedded - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordEmbedded_AutoDetected_ShouldDeserializeAsCollection()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            Tags =
            [
                new Tag("electronica", "blue"),
                new Tag("gaming", "red"),
                new Tag("premium", "gold")
            ]
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ProductoHashSetDbContext>();
        var result = await readContext.Productos
            .FirstOrDefaultAsync(p => p.Id == productoId);

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Laptop Gaming");
        result.Precio.Should().Be(1299.99m);
        result.Tags.Should().HaveCount(3);

        // HashSet no garantiza orden, verificamos que contenga los elementos
        result.Tags.Should().Contain(t => t.Nombre == "electronica" && t.Color == "blue");
        result.Tags.Should().Contain(t => t.Nombre == "gaming" && t.Color == "red");
        result.Tags.Should().Contain(t => t.Nombre == "premium" && t.Color == "gold");
    }

    #endregion

    #region HashSet<Record> GeoPoint - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordGeoPoint_AutoDetected_ShouldDeserializeAsGeoPoints()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            PuntosVenta =
            [
                new Ubicacion(40.4168, -3.7038),
                new Ubicacion(41.3851, 2.1734)
            ]
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ
        using var readContext = _fixture.CreateContext<ProductoHashSetDbContext>();
        var result = await readContext.Productos
            .FirstOrDefaultAsync(p => p.Id == productoId);

        // Assert
        result.Should().NotBeNull();
        result!.PuntosVenta.Should().HaveCount(2);

        // HashSet no garantiza orden, verificamos que contenga los elementos
        result.PuntosVenta.Should().Contain(u =>
            Math.Abs(u.Latitude - 40.4168) < 0.0001 && Math.Abs(u.Longitude - (-3.7038)) < 0.0001);
        result.PuntosVenta.Should().Contain(u =>
            Math.Abs(u.Latitude - 41.3851) < 0.0001 && Math.Abs(u.Longitude - 2.1734) < 0.0001);
    }

    #endregion

    #region HashSet<Record> Reference - AUTO-DETECTADO

    [Fact]
    public async Task HashSet_RecordReference_AutoDetected_ShouldDeserializeWithInclude()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        var prov1Id = FirestoreTestFixture.GenerateId("prov");
        var prov2Id = FirestoreTestFixture.GenerateId("prov");
        using var context = _fixture.CreateContext<ProductoHashSetDbContext>();

        var proveedor1 = new Proveedor { Id = prov1Id, Nombre = "TechSupplier", Pais = "USA" };
        var proveedor2 = new Proveedor { Id = prov2Id, Nombre = "AsiaComponents", Pais = "China" };
        context.Proveedores.AddRange(proveedor1, proveedor2);

        var producto = new ProductoConHashSet
        {
            Id = productoId,
            Nombre = "Laptop Gaming",
            Precio = 1299.99m,
            Proveedores = [proveedor1, proveedor2]
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer con LINQ + Include
        using var readContext = _fixture.CreateContext<ProductoHashSetDbContext>();
        var result = await readContext.Productos
            .Include(p => p.Proveedores)
            .FirstOrDefaultAsync(p => p.Id == productoId);

        // Assert
        result.Should().NotBeNull();
        result!.Proveedores.Should().HaveCount(2);

        // HashSet no garantiza orden
        result.Proveedores.Select(p => p.Id).Should().BeEquivalentTo([prov1Id, prov2Id]);
        result.Proveedores.Should().Contain(p => p.Nombre == "TechSupplier" && p.Pais == "USA");
        result.Proveedores.Should().Contain(p => p.Nombre == "AsiaComponents" && p.Pais == "China");
    }

    #endregion
}
