using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Firestore.EntityFrameworkCore.Metadata.Builders;
using Firestore.EntityFrameworkCore.Extensions;

namespace Fudie.Firestore.IntegrationTest.References;

/// <summary>
/// Tests de integración para verificar que las References se serializan
/// correctamente como DocumentReference en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ReferenceSerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public ReferenceSerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// CICLO 1 TDD: Verificar que Reference en Collection Principal
    /// se serializa como DocumentReference en Firestore.
    /// </summary>
    [Fact]
    public async Task Serialization_Reference_InCollection_ShouldStoreAsDocumentReference()
    {
        // Arrange
        var categoriaId = FirestoreTestFixture.GenerateId("cat");
        var articuloId = FirestoreTestFixture.GenerateId("art");

        using var context = _fixture.CreateContext<ReferenceTestDbContext>();

        var categoria = new Categoria
        {
            Id = categoriaId,
            Nombre = "Electrónica"
        };

        var articulo = new Articulo
        {
            Id = articuloId,
            Nombre = "Laptop",
            Categoria = categoria
        };

        // Act - Guardar usando EF Core
        context.Categorias.Add(categoria);
        context.Articulos.Add(articulo);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore para verificar el tipo
        // Nota: FirestoreCollectionManager pluraliza "Articulo" -> "Articulos"
        var firestoreDb = await GetFirestoreDbAsync();
        var docSnapshot = await firestoreDb
            .Collection("Articulos")
            .Document(articuloId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento debe existir en Firestore");

        // El campo Categoria debe ser DocumentReference, NO string
        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("Categoria", "Debe existir el campo de referencia");

        var categoriaValue = rawData["Categoria"];
        categoriaValue.Should().BeOfType<DocumentReference>(
            "El campo debe ser DocumentReference, no string/ID");

        var docRef = (DocumentReference)categoriaValue;
        docRef.Path.Should().EndWith($"Categorias/{categoriaId}",
            "El path debe apuntar al documento correcto");
        docRef.Id.Should().Be(categoriaId, "El ID del documento referenciado debe coincidir");
    }

    /// <summary>
    /// CICLO 3 TDD: Verificar que Reference en ComplexType
    /// se serializa como DocumentReference en Firestore.
    /// La Direccion (ComplexType) tiene referencia a Sucursal (entity)
    /// </summary>
    [Fact]
    public async Task Serialization_Reference_InComplexType_ShouldStoreAsDocumentReference()
    {
        // Arrange
        var sucursalId = FirestoreTestFixture.GenerateId("suc");
        var empresaId = FirestoreTestFixture.GenerateId("emp");

        using var context = _fixture.CreateContext<ComplexTypeReferenceTestDbContext>();

        var sucursal = new Sucursal
        {
            Id = sucursalId,
            Nombre = "Sucursal Centro"
        };

        var empresa = new Empresa
        {
            Id = empresaId,
            Nombre = "ACME Corp",
            DireccionPrincipal = new DireccionConRef
            {
                Calle = "Av. Principal 123",
                Ciudad = "Ciudad Central",
                SucursalCercana = sucursal
            }
        };

        // Act - Guardar usando EF Core
        context.Sucursales.Add(sucursal);
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore para verificar el tipo
        var firestoreDb = await GetFirestoreDbAsync();
        var docSnapshot = await firestoreDb
            .Collection("Empresas")
            .Document(empresaId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento de empresa debe existir en Firestore");

        // El campo DireccionPrincipal debe existir como map
        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("DireccionPrincipal", "Debe existir el ComplexType");

        var direccion = rawData["DireccionPrincipal"] as Dictionary<string, object>;
        direccion.Should().NotBeNull("DireccionPrincipal debe ser un map/object");

        // Dentro del ComplexType, el campo SucursalCercana debe ser DocumentReference
        direccion.Should().ContainKey("SucursalCercana", "Debe existir el campo de referencia en el ComplexType");

        var sucursalValue = direccion!["SucursalCercana"];
        sucursalValue.Should().BeOfType<DocumentReference>(
            "El campo debe ser DocumentReference, no string/ID");

        var docRef = (DocumentReference)sucursalValue;
        docRef.Path.Should().EndWith($"Sucursals/{sucursalId}",
            "El path debe apuntar al documento de sucursal");
        docRef.Id.Should().Be(sucursalId, "El ID de la sucursal referenciada debe coincidir");
    }

    /// <summary>
    /// CICLO 2 TDD: Verificar que Reference en SubCollection
    /// se serializa como DocumentReference en Firestore.
    /// Path: /clientes/{clienteId}/pedidos/{pedidoId}/lineas/{lineaId}
    /// La linea tiene referencia a Producto (collection raíz)
    /// </summary>
    [Fact]
    public async Task Serialization_Reference_InSubCollection_ShouldStoreAsDocumentReference()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedidoId = FirestoreTestFixture.GenerateId("ped");
        var lineaId = FirestoreTestFixture.GenerateId("lin");

        using var context = _fixture.CreateContext<SubCollectionReferenceTestDbContext>();

        var producto = new ProductoRef
        {
            Id = productoId,
            Nombre = "Laptop HP"
        };

        var cliente = new ClienteRef
        {
            Id = clienteId,
            Nombre = "Juan Pérez",
            Pedidos =
            [
                new PedidoRef
                {
                    Id = pedidoId,
                    NumeroOrden = "ORD-001",
                    Lineas =
                    [
                        new LineaPedidoRef
                        {
                            Id = lineaId,
                            Cantidad = 2,
                            Producto = producto
                        }
                    ]
                }
            ]
        };

        // Act - Guardar usando EF Core
        context.Productos.Add(producto);
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore para verificar el tipo
        var firestoreDb = await GetFirestoreDbAsync();
        var docSnapshot = await firestoreDb
            .Collection("ClienteRefs")
            .Document(clienteId)
            .Collection("PedidoRefs")
            .Document(pedidoId)
            .Collection("LineaPedidoRefs")
            .Document(lineaId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento de línea debe existir en Firestore");

        // El campo Producto debe ser DocumentReference, NO string
        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("Producto", "Debe existir el campo de referencia al producto");

        var productoValue = rawData["Producto"];
        productoValue.Should().BeOfType<DocumentReference>(
            "El campo debe ser DocumentReference, no string/ID");

        var docRef = (DocumentReference)productoValue;
        docRef.Path.Should().EndWith($"ProductoRefs/{productoId}",
            "El path debe apuntar al documento del producto");
        docRef.Id.Should().Be(productoId, "El ID del producto referenciado debe coincidir");
    }

    /// <summary>
    /// CICLO 5 TDD: Verificar que al leer una entidad con Reference
    /// USANDO Include, la propiedad de navegación se carga.
    /// </summary>
    [Fact]
    public async Task Deserialization_Reference_WithInclude_ShouldLoadReferencedEntity()
    {
        // Arrange - Crear y guardar entidades
        var categoriaId = FirestoreTestFixture.GenerateId("cat");
        var articuloId = FirestoreTestFixture.GenerateId("art");

        using (var setupContext = _fixture.CreateContext<ReferenceTestDbContext>())
        {
            var categoria = new Categoria
            {
                Id = categoriaId,
                Nombre = "Electrónica"
            };

            var articulo = new Articulo
            {
                Id = articuloId,
                Nombre = "Laptop",
                Categoria = categoria
            };

            setupContext.Categorias.Add(categoria);
            setupContext.Articulos.Add(articulo);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el artículo CON Include
        using var readContext = _fixture.CreateContext<ReferenceTestDbContext>();
        var articuloLeido = await readContext.Articulos
            .Include(a => a.Categoria)
            .FirstOrDefaultAsync(a => a.Id == articuloId);

        // Assert - La referencia debe estar cargada
        articuloLeido.Should().NotBeNull("El artículo debe existir");
        articuloLeido!.Nombre.Should().Be("Laptop");
        articuloLeido.Categoria.Should().NotBeNull(
            "Con Include, la propiedad de navegación debe estar cargada");
        articuloLeido.Categoria!.Id.Should().Be(categoriaId);
        articuloLeido.Categoria.Nombre.Should().Be("Electrónica");
    }

    /// <summary>
    /// CICLO 4 TDD: Verificar que al leer una entidad con Reference
    /// SIN usar Include, la propiedad de navegación es null.
    /// </summary>
    [Fact]
    public async Task Deserialization_Reference_WithoutInclude_ShouldBeNull()
    {
        // Arrange - Crear y guardar entidades
        var categoriaId = FirestoreTestFixture.GenerateId("cat");
        var articuloId = FirestoreTestFixture.GenerateId("art");

        using (var setupContext = _fixture.CreateContext<ReferenceTestDbContext>())
        {
            var categoria = new Categoria
            {
                Id = categoriaId,
                Nombre = "Electrónica"
            };

            var articulo = new Articulo
            {
                Id = articuloId,
                Nombre = "Laptop",
                Categoria = categoria
            };

            setupContext.Categorias.Add(categoria);
            setupContext.Articulos.Add(articulo);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el artículo SIN Include
        using var readContext = _fixture.CreateContext<ReferenceTestDbContext>();
        var articuloLeido = await readContext.Articulos.FindAsync(articuloId);

        // Assert - La referencia debe ser null (no cargada)
        articuloLeido.Should().NotBeNull("El artículo debe existir");
        articuloLeido!.Nombre.Should().Be("Laptop");
        articuloLeido.Categoria.Should().BeNull(
            "Sin Include, la propiedad de navegación debe ser null");
    }

    /// <summary>
    /// CICLO 6 TDD: Verificar que al leer una entidad con Reference en ComplexType
    /// USANDO Include, la propiedad de navegación se carga.
    /// </summary>
    [Fact]
    public async Task Include_Reference_InComplexType_ShouldLoadReferencedEntity()
    {
        // Arrange - Crear y guardar entidades
        var sucursalId = FirestoreTestFixture.GenerateId("suc");
        var empresaId = FirestoreTestFixture.GenerateId("emp");

        using (var setupContext = _fixture.CreateContext<ComplexTypeReferenceTestDbContext>())
        {
            var sucursal = new Sucursal
            {
                Id = sucursalId,
                Nombre = "Sucursal Centro"
            };

            var empresa = new Empresa
            {
                Id = empresaId,
                Nombre = "ACME Corp",
                DireccionPrincipal = new DireccionConRef
                {
                    Calle = "Av. Principal 123",
                    Ciudad = "Ciudad Central",
                    SucursalCercana = sucursal
                }
            };

            setupContext.Sucursales.Add(sucursal);
            setupContext.Empresas.Add(empresa);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer la empresa CON Include del Reference en ComplexType
        using var readContext = _fixture.CreateContext<ComplexTypeReferenceTestDbContext>();
        var empresaLeida = await readContext.Empresas
            .Include(e => e.DireccionPrincipal.SucursalCercana)
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        // Assert - La referencia dentro del ComplexType debe estar cargada
        empresaLeida.Should().NotBeNull("La empresa debe existir");
        empresaLeida!.Nombre.Should().Be("ACME Corp");
        empresaLeida.DireccionPrincipal.Should().NotBeNull("El ComplexType debe existir");
        empresaLeida.DireccionPrincipal.SucursalCercana.Should().NotBeNull(
            "Con Include, la referencia en el ComplexType debe estar cargada");
        empresaLeida.DireccionPrincipal.SucursalCercana!.Id.Should().Be(sucursalId);
        empresaLeida.DireccionPrincipal.SucursalCercana.Nombre.Should().Be("Sucursal Centro");
    }

    /// <summary>
    /// CICLO 7.1 TDD: Verificar que Lazy Loading carga automáticamente
    /// una Reference cuando se accede a la propiedad (sin Include).
    /// </summary>
    [Fact]
    public async Task LazyLoading_Reference_ShouldLoadWhenAccessed()
    {
        // Arrange - Crear y guardar entidades
        var categoriaId = FirestoreTestFixture.GenerateId("cat");
        var articuloId = FirestoreTestFixture.GenerateId("art");

        using (var setupContext = _fixture.CreateContextWithLazyLoading<LazyLoadingTestDbContext>())
        {
            var categoria = new CategoriaLazy
            {
                Id = categoriaId,
                Nombre = "Electrónica"
            };

            var articulo = new ArticuloLazy
            {
                Id = articuloId,
                Nombre = "Laptop",
                Categoria = categoria
            };

            setupContext.Categorias.Add(categoria);
            setupContext.Articulos.Add(articulo);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el artículo SIN Include, pero con lazy loading habilitado
        using var readContext = _fixture.CreateContextWithLazyLoading<LazyLoadingTestDbContext>();
        var articuloLeido = await readContext.Articulos
            .AsTracking()  // Lazy loading requiere tracking
            .FirstOrDefaultAsync(a => a.Id == articuloId);

        // Verificar que se creó un proxy
        var actualType = articuloLeido!.GetType();
        actualType.BaseType.Should().Be(typeof(ArticuloLazy), "Entity should be a Castle proxy");

        // Acceder a la propiedad dispara lazy loading automáticamente
        var categoriaLeida = articuloLeido!.Categoria;

        // Assert - La referencia debe haberse cargado automáticamente
        categoriaLeida.Should().NotBeNull("Lazy loading debe cargar la referencia al acceder");
        categoriaLeida!.Id.Should().Be(categoriaId);
        categoriaLeida.Nombre.Should().Be("Electrónica");
    }

    [Fact]
    public async Task ExplicitLoading_Reference_ShouldLoadWhenRequested()
    {
        // Arrange - Crear y guardar entidades
        var categoriaId = FirestoreTestFixture.GenerateId("cat");
        var articuloId = FirestoreTestFixture.GenerateId("art");

        using (var setupContext = _fixture.CreateContext<LazyLoadingTestDbContext>())
        {
            var categoria = new CategoriaLazy
            {
                Id = categoriaId,
                Nombre = "Electrónica"
            };

            var articulo = new ArticuloLazy
            {
                Id = articuloId,
                Nombre = "Laptop",
                Categoria = categoria
            };

            setupContext.Categorias.Add(categoria);
            setupContext.Articulos.Add(articulo);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el artículo CON TRACKING habilitado
        using var readContext = _fixture.CreateContext<LazyLoadingTestDbContext>();
        var articuloLeido = await readContext.Articulos
            .AsTracking()  // <-- Forzar tracking explícitamente
            .FirstOrDefaultAsync(a => a.Id == articuloId);

        // Verificar que está siendo tracked
        var entry = readContext.Entry(articuloLeido!);
        entry.State.Should().NotBe(EntityState.Detached, "La entidad debe estar tracked");

        // Explicit Loading
        await entry.Reference<CategoriaLazy>(nameof(ArticuloLazy.Categoria)).LoadAsync();

        // Assert
        var categoriaLeida = articuloLeido!.Categoria;
        categoriaLeida.Should().NotBeNull("Explicit loading debe cargar la referencia");
        categoriaLeida!.Id.Should().Be(categoriaId);
        categoriaLeida.Nombre.Should().Be("Electrónica");
    }

    /// <summary>
    /// CICLO 7.2: Limitación documentada - Lazy/Explicit Loading para SubCollections
    /// NO está soportado porque las SubCollections en Firestore son jerárquicas
    /// (path-based) y no FK-based como espera EF Core.
    ///
    /// SOLUCIÓN: Usar Include() para cargar SubCollections.
    ///
    /// RAZÓN TÉCNICA:
    /// - EF Core Lazy/Explicit Loading crea queries FK-based: WHERE ClienteId = @id
    /// - Firestore SubCollections están en paths: /Clientes/{id}/Pedidos/
    /// - No existe collection global "Pedidos" para consultar
    /// </summary>
    [Fact(Skip = "Limitación conocida: SubCollections requieren Include() - ver documentación")]
    public async Task ExplicitLoading_SubCollection_ShouldLoadWhenRequested()
    {
        // Arrange - Crear y guardar entidades
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedido1Id = FirestoreTestFixture.GenerateId("ped");

        using (var setupContext = _fixture.CreateContext<SubCollectionLazyLoadingTestDbContext>())
        {
            var cliente = new ClienteLazy
            {
                Id = clienteId,
                Nombre = "Cliente Explicit Test",
                Pedidos =
                [
                    new PedidoLazy
                    {
                        Id = pedido1Id,
                        NumeroOrden = "ORD-EXPL-001",
                        Total = 150.00m
                    }
                ]
            };

            setupContext.Clientes.Add(cliente);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el cliente CON TRACKING habilitado
        using var readContext = _fixture.CreateContext<SubCollectionLazyLoadingTestDbContext>();
        var clienteLeido = await readContext.Clientes
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Explicit Loading
        var entry = readContext.Entry(clienteLeido!);
        entry.State.Should().NotBe(EntityState.Detached, "La entidad debe estar tracked");
        await entry.Collection(c => c.Pedidos).LoadAsync();

        // Assert
        var pedidos = clienteLeido!.Pedidos;
        pedidos.Should().NotBeNull("Explicit loading debe cargar la subcollection");
        pedidos.Should().HaveCount(1);
        pedidos.First().NumeroOrden.Should().Be("ORD-EXPL-001");
    }

    /// <summary>
    /// CICLO 7.2: Limitación documentada - Lazy Loading para SubCollections
    /// NO está soportado por la misma razón que Explicit Loading.
    ///
    /// SOLUCIÓN: Usar Include() para cargar SubCollections.
    /// </summary>
    [Fact(Skip = "Limitación conocida: SubCollections requieren Include() - ver documentación")]
    public async Task LazyLoading_SubCollection_ShouldLoadWhenAccessed()
    {
        // Arrange - Crear y guardar entidades
        var clienteId = FirestoreTestFixture.GenerateId("cli");
        var pedido1Id = FirestoreTestFixture.GenerateId("ped");
        var pedido2Id = FirestoreTestFixture.GenerateId("ped");

        using (var setupContext = _fixture.CreateContext<SubCollectionLazyLoadingTestDbContext>())
        {
            var cliente = new ClienteLazy
            {
                Id = clienteId,
                Nombre = "Cliente Lazy Test",
                Pedidos =
                [
                    new PedidoLazy
                    {
                        Id = pedido1Id,
                        NumeroOrden = "ORD-LAZY-001",
                        Total = 100.00m
                    },
                    new PedidoLazy
                    {
                        Id = pedido2Id,
                        NumeroOrden = "ORD-LAZY-002",
                        Total = 200.00m
                    }
                ]
            };

            setupContext.Clientes.Add(cliente);
            await setupContext.SaveChangesAsync();
        }

        // Act - Leer el cliente SIN Include, pero con lazy loading habilitado
        using var readContext = _fixture.CreateContextWithLazyLoading<SubCollectionLazyLoadingTestDbContext>();
        var clienteLeido = await readContext.Clientes
            .AsTracking()  // Lazy loading requiere tracking
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        // Verificar que se creó un proxy
        var actualType = clienteLeido!.GetType();
        actualType.BaseType.Should().Be(typeof(ClienteLazy), "Entity should be a Castle proxy");

        // Acceder a la propiedad dispara lazy loading automáticamente
        var pedidos = clienteLeido!.Pedidos;

        // Assert - La subcollection debe haberse cargado automáticamente
        pedidos.Should().NotBeNull("Lazy loading debe cargar la subcollection al acceder");
        pedidos.Should().HaveCount(2);
        pedidos.Should().Contain(p => p.NumeroOrden == "ORD-LAZY-001");
        pedidos.Should().Contain(p => p.NumeroOrden == "ORD-LAZY-002");
    }

    private async Task<FirestoreDb> GetFirestoreDbAsync()
    {
        return await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();
    }
}

// ============================================================================
// ENTIDADES PARA TEST DE REFERENCE EN SUBCOLLECTION
// ============================================================================

public class ProductoRef
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class ClienteRef
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<PedidoRef> Pedidos { get; set; } = [];
}

public class PedidoRef
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public List<LineaPedidoRef> Lineas { get; set; } = [];
}

public class LineaPedidoRef
{
    public string? Id { get; set; }
    public int Cantidad { get; set; }

    // Reference a ProductoRef (collection raíz)
    public ProductoRef? Producto { get; set; }
}

// ============================================================================
// DBCONTEXT PARA TEST DE REFERENCE EN SUBCOLLECTION
// ============================================================================

public class SubCollectionReferenceTestDbContext : DbContext
{
    public SubCollectionReferenceTestDbContext(DbContextOptions<SubCollectionReferenceTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductoRef> Productos => Set<ProductoRef>();
    public DbSet<ClienteRef> Clientes => Set<ClienteRef>();
    public DbSet<PedidoRef> Pedidos => Set<PedidoRef>();
    public DbSet<LineaPedidoRef> LineasPedido => Set<LineaPedidoRef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductoRef>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<ClienteRef>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // SubCollection: Pedidos con Lineas anidadas
            entity.SubCollection(c => c.Pedidos)
                  .SubCollection(p => p.Lineas);
        });

        modelBuilder.Entity<PedidoRef>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroOrden).IsRequired();
        });

        modelBuilder.Entity<LineaPedidoRef>(entity =>
        {
            entity.HasKey(e => e.Id);

            // ✅ Reference a Producto desde SubCollection
            entity.Reference(l => l.Producto);
        });
    }
}

