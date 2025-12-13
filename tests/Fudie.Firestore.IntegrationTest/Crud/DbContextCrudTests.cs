using Fudie.Firestore.IntegrationTest.Helpers;

namespace Fudie.Firestore.IntegrationTest.Crud;

/// <summary>
/// Tests de integración para operaciones CRUD básicas usando DbContext.
/// Demuestra el flujo típico de un desarrollador.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class DbContextCrudTests
{
    private readonly FirestoreTestFixture _fixture;

    public DbContextCrudTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_SingleEntity_ShouldPersistToFirestore()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var producto = new Producto
        {
            Id = FirestoreTestFixture.GenerateId("prod"),
            Nombre = "Laptop HP",
            Precio = 1299.99m,
            Activo = true
        };

        // Act
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Assert - Verificar que se guardó leyendo de nuevo
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoLeido = await readContext.Productos.FindAsync(producto.Id);

        productoLeido.Should().NotBeNull();
        productoLeido!.Nombre.Should().Be("Laptop HP");
        productoLeido.Precio.Should().Be(1299.99m);
        productoLeido.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Add_MultipleEntities_ShouldPersistAllToFirestore()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var productos = new[]
        {
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = "Mouse", Precio = 29.99m },
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = "Teclado", Precio = 79.99m },
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = "Monitor", Precio = 299.99m }
        };

        // Act
        context.Productos.AddRange(productos);
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        foreach (var producto in productos)
        {
            var leido = await readContext.Productos.FindAsync(producto.Id);
            leido.Should().NotBeNull();
            leido!.Nombre.Should().Be(producto.Nombre);
        }
    }

    [Fact]
    public async Task Query_WithWhere_ShouldFilterResults()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var uniqueTag = Guid.NewGuid().ToString("N");
        var productos = new[]
        {
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = uniqueTag, Precio = 100m, Activo = true },
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = uniqueTag, Precio = 200m, Activo = true },
            new Producto { Id = FirestoreTestFixture.GenerateId("prod"), Nombre = "otro", Precio = 300m, Activo = false }
        };

        context.Productos.AddRange(productos);
        await context.SaveChangesAsync();

        // Act - Usar solo una condición (equality soportada por Firestore)
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productosConTag = await readContext.Productos
            .Where(p => p.Nombre == uniqueTag)
            .ToListAsync();

        // Assert
        productosConTag.Should().HaveCount(2);
        productosConTag.Should().AllSatisfy(p => p.Nombre.Should().Be(uniqueTag));
    }

    [Fact]
    public async Task Update_ExistingEntity_ShouldPersistChanges()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var producto = new Producto
        {
            Id = FirestoreTestFixture.GenerateId("prod"),
            Nombre = "Producto Original",
            Precio = 100m
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act - Leer, modificar y guardar
        using var updateContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoParaActualizar = await updateContext.Productos.FindAsync(producto.Id);

        // WORKAROUND: El provider aún no trackea automáticamente las entidades leídas.
        // Las entidades se devuelven en estado Detached en lugar de Unchanged.
        // TODO: Remover Attach() cuando se implemente tracking automático (ver plan 2025-12-13-tracking-fix.md)
        updateContext.Attach(productoParaActualizar!);

        productoParaActualizar!.Nombre = "Producto Modificado";
        productoParaActualizar.Precio = 150m;

        await updateContext.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoActualizado = await readContext.Productos.FindAsync(producto.Id);

        productoActualizado.Should().NotBeNull();
        productoActualizado!.Nombre.Should().Be("Producto Modificado");
        productoActualizado.Precio.Should().Be(150m);
    }

    [Fact]
    public async Task Delete_ExistingEntity_ShouldRemoveFromFirestore()
    {
        // Arrange
        using var context = _fixture.CreateContext<SimpleTestDbContext>();
        var producto = new Producto
        {
            Id = FirestoreTestFixture.GenerateId("prod"),
            Nombre = "Producto a Eliminar",
            Precio = 50m
        };

        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Act
        using var deleteContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoParaEliminar = await deleteContext.Productos.FindAsync(producto.Id);
        deleteContext.Productos.Remove(productoParaEliminar!);
        await deleteContext.SaveChangesAsync();

        // Assert
        using var readContext = _fixture.CreateContext<SimpleTestDbContext>();
        var productoEliminado = await readContext.Productos.FindAsync(producto.Id);

        productoEliminado.Should().BeNull();
    }
}
