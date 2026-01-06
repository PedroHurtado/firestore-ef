using Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

public class ArrayOfTestDbContext(DbContextOptions<ArrayOfTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConHorarios> Tiendas => Set<TiendaConHorarios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConHorarios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Horarios);
        });
    }
}

public class ArrayOfGeoPointTestDbContext(DbContextOptions<ArrayOfGeoPointTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConUbicaciones> Tiendas => Set<TiendaConUbicaciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConUbicaciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });
    }
}

public class ArrayOfReferencesTestDbContext(DbContextOptions<ArrayOfReferencesTestDbContext> options) : DbContext(options)
{
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<ProductoConEtiquetas> Productos => Set<ProductoConEtiquetas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Etiqueta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<ProductoConEtiquetas>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Etiquetas).AsReferences();
        });
    }
}