// ============================================================================
// ENTIDADES PARA TEST DE REFERENCE EN COMPLEXTYPE
// ============================================================================

public class Sucursal
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class DireccionConRef
{
    public required string Calle { get; set; }
    public required string Ciudad { get; set; }

    // Reference a Sucursal desde ComplexType
    public Sucursal? SucursalCercana { get; set; }
}

public class Empresa
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required DireccionConRef DireccionPrincipal { get; set; }
}

// ============================================================================
// DBCONTEXT PARA TEST DE REFERENCE EN COMPLEXTYPE
// ============================================================================

public class ComplexTypeReferenceTestDbContext : DbContext
{
    public ComplexTypeReferenceTestDbContext(DbContextOptions<ComplexTypeReferenceTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Empresa> Empresas => Set<Empresa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sucursal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ ComplexType con Reference a Sucursal
            entity.ComplexProperty(e => e.DireccionPrincipal, direccion =>
            {
                direccion.Reference(d => d.SucursalCercana);
            });
        });
    }
}

// ============================================================================
// ENTIDADES DE TEST PARA REFERENCES EN COLLECTION
// ============================================================================

public class Categoria
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class Articulo
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    // Reference a Categoria (será DocumentReference en Firestore)
    public Categoria? Categoria { get; set; }
}

