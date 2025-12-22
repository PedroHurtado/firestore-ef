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

    #region Ciclo 3: Constructor con parámetros

    /// <summary>
    /// Caso 1: Constructor con parámetros que cubren TODAS las propiedades.
    /// El deserializador debe usar el constructor en lugar de new() + setters.
    /// </summary>
    [Fact]
    public async Task DeserializeEntity_WithFullParameterizedConstructor_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<ConstructorTestDbContext>();
        var entity = new EntityWithFullConstructor("full-ctor-1", "Test Full Constructor", 150.50m);

        context.EntitiesWithFullConstructor.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ConstructorTestDbContext>();
        var entityLeido = await readContext.EntitiesWithFullConstructor.FindAsync("full-ctor-1");

        // Assert
        Assert.NotNull(entityLeido);
        Assert.Equal("full-ctor-1", entityLeido.Id);
        Assert.Equal("Test Full Constructor", entityLeido.Nombre);
        Assert.Equal(150.50m, entityLeido.Precio);
    }

    /// <summary>
    /// Caso 2: Constructor con parámetros que cubren SOLO ALGUNAS propiedades.
    /// Las propiedades restantes se setean después vía property setters.
    /// </summary>
    [Fact]
    public async Task DeserializeEntity_WithPartialParameterizedConstructor_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<ConstructorTestDbContext>();
        // Constructor solo recibe (id, nombre), pero Precio y Activo se setean después
        var entity = new EntityWithPartialConstructor("partial-ctor-1", "Test Partial Constructor")
        {
            Precio = 200.75m,
            Activo = true
        };

        context.EntitiesWithPartialConstructor.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ConstructorTestDbContext>();
        var entityLeido = await readContext.EntitiesWithPartialConstructor.FindAsync("partial-ctor-1");

        // Assert
        Assert.NotNull(entityLeido);
        Assert.Equal("partial-ctor-1", entityLeido.Id);
        Assert.Equal("Test Partial Constructor", entityLeido.Nombre);
        Assert.Equal(200.75m, entityLeido.Precio);  // Seteado vía property, no constructor
        Assert.True(entityLeido.Activo);            // Seteado vía property, no constructor
    }

    /// <summary>
    /// Caso 3: Constructor con parámetros para múltiples entidades (ToListAsync).
    /// Verifica que la deserialización de listas también funciona con constructores.
    /// </summary>
    [Fact]
    public async Task DeserializeEntities_WithParameterizedConstructor_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<ConstructorTestDbContext>();
        var uniqueTag = $"ctor-list-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new EntityWithFullConstructor($"full-ctor-list-1-{uniqueTag}", uniqueTag, 10.00m),
            new EntityWithFullConstructor($"full-ctor-list-2-{uniqueTag}", uniqueTag, 20.00m),
            new EntityWithFullConstructor($"full-ctor-list-3-{uniqueTag}", uniqueTag, 30.00m)
        };

        context.EntitiesWithFullConstructor.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ConstructorTestDbContext>();
        var entitiesLeidos = await readContext.EntitiesWithFullConstructor
            .Where(e => e.Nombre == uniqueTag)
            .ToListAsync();

        // Assert
        Assert.Equal(3, entitiesLeidos.Count);
        Assert.All(entitiesLeidos, e => Assert.Equal(uniqueTag, e.Nombre));
    }

    /// <summary>
    /// Caso 4: Record (immutable) - Solo constructor, sin setters.
    /// Los records son un caso especial porque no tienen setters públicos.
    /// </summary>
    [Fact]
    public async Task DeserializeEntity_WithRecord_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<ConstructorTestDbContext>();
        var entity = new EntityRecord("record-1", "Test Record", 300.00m);

        context.EntityRecords.Add(entity);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ConstructorTestDbContext>();
        var entityLeido = await readContext.EntityRecords.FindAsync("record-1");

        // Assert
        Assert.NotNull(entityLeido);
        Assert.Equal("record-1", entityLeido.Id);
        Assert.Equal("Test Record", entityLeido.Nombre);
        Assert.Equal(300.00m, entityLeido.Precio);
    }

    #endregion
}
