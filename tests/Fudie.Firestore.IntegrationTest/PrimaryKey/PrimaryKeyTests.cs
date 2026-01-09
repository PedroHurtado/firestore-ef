using FluentAssertions;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.PrimaryKey;

namespace Fudie.Firestore.IntegrationTest.PrimaryKey;

/// <summary>
/// Integration tests for different primary key configurations.
/// Validates that explicit PKs, convention PKs, and Guid PKs work correctly.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class PrimaryKeyTests
{
    private readonly FirestoreTestFixture _fixture;

    public PrimaryKeyTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Explicit PK Tests

    [Fact]
    public async Task ExplicitPK_Add_ShouldUseCodigoAsDocumentId()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigo = FirestoreTestFixture.GenerateId("prod");
        var producto = new ProductoConCodigo
        {
            Codigo = codigo,
            Nombre = "Producto Test",
            Precio = 99.99m
        };

        // Act
        context.ProductosConCodigo.Add(producto);
        await context.SaveChangesAsync();

        // Assert - Query by explicit PK
        var retrieved = await context.ProductosConCodigo
            .Where(p => p.Codigo == codigo)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.Codigo.Should().Be(codigo);
        retrieved.Nombre.Should().Be("Producto Test");
        retrieved.Precio.Should().Be(99.99m);
    }

    [Fact]
    public async Task ExplicitPK_FindAsync_ShouldWorkWithCodigo()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigo = FirestoreTestFixture.GenerateId("find");
        var producto = new ProductoConCodigo
        {
            Codigo = codigo,
            Nombre = "Producto FindAsync",
            Precio = 50.00m
        };

        context.ProductosConCodigo.Add(producto);
        await context.SaveChangesAsync();

        // Act - Use FindAsync with explicit PK
        var retrieved = await context.ProductosConCodigo.FindAsync(codigo);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Codigo.Should().Be(codigo);
        retrieved.Nombre.Should().Be("Producto FindAsync");
    }

    [Fact]
    public async Task ExplicitPK_Update_ShouldWorkWithCodigo()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigo = FirestoreTestFixture.GenerateId("upd");
        var producto = new ProductoConCodigo
        {
            Codigo = codigo,
            Nombre = "Original",
            Precio = 10.00m
        };

        context.ProductosConCodigo.Add(producto);
        await context.SaveChangesAsync();

        // Act - Update
        producto.Nombre = "Actualizado";
        producto.Precio = 20.00m;
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var retrieved = await verifyContext.ProductosConCodigo.FindAsync(codigo);
        retrieved.Should().NotBeNull();
        retrieved!.Nombre.Should().Be("Actualizado");
        retrieved.Precio.Should().Be(20.00m);
    }

    [Fact]
    public async Task ExplicitPK_Delete_ShouldWorkWithCodigo()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigo = FirestoreTestFixture.GenerateId("del");
        var producto = new ProductoConCodigo
        {
            Codigo = codigo,
            Nombre = "Para Eliminar",
            Precio = 5.00m
        };

        context.ProductosConCodigo.Add(producto);
        await context.SaveChangesAsync();

        // Act - Delete
        context.ProductosConCodigo.Remove(producto);
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var retrieved = await verifyContext.ProductosConCodigo.FindAsync(codigo);
        retrieved.Should().BeNull();
    }

    #endregion

    #region Convention PK "Id" Tests

    [Fact]
    public async Task ConventionPK_Id_Add_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var id = FirestoreTestFixture.GenerateId("art");
        var articulo = new ArticuloConId
        {
            Id = id,
            Descripcion = "Articulo con Id convencional",
            Stock = 100
        };

        // Act
        context.ArticulosConId.Add(articulo);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.ArticulosConId
            .Where(a => a.Id == id)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
        retrieved.Descripcion.Should().Be("Articulo con Id convencional");
    }

    [Fact]
    public async Task ConventionPK_Id_FindAsync_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var id = FirestoreTestFixture.GenerateId("artfind");
        var articulo = new ArticuloConId
        {
            Id = id,
            Descripcion = "Articulo FindAsync",
            Stock = 50
        };

        context.ArticulosConId.Add(articulo);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.ArticulosConId.FindAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
    }

    #endregion

    #region Convention PK "{EntityName}Id" Tests

    [Fact]
    public async Task ConventionPK_EntityNameId_Add_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var id = FirestoreTestFixture.GenerateId("cat");
        var categoria = new CategoriaConEntityId
        {
            CategoriaConEntityIdId = id,
            Nombre = "Categoria con EntityNameId",
            Activa = true
        };

        // Act
        context.CategoriasConEntityId.Add(categoria);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.CategoriasConEntityId
            .Where(c => c.CategoriaConEntityIdId == id)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.CategoriaConEntityIdId.Should().Be(id);
        retrieved.Nombre.Should().Be("Categoria con EntityNameId");
    }

    [Fact]
    public async Task ConventionPK_EntityNameId_FindAsync_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var id = FirestoreTestFixture.GenerateId("catfind");
        var categoria = new CategoriaConEntityId
        {
            CategoriaConEntityIdId = id,
            Nombre = "Categoria FindAsync",
            Activa = false
        };

        context.CategoriasConEntityId.Add(categoria);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.CategoriasConEntityId.FindAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.CategoriaConEntityIdId.Should().Be(id);
    }

    #endregion

    #region Guid PK Tests

    [Fact]
    public async Task GuidPK_Add_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var ordenId = Guid.NewGuid();
        var orden = new OrdenConGuid
        {
            OrdenId = ordenId,
            Cliente = "Cliente Test",
            Total = 150.00m,
            FechaCreacion = DateTime.UtcNow
        };

        // Act
        context.OrdenesConGuid.Add(orden);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.OrdenesConGuid
            .Where(o => o.OrdenId == ordenId)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.OrdenId.Should().Be(ordenId);
        retrieved.Cliente.Should().Be("Cliente Test");
    }

    [Fact]
    public async Task GuidPK_FindAsync_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var ordenId = Guid.NewGuid();
        var orden = new OrdenConGuid
        {
            OrdenId = ordenId,
            Cliente = "Cliente FindAsync",
            Total = 200.00m,
            FechaCreacion = DateTime.UtcNow
        };

        context.OrdenesConGuid.Add(orden);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.OrdenesConGuid.FindAsync(ordenId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.OrdenId.Should().Be(ordenId);
        retrieved.Cliente.Should().Be("Cliente FindAsync");
    }

    [Fact]
    public async Task GuidPK_Update_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var ordenId = Guid.NewGuid();
        var orden = new OrdenConGuid
        {
            OrdenId = ordenId,
            Cliente = "Cliente Original",
            Total = 100.00m,
            FechaCreacion = DateTime.UtcNow
        };

        context.OrdenesConGuid.Add(orden);
        await context.SaveChangesAsync();

        // Act
        orden.Cliente = "Cliente Actualizado";
        orden.Total = 300.00m;
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var retrieved = await verifyContext.OrdenesConGuid.FindAsync(ordenId);
        retrieved.Should().NotBeNull();
        retrieved!.Cliente.Should().Be("Cliente Actualizado");
        retrieved.Total.Should().Be(300.00m);
    }

    #endregion

    #region Int PK Tests

    [Fact]
    public async Task IntPK_Add_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var numeroItem = new Random().Next(100000, 999999);
        var item = new ItemConNumero
        {
            NumeroItem = numeroItem,
            Descripcion = "Item con numero",
            Valor = 45.50m
        };

        // Act
        context.ItemsConNumero.Add(item);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.ItemsConNumero
            .Where(i => i.NumeroItem == numeroItem)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.NumeroItem.Should().Be(numeroItem);
        retrieved.Descripcion.Should().Be("Item con numero");
    }

    [Fact]
    public async Task IntPK_FindAsync_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var numeroItem = new Random().Next(100000, 999999);
        var item = new ItemConNumero
        {
            NumeroItem = numeroItem,
            Descripcion = "Item FindAsync",
            Valor = 33.33m
        };

        context.ItemsConNumero.Add(item);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.ItemsConNumero.FindAsync(numeroItem);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.NumeroItem.Should().Be(numeroItem);
        retrieved.Descripcion.Should().Be("Item FindAsync");
    }

    [Fact]
    public async Task IntPK_Update_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var numeroItem = new Random().Next(100000, 999999);
        var item = new ItemConNumero
        {
            NumeroItem = numeroItem,
            Descripcion = "Original",
            Valor = 10.00m
        };

        context.ItemsConNumero.Add(item);
        await context.SaveChangesAsync();

        // Act
        item.Descripcion = "Actualizado";
        item.Valor = 25.00m;
        await context.SaveChangesAsync();

        // Assert
        using var verifyContext = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var retrieved = await verifyContext.ItemsConNumero.FindAsync(numeroItem);
        retrieved.Should().NotBeNull();
        retrieved!.Descripcion.Should().Be("Actualizado");
        retrieved.Valor.Should().Be(25.00m);
    }

    #endregion

    #region Explicit PK with SubCollection Tests

    [Fact]
    public async Task ExplicitPK_WithSubCollection_Add_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigoProveedor = FirestoreTestFixture.GenerateId("prov");
        var proveedor = new ProveedorConCodigo
        {
            CodigoProveedor = codigoProveedor,
            RazonSocial = "Proveedor Test SA",
            Contactos = new List<ContactoProveedor>
            {
                new()
                {
                    ContactoId = FirestoreTestFixture.GenerateId("cont1"),
                    Nombre = "Juan Perez",
                    Email = "juan@test.com",
                    Telefono = "123456789"
                },
                new()
                {
                    ContactoId = FirestoreTestFixture.GenerateId("cont2"),
                    Nombre = "Maria Garcia",
                    Email = "maria@test.com",
                    Telefono = "987654321"
                }
            }
        };

        // Act
        context.ProveedoresConCodigo.Add(proveedor);
        await context.SaveChangesAsync();

        // Assert - Query parent by explicit PK
        var retrieved = await context.ProveedoresConCodigo
            .Where(p => p.CodigoProveedor == codigoProveedor)
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.CodigoProveedor.Should().Be(codigoProveedor);
        retrieved.RazonSocial.Should().Be("Proveedor Test SA");
    }

    [Fact]
    public async Task ExplicitPK_WithSubCollection_Include_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigoProveedor = FirestoreTestFixture.GenerateId("provinc");
        var proveedor = new ProveedorConCodigo
        {
            CodigoProveedor = codigoProveedor,
            RazonSocial = "Proveedor Include SA",
            Contactos = new List<ContactoProveedor>
            {
                new()
                {
                    ContactoId = FirestoreTestFixture.GenerateId("incont1"),
                    Nombre = "Carlos Lopez",
                    Email = "carlos@test.com",
                    Telefono = "111222333"
                }
            }
        };

        context.ProveedoresConCodigo.Add(proveedor);
        await context.SaveChangesAsync();

        // Act - Include subcollection
        var retrieved = await context.ProveedoresConCodigo
            .Include(p => p.Contactos)
            .Where(p => p.CodigoProveedor == codigoProveedor)
            .FirstOrDefaultAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.CodigoProveedor.Should().Be(codigoProveedor);
        retrieved.Contactos.Should().HaveCount(1);
        retrieved.Contactos[0].Nombre.Should().Be("Carlos Lopez");
    }

    [Fact]
    public async Task ExplicitPK_WithSubCollection_FilteredIncludeById_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var codigoProveedor = FirestoreTestFixture.GenerateId("provfilt");
        var contactoId1 = FirestoreTestFixture.GenerateId("filtcont1");
        var contactoId2 = FirestoreTestFixture.GenerateId("filtcont2");

        var proveedor = new ProveedorConCodigo
        {
            CodigoProveedor = codigoProveedor,
            RazonSocial = "Proveedor Filtered SA",
            Contactos = new List<ContactoProveedor>
            {
                new()
                {
                    ContactoId = contactoId1,
                    Nombre = "Contacto Uno",
                    Email = "uno@test.com",
                    Telefono = "111"
                },
                new()
                {
                    ContactoId = contactoId2,
                    Nombre = "Contacto Dos",
                    Email = "dos@test.com",
                    Telefono = "222"
                }
            }
        };
        context.ProveedoresConCodigo.Add(proveedor);
        await context.SaveChangesAsync();

        // Act - Use fresh context to avoid ChangeTracker returning cached entities
        using var queryContext = _fixture.CreateContext<PrimaryKeyTestDbContext>();
        var retrieved = await queryContext.ProveedoresConCodigo
            .Include(p => p.Contactos.Where(c => c.ContactoId == contactoId1))
            .Where(p => p.CodigoProveedor == codigoProveedor)
            .FirstOrDefaultAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Contactos.Should().HaveCount(1);
        retrieved.Contactos[0].ContactoId.Should().Be(contactoId1);
        retrieved.Contactos[0].Nombre.Should().Be("Contacto Uno");
    }

    #endregion
}