// ============================================================================
// DBCONTEXT DE TEST PARA REFERENCES
// ============================================================================

public class ReferenceTestDbContext : DbContext
{
    public ReferenceTestDbContext(DbContextOptions<ReferenceTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Articulo> Articulos => Set<Articulo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<Articulo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ Configurar Reference (debe serializar como DocumentReference)
            entity.Reference(a => a.Categoria);
        });
    }
}

// ============================================================================
// ENTIDADES PARA TEST DE LAZY LOADING
// ============================================================================

public class CategoriaLazy
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class ArticuloLazy
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    // Reference con virtual para lazy loading
    public virtual CategoriaLazy? Categoria { get; set; }
}

// ============================================================================
// DBCONTEXT PARA TEST DE LAZY LOADING
// ============================================================================

public class LazyLoadingTestDbContext : DbContext
{
    public LazyLoadingTestDbContext(DbContextOptions<LazyLoadingTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<CategoriaLazy> Categorias => Set<CategoriaLazy>();
    public DbSet<ArticuloLazy> Articulos => Set<ArticuloLazy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoriaLazy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<ArticuloLazy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ Configurar Reference para lazy loading
            entity.Reference(a => a.Categoria);
        });
    }
}

// ============================================================================
// ENTIDADES PARA TEST DE LAZY LOADING EN SUBCOLLECTIONS
// ============================================================================

public class ClienteLazy
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    // SubCollection con virtual para lazy loading
    public virtual List<PedidoLazy> Pedidos { get; set; } = [];
}

public class PedidoLazy
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public decimal Total { get; set; }
}

// ============================================================================
// DBCONTEXT PARA TEST DE LAZY LOADING EN SUBCOLLECTIONS
// ============================================================================

public class SubCollectionLazyLoadingTestDbContext : DbContext
{
    public SubCollectionLazyLoadingTestDbContext(DbContextOptions<SubCollectionLazyLoadingTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<ClienteLazy> Clientes => Set<ClienteLazy>();
    public DbSet<PedidoLazy> Pedidos => Set<PedidoLazy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClienteLazy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ Configurar SubCollection para lazy loading
            entity.SubCollection(c => c.Pedidos);
        });

        modelBuilder.Entity<PedidoLazy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroOrden).IsRequired();
        });
    }
}
