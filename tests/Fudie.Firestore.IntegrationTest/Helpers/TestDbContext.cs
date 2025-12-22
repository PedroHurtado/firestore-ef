using Firestore.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.IntegrationTest.Helpers;

/// <summary>
/// DbContext de prueba para tests de integración.
/// Demuestra la configuración típica de un desarrollador.
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    // Solo agregados raíz (SubCollections se auto-registran)
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ProductoCompleto> ProductosCompletos => Set<ProductoCompleto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de Producto
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Precio);
        });

        // Configuración de Cliente con subcollections anidadas
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Email).IsRequired();

            // Configurar Pedidos como subcollection con Lineas anidadas
            entity.SubCollection(c => c.Pedidos)
                  .SubCollection(p => p.Lineas);
        });

        // Configuración de ProductoCompleto (para tests de conventions)
        modelBuilder.Entity<ProductoCompleto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ComplexType: Ubicacion (GeoPoint directo)
            entity.ComplexProperty(e => e.Ubicacion);

            // ComplexType: Direccion con Coordenadas anidadas
            entity.ComplexProperty(e => e.Direccion, direccion =>
            {
                direccion.ComplexProperty(d => d.Coordenadas, coordenadas =>
                {
                    coordenadas.ComplexProperty(c => c.Posicion);  // GeoPoint anidado
                });
            });
        });
    }
}

/// <summary>
/// DbContext simple solo con Productos para tests básicos de CRUD.
/// </summary>
public class SimpleTestDbContext : DbContext
{
    public SimpleTestDbContext(DbContextOptions<SimpleTestDbContext> options) : base(options)
    {
    }

    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });
    }
}

/// <summary>
/// DbContext for Query tests (Where, OrderBy, etc.).
/// </summary>
public class QueryTestDbContext : DbContext
{
    public QueryTestDbContext(DbContextOptions<QueryTestDbContext> options) : base(options)
    {
    }

    public DbSet<QueryTestEntity> QueryTestEntities => Set<QueryTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueryTestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}

/// <summary>
/// DbContext para tests de constructores con parámetros (Ciclo 3).
/// </summary>
public class ConstructorTestDbContext : DbContext
{
    public ConstructorTestDbContext(DbContextOptions<ConstructorTestDbContext> options) : base(options)
    {
    }

    public DbSet<EntityWithFullConstructor> EntitiesWithFullConstructor => Set<EntityWithFullConstructor>();
    public DbSet<EntityWithPartialConstructor> EntitiesWithPartialConstructor => Set<EntityWithPartialConstructor>();
    public DbSet<EntityRecord> EntityRecords => Set<EntityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithFullConstructor>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<EntityWithPartialConstructor>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<EntityRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}

/// <summary>
/// DbContext con Query Filter global para multi-tenancy.
/// Implementa el patrón de filtrado automático por TenantId usando HasQueryFilter de EF Core.
/// </summary>
public class TenantDbContext : DbContext
{
    private readonly string _tenantId;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, string tenantId) : base(options)
    {
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
    }

    public DbSet<TenantEntity> TenantEntities => Set<TenantEntity>();

    /// <summary>
    /// El TenantId actual configurado para este contexto.
    /// </summary>
    public string TenantId => _tenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.TenantId).IsRequired();

            // Query Filter global: todas las queries filtran automáticamente por TenantId
            entity.HasQueryFilter(e => e.TenantId == _tenantId);
        });
    }
}
