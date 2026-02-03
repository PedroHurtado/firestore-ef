using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.MapOf;

// ============================================================================
// DBCONTEXT PARA MAPOF CON ENUM KEY
// ============================================================================

public class MapOfEnumKeyTestDbContext(DbContextOptions<MapOfEnumKeyTestDbContext> options) : DbContext(options)
{
    public DbSet<RestauranteConHorarios> Restaurantes => Set<RestauranteConHorarios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RestauranteConHorarios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Direccion).IsRequired();
            entity.MapOf(e => e.HorariosSemanal);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA MAPOF CON OTRO ENUM KEY
// ============================================================================

public class MapOfTipoHabitacionTestDbContext(DbContextOptions<MapOfTipoHabitacionTestDbContext> options) : DbContext(options)
{
    public DbSet<HotelConPrecios> Hoteles => Set<HotelConPrecios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HotelConPrecios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.MapOf(e => e.PreciosHabitaciones);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA MAPOF CON STRING KEY
// ============================================================================

public class MapOfStringKeyTestDbContext(DbContextOptions<MapOfStringKeyTestDbContext> options) : DbContext(options)
{
    public DbSet<AplicacionConConfiguraciones> Aplicaciones => Set<AplicacionConConfiguraciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AplicacionConConfiguraciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.MapOf(e => e.Configuraciones);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA MAPOF CON INT KEY
// ============================================================================

public class MapOfIntKeyTestDbContext(DbContextOptions<MapOfIntKeyTestDbContext> options) : DbContext(options)
{
    public DbSet<AlmacenConSecciones> Almacenes => Set<AlmacenConSecciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlmacenConSecciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Ubicacion).IsRequired();
            entity.MapOf(e => e.Secciones);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA MAPOF CON DICTIONARY MUTABLE
// ============================================================================

public class MapOfMutableTestDbContext(DbContextOptions<MapOfMutableTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConCategorias> Tiendas => Set<TiendaConCategorias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConCategorias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.MapOf(e => e.Categorias);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA AUTO-DETECCIÓN (SIN CONFIGURACIÓN EXPLÍCITA)
// ============================================================================

public class MapOfConventionTestDbContext(DbContextOptions<MapOfConventionTestDbContext> options) : DbContext(options)
{
    public DbSet<ProductoConTraducciones> Productos => Set<ProductoConTraducciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // NO se configura MapOf explícitamente
        // La Convention debe detectarlo automáticamente
        modelBuilder.Entity<ProductoConTraducciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Codigo).IsRequired();
        });
    }
}

// ============================================================================
// CASO COMPLEJO 1: PROPIEDADES IGNORADAS EN ELEMENTOS
// ============================================================================

public class MapOfIgnoredPropertiesTestDbContext(DbContextOptions<MapOfIgnoredPropertiesTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConPreciosCalculados> Tiendas => Set<TiendaConPreciosCalculados>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConPreciosCalculados>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.MapOf(e => e.PreciosPorCategoria, element =>
            {
                // Ignorar propiedades calculadas
                element.Ignore(p => p.PrecioFinal);
                element.Ignore(p => p.Descripcion);
            });
        });
    }
}

// ============================================================================
// CASO COMPLEJO 2: ARRAYOF DENTRO DE ELEMENTOS DE MAPOF
// ============================================================================

public class MapOfWithArrayOfTestDbContext(DbContextOptions<MapOfWithArrayOfTestDbContext> options) : DbContext(options)
{
    public DbSet<NegocioConHorariosFranjas> Negocios => Set<NegocioConHorariosFranjas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NegocioConHorariosFranjas>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Tipo).IsRequired();
            entity.MapOf(e => e.Horarios, element =>
            {
                // ArrayOf anidado dentro del elemento del Map
                element.ArrayOf(h => h.Franjas);
            });
        });
    }
}

// ============================================================================
// CASO COMPLEJO 3: REFERENCES DENTRO DE ELEMENTOS DE MAPOF
// ============================================================================

public class MapOfWithReferenceTestDbContext(DbContextOptions<MapOfWithReferenceTestDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<EmpresaConAreas> Empresas => Set<EmpresaConAreas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Email).IsRequired();
        });

        modelBuilder.Entity<EmpresaConAreas>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RazonSocial).IsRequired();
            entity.MapOf(e => e.Areas, element =>
            {
                // Reference a Usuario dentro del elemento
                element.Reference(a => a.Responsable);
            });
        });
    }
}

// ============================================================================
// CASO COMPLEJO 4: ARRAYOF + REFERENCE EN ELEMENTO
// ============================================================================

public class MapOfWithArrayOfReferencesTestDbContext(DbContextOptions<MapOfWithArrayOfReferencesTestDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<FabricaConTurnos> Fabricas => Set<FabricaConTurnos>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Email).IsRequired();
        });

        modelBuilder.Entity<FabricaConTurnos>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.MapOf(e => e.Turnos, element =>
            {
                // ArrayOf con entidades Usuario - se detectará automáticamente como referencias
                element.ArrayOf(t => t.Empleados);
            });
        });
    }
}
