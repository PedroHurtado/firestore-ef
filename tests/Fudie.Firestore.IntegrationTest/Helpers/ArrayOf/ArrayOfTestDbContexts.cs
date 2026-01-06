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

// ============================================================================
// DBCONTEXT PARA ARRAYOF ANIDADO (3 NIVELES)
// ============================================================================

public class ArrayOfNestedTestDbContext(DbContextOptions<ArrayOfNestedTestDbContext> options) : DbContext(options)
{
    public DbSet<LibroCocina> Libros => Set<LibroCocina>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LibroCocina>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).IsRequired();
            entity.ArrayOf(e => e.Categorias);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA COMPLEXTYPE CON LIST<GEOPOINT>
// ============================================================================

public class ArrayOfComplexWithGeoPointTestDbContext(DbContextOptions<ArrayOfComplexWithGeoPointTestDbContext> options) : DbContext(options)
{
    public DbSet<EmpresaLogistica> Empresas => Set<EmpresaLogistica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmpresaLogistica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Rutas);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA COMPLEXTYPE CON LIST<REFERENCES>
// ============================================================================

public class ArrayOfComplexWithReferencesTestDbContext(DbContextOptions<ArrayOfComplexWithReferencesTestDbContext> options) : DbContext(options)
{
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<Catalogo> Catalogos => Set<Catalogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Etiqueta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<Catalogo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).IsRequired();
            entity.ArrayOf(e => e.Secciones);
        });
    }
}
