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

    // Entidades raíz (colecciones principales)
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();

    // Entidad subcollection (necesaria para que EF Core la reconozca)
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de Producto
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Precio);
        });

        // Configuración de Cliente con subcollection de Pedidos
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Email).IsRequired();

            // Configurar Pedidos como subcollection
            entity.SubCollection(c => c.Pedidos);
        });

        // Configuración de Pedido
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroOrden).IsRequired();
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
